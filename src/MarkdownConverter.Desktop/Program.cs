using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Core.Services;
using MarkdownConverter.Infrastructure.Converters;
using MarkdownConverter.Infrastructure.Factories;
using MarkdownConverter.Infrastructure.FileSystem;
using MarkdownConverter.Infrastructure.Parsing;
using MarkdownConverter.Infrastructure.Process;
using MarkdownConverter.Infrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace MarkdownConverter.Desktop;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Windows passes the path of a double-clicked / "Open with" file as
        // args[0]. We accept it here and let MainForm hand it to the Blazor
        // UI via /api/pending-file once the WebView is up.
        var launchRequest = LaunchRequestFactory.FromArgs(args, File.Exists);

        using var singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.IsPrimary)
        {
            try
            {
                singleInstance.SendToPrimaryAsync(launchRequest, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // The primary may be exiting between mutex detection and pipe
                // connection. Secondary launches must still exit without
                // touching WebView2 or the persisted port.
            }

            return;
        }

        ApplicationConfiguration.Initialize();

        using var listenerCts = new CancellationTokenSource();
        using var mainForm = new MainForm(launchRequest.FilePath);
        _ = singleInstance.StartListeningAsync(
            request => mainForm.HandleLaunchRequestAsync(request),
            listenerCts.Token);

        Application.Run(mainForm);
        listenerCts.Cancel();
    }
}

sealed class MainForm : Form
{
    private readonly WebView2 _webView;
    private HttpListener? _listener;
    private readonly int _port;
    private readonly string _wwwrootPath;
    private readonly string _userDataFolder;
    private CancellationTokenSource? _cts;
    private readonly IConversionService _conversionService;
    // Set from the command line in MainForm's ctor and appended to by the
    // single-instance pipe listener. The Blazor UI drains this queue via
    // /api/pending-files.
    private readonly Queue<string> _pendingFilePaths = new();
    private readonly object _pendingLock = new();
    private readonly static JsonSerializerOptions serializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public MainForm(string? pendingFilePath = null)
    {
        EnqueuePendingFile(pendingFilePath);
        Text = "MD Converter";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = LoadAppIcon();

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        // Resolve wwwroot path relative to the executable
        var exeDir = AppContext.BaseDirectory;
        _wwwrootPath = Path.Combine(exeDir, "wwwroot");

        // Durable user-data folder: localStorage lives here, so it must
        // survive reboots (don't put it in %TEMP%).
        _userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarkdownConverter", "WebView2");
        Directory.CreateDirectory(_userDataFolder);

        // Stable loopback port across launches: the WebView2 origin
        // (http://localhost:{port}) is what scopes localStorage. A different
        // port every launch = a different origin = the previous session
        // appears empty even though the data is still there.
        _port = PortStore.ResolveStablePort(Path.Combine(_userDataFolder, "port.txt"));

        // Build DI for PDF conversion (Desktop-exclusive feature)
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var provider = services.BuildServiceProvider();
        _conversionService = provider.GetRequiredService<IConversionService>();

        Load += async (_, _) => await InitializeAsync();
        FormClosing += (_, _) => Cleanup();
    }

    public Task HandleLaunchRequestAsync(LaunchRequest request)
    {
        if (IsDisposed) return Task.CompletedTask;
        if (!IsHandleCreated)
        {
            if (LaunchRequestFactory.TryGetValidFilePath(request, File.Exists, out var filePath))
                EnqueuePendingFile(filePath);
            return Task.CompletedTask;
        }

        BeginInvoke(() =>
        {
            if (LaunchRequestFactory.TryGetValidFilePath(request, File.Exists, out var filePath))
                EnqueuePendingFile(filePath);

            FocusExistingWindow();
        });

        return Task.CompletedTask;
    }

    private async Task InitializeAsync()
    {
        // Start a lightweight HTTP server to serve WASM files
        StartLocalServer();

        // Initialize WebView2 with isolated user data (resolved in the constructor).
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataFolder);

        await _webView.EnsureCoreWebView2Async(env);

        // App-like settings: no context menu, no status bar
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

