using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownConverter.Infrastructure.Tests;

/// <summary>
/// Runs a test only where the named TeX and PDF tools are installed. Elsewhere the test is
/// reported as skipped with the missing tool as the reason, never as passed.
/// </summary>
public sealed class LatexFactAttribute : FactAttribute
{
    public LatexFactAttribute(params string[] tools) => Skip = LatexToolchain.MissingReason(tools);
}

/// <inheritdoc cref="LatexFactAttribute"/>
public sealed class LatexTheoryAttribute : TheoryAttribute
{
    public LatexTheoryAttribute(params string[] tools) => Skip = LatexToolchain.MissingReason(tools);
}

/// <summary>
/// Locates and runs the local TeX and PDF tools behind the compiled table tests.
/// </summary>
internal static partial class LatexToolchain
{
    public const string PdfLatex = "pdflatex";
    public const string PdfToText = "pdftotext";
    public const string PdfToPpm = "pdftoppm";

    /// <summary>Environment variable naming a directory that keeps the generated .tex, .log and .pdf files.</summary>
    public const string ArtifactsVariable = "MDCONVERT_LATEX_ARTIFACTS";

    private const int RasterDpi = 150;
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(3);
    private static readonly ConcurrentDictionary<string, string?> Located = new();

    public static string? Find(string tool) => Located.GetOrAdd(tool, Locate);

    /// <summary>Why the tools cannot be used here, or null when all of them are installed.</summary>
    public static string? MissingReason(params string[] tools)
    {
        var missing = tools.Where(tool => Find(tool) is null).ToList();
        return missing.Count == 0
            ? null
            : $"{string.Join(", ", missing)} not found on PATH; compiled LaTeX table verification was not run.";
    }