        // Navigate to the local server
        _webView.CoreWebView2.Navigate($"http://localhost:{_port}/");
    }

    private void StartLocalServer()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
            }
        }, _cts.Token);
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var requestPath = context.Request.Url?.AbsolutePath?.TrimStart('/') ?? "";
            if (string.IsNullOrEmpty(requestPath)) requestPath = "index.html";

            // API: PDF preview (Desktop-exclusive)
            if (requestPath == "api/preview-pdf" && context.Request.HttpMethod == "POST")
            {
                HandlePdfPreview(context);
                return;
            }

            if (requestPath == "api/desktop-status" && context.Request.HttpMethod == "GET")
            {
                context.Response.StatusCode = 204;
                return;
            }

            // API: hand off a file path passed on the command line.
            // Single-shot — the field is nulled after a successful read so a
            // Blazor reload doesn't keep re-opening the same file.
            if (requestPath == "api/pending-file" && context.Request.HttpMethod == "GET")
            {
                HandlePendingFiles(context, singleFile: true);
                return;
            }

            if (requestPath == "api/pending-files" && context.Request.HttpMethod == "GET")
            {
                HandlePendingFiles(context, singleFile: false);
                return;
            }

            // Block service worker registration in Desktop mode — it interferes with local serving
            if (requestPath == "service-worker.js")
            {
                // Return empty service worker that immediately activates and does nothing
                var swBytes = "self.addEventListener('install', e => e.waitUntil(self.skipWaiting()));\nself.addEventListener('activate', e => e.waitUntil(self.clients.claim()));\n"u8.ToArray();
                context.Response.ContentType = "application/javascript";
                context.Response.ContentLength64 = swBytes.Length;
                context.Response.OutputStream.Write(swBytes, 0, swBytes.Length);
                context.Response.Close();
                return;
            }

            var filePath = Path.Combine(_wwwrootPath, requestPath.Replace('/', Path.DirectorySeparatorChar));

            // Try serving with gzip content negotiation
            var acceptEncoding = context.Request.Headers["Accept-Encoding"] ?? "";
            var gzPath = filePath + ".gz";
            if (acceptEncoding.Contains("gzip") && File.Exists(gzPath))
            {
                var content = File.ReadAllBytes(gzPath);
                context.Response.ContentType = GetMimeType(filePath);
                context.Response.ContentLength64 = content.Length;
                context.Response.Headers.Add("Content-Encoding", "gzip");
                context.Response.OutputStream.Write(content, 0, content.Length);
            }
            else if (File.Exists(filePath))
            {
                var content = File.ReadAllBytes(filePath);
                context.Response.ContentType = GetMimeType(filePath);
                context.Response.ContentLength64 = content.Length;
                context.Response.OutputStream.Write(content, 0, content.Length);
            }
            else
            {
                // SPA fallback: serve index.html for unmatched routes
                var indexPath = Path.Combine(_wwwrootPath, "index.html");
                if (File.Exists(indexPath))
                {
                    var content = File.ReadAllBytes(indexPath);
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength64 = content.Length;
                    context.Response.OutputStream.Write(content, 0, content.Length);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
        }
        catch
        {
            context.Response.StatusCode = 500;
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private void HandlePdfPreview(HttpListenerContext context)
    {
        try
        {
            using var reader = new System.IO.StreamReader(context.Request.InputStream);
            var body = reader.ReadToEnd();
            var request = JsonSerializer.Deserialize<PdfPreviewRequest>(body, serializerOptions);
            if (request?.Markdown == null)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "mdconverter_pdf");
            Directory.CreateDirectory(tempDir);
            var outputPath = Path.Combine(tempDir, "preview.pdf");

            var result = _conversionService.ConvertAsync(
                request.Markdown, ExportFormat.Pdf, outputPath).GetAwaiter().GetResult();

            if (result.Success && File.Exists(outputPath))
            {
                var pdfBytes = File.ReadAllBytes(outputPath);
                context.Response.ContentType = "application/pdf";
                context.Response.ContentLength64 = pdfBytes.Length;
                context.Response.OutputStream.Write(pdfBytes, 0, pdfBytes.Length);
            }
            else
            {
                var errorMsg = Encoding.UTF8.GetBytes(result.ErrorMessage ?? "PDF conversion failed");
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = errorMsg.Length;
                context.Response.OutputStream.Write(errorMsg, 0, errorMsg.Length);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = Encoding.UTF8.GetBytes($"Error: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = errorMsg.Length;
            context.Response.OutputStream.Write(errorMsg, 0, errorMsg.Length);
        }
    }

    private sealed class PdfPreviewRequest
    {
        public string? Markdown { get; set; }
    }

    private void HandlePendingFiles(HttpListenerContext context, bool singleFile)
    {
        try
        {
            var pendingFiles = ReadPendingFiles(singleFile);
            if (pendingFiles.Count == 0)
            {
                context.Response.StatusCode = 204; // No Content
                return;
            }

            object response = singleFile ? pendingFiles[0] : pendingFiles;
            var payload = JsonSerializer.Serialize(response, serializerOptions);
            var bytes = Encoding.UTF8.GetBytes(payload);

            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            var msg = Encoding.UTF8.GetBytes($"Error reading file: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = msg.Length;
            context.Response.OutputStream.Write(msg, 0, msg.Length);
        }
    }

    private void EnqueuePendingFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (StartupArgs.GetPendingFilePath(new[] { path }, File.Exists) is not string validPath) return;

        lock (_pendingLock)
        {
            _pendingFilePaths.Enqueue(validPath);
        }
    }

    private List<PendingFile> ReadPendingFiles(bool singleFile)
    {
        var paths = new List<string>();
        lock (_pendingLock)
        {
            while (_pendingFilePaths.Count > 0 && (!singleFile || paths.Count == 0))
                paths.Add(_pendingFilePaths.Dequeue());
        }

        var files = new List<PendingFile>();
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            files.Add(new PendingFile
            {
                Name = Path.GetFileName(path),
                Content = File.ReadAllText(path)
            });
        }

        return files;
    }

    private sealed class PendingFile
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private static string GetMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html",
        ".css" => "text/css",
        ".js" => "application/javascript",
        ".mjs" => "application/javascript",
        ".json" => "application/json",
        ".wasm" => "application/wasm",
        ".dll" => "application/octet-stream",
        ".pdb" => "application/octet-stream",
        ".dat" => "application/octet-stream",
        ".blat" => "application/octet-stream",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".webmanifest" => "application/manifest+json",
        _ => "application/octet-stream"
    };

    private static Icon? LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : null;
    }

    private void FocusExistingWindow()
    {
        if (WindowState == FormWindowState.Minimized)
            ShowWindow(Handle, 9); // SW_RESTORE

        Activate();
        BringToFront();
        SetForegroundWindow(Handle);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void Cleanup()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
        _webView.Dispose();
    }
}