    /// <summary>
    /// Compiles a document the way the PDF converter does: pdflatex twice in nonstop mode.
    /// </summary>
    public static async Task<CompiledLatex> CompileAsync(string directory, string name, string tex)
    {
        Directory.CreateDirectory(directory);
        var texPath = Path.Combine(directory, name + ".tex");
        await File.WriteAllTextAsync(texPath, tex, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var result = (ExitCode: 0, Output: "");
        for (int pass = 0; pass < 2; pass++)
        {
            result = await RunAsync(
                PdfLatex, directory, null, "-interaction=nonstopmode", $"-output-directory={directory}", texPath);
        }

        var logPath = Path.ChangeExtension(texPath, ".log");
        var log = File.Exists(logPath) ? await File.ReadAllTextAsync(logPath, Encoding.Latin1) : result.Output;
        return new CompiledLatex(name, Path.ChangeExtension(texPath, ".pdf"), result.ExitCode, log);
    }

    /// <summary>The horizontal extent of the words on every page, from <c>pdftotext -bbox</c>.</summary>
    public static async Task<IReadOnlyList<PdfPageExtent>> ReadTextExtentsAsync(string pdfPath)
    {
        var (_, xhtml) = await RunAsync(PdfToText, null, Encoding.UTF8, "-bbox", pdfPath, "-");

        var pages = new List<PdfPageExtent>();
        foreach (Match page in PagePattern().Matches(xhtml))
        {
            double? left = null, right = null;
            string? rightmost = null;
            foreach (Match word in WordPattern().Matches(page.Groups["words"].Value))
            {
                double xMin = Number(word.Groups["xmin"].Value), xMax = Number(word.Groups["xmax"].Value);
                if (left is null || xMin < left) left = xMin;
                if (right is null || xMax > right) (right, rightmost) = (xMax, word.Groups["text"].Value);
            }
            pages.Add(new PdfPageExtent(Number(page.Groups["width"].Value), left, right, rightmost));
        }
        return pages;
    }

    /// <summary>The horizontal extent of everything drawn on every page, from a grey raster.</summary>
    public static async Task<IReadOnlyList<PdfPageExtent>> ReadInkExtentsAsync(string pdfPath)
    {
        var prefix = Path.Combine(Path.GetDirectoryName(pdfPath)!, Path.GetFileNameWithoutExtension(pdfPath) + "-ink");
        await RunAsync(
            PdfToPpm, null, null, "-r", RasterDpi.ToString(CultureInfo.InvariantCulture), "-gray", pdfPath, prefix);

        var pages = new List<PdfPageExtent>();
        var rasters = Directory.GetFiles(Path.GetDirectoryName(prefix)!, Path.GetFileName(prefix) + "-*.pgm");
        foreach (var raster in rasters.OrderBy(PageNumber))
        {
            pages.Add(InkExtent(await File.ReadAllBytesAsync(raster)));
            File.Delete(raster);
        }
        return pages;

        static int PageNumber(string path) =>
            int.Parse(Path.GetFileNameWithoutExtension(path).Split('-')[^1], CultureInfo.InvariantCulture);
    }

    /// <summary>The text of the document in reading order, from <c>pdftotext</c>.</summary>
    public static async Task<string> ReadTextAsync(string pdfPath)
    {
        var (_, text) = await RunAsync(PdfToText, null, Encoding.UTF8, "-enc", "UTF-8", pdfPath, "-");
        return text;
    }

    // Binary PGM: "P5 <width> <height> <maxval>" and one byte per pixel, 0 = black.
    // Anything that is not white counts: a 0.4pt rule covers less than one pixel.
    private static PdfPageExtent InkExtent(byte[] pgm)
    {
        const byte white = 250;
        int position = 0;
        string Token()
        {
            while (char.IsWhiteSpace((char)pgm[position])) position++;
            int start = position;
            while (!char.IsWhiteSpace((char)pgm[position])) position++;
            return Encoding.ASCII.GetString(pgm, start, position - start);
        }

        Assert.Equal("P5", Token());
        int width = int.Parse(Token(), CultureInfo.InvariantCulture);
        int height = int.Parse(Token(), CultureInfo.InvariantCulture);
        Token();
        position++;

        int left = int.MaxValue, right = -1;
        for (int y = 0; y < height; y++)
        {
            var row = pgm.AsSpan(position + y * width, width);
            for (int x = 0; x < left && x < width; x++)
            {
                if (row[x] < white) { left = x; break; }
            }
            for (int x = width - 1; x > right; x--)
            {
                if (row[x] < white) { right = x; break; }
            }
        }

        const double pointsPerPixel = 72.0 / RasterDpi;
        return right < 0
            ? new PdfPageExtent(width * pointsPerPixel, null, null, null)
            : new PdfPageExtent(width * pointsPerPixel, left * pointsPerPixel, (right + 1) * pointsPerPixel, null);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string tool, string? workingDirectory, Encoding? outputEncoding, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Find(tool) ?? throw new InvalidOperationException($"{tool} is not installed."),
            WorkingDirectory = workingDirectory ?? Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = outputEncoding,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(ToolTimeout);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await error;
            return (process.ExitCode, await output);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{tool} did not finish within {ToolTimeout.TotalMinutes} minutes.");
        }
    }

    private static string? Locate(string tool)
    {
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim('"'), tool + extension);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry cannot hold the tool.
                }
            }
        }
        return null;
    }

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"<page width=""(?<width>[0-9.]+)"" height=""[0-9.]+"">(?<words>.*?)</page>", RegexOptions.Singleline)]
    private static partial Regex PagePattern();

    [GeneratedRegex(@"<word xMin=""(?<xmin>[0-9.]+)"" yMin=""[0-9.]+"" xMax=""(?<xmax>[0-9.]+)"" yMax=""[0-9.]+"">(?<text>.*?)</word>")]
    private static partial Regex WordPattern();
}

/// <summary>A compiled document: where its PDF is and what pdflatex logged.</summary>
internal sealed partial record CompiledLatex(string Name, string PdfPath, int ExitCode, string Log)
{
    /// <summary>Every "Overfull \hbox" pdflatex reported, in points, with the place it gave.</summary>
    public IReadOnlyList<(double Points, string Place)> OverfullBoxes { get; } =
        OverfullPattern().Matches(Log)
            .Select(m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), m.Groups[2].Value.Trim()))
            .ToList();

    /// <summary>The end of the log, for failure messages.</summary>
    public string LogTail => Log.Length <= 2500 ? Log : Log[^2500..];

    [GeneratedRegex(@"Overfull \\hbox \(([0-9.]+)pt too wide\) ([^\r\n]*)")]
    private static partial Regex OverfullPattern();
}

/// <summary>Page width and the leftmost and rightmost marks on it, in PDF points.</summary>
internal sealed record PdfPageExtent(double Width, double? Left, double? Right, string? RightmostWord);
