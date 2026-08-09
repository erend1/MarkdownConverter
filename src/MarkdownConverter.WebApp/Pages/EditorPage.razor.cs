using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Pages;

public partial class EditorPage : IDisposable
{
    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IBibliographyPresenter BibPresenter { get; set; } = default!;
    [Inject] private IToastService ToastService { get; set; } = default!;
    [Inject] private IDesktopCapabilityProvider DesktopCapabilityProvider { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private DotNetObjectReference<EditorPage>? _dotNetRef;
    private CancellationTokenSource? _pendingFilePollCts;

    // Splitter geometry — kept as a percentage so the layout scales when
    // the window is resized. Default 50/50, clamped 20..80 to mirror the
    // limits the old JS splitter enforced.
    private const double MinPct = 20.0;
    private const double MaxPct = 80.0;
    private double _leftPct = 50.0;

    private string LeftPaneStyle =>
        FormattableString.Invariant($"flex: none; width: {_leftPct:0.##}%;");
    private string RightPaneStyle =>
        FormattableString.Invariant($"flex: none; width: {100 - _leftPct:0.##}%;");

    protected override void OnInitialized()
    {
        TabPresenter.AutoSaveFailed += OnAutoSaveFailed;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("domBridge.attachDragDrop", _dotNetRef);
            // Document-level Ctrl+S / Ctrl+Shift+S / Ctrl+N / Ctrl+W — JS
            // matches the chord and clicks the corresponding button. The
            // routing data lives here so all keyboard wiring is auditable
            // from a single C# spot.
            await JS.InvokeVoidAsync("domBridge.attachGlobalShortcuts", new[]
            {
                new { key = "s", shift = false, selector = "[title*='Save to session']" },
                new { key = "s", shift = true,  selector = "[title*='Download']" },
                new { key = "n", shift = false, selector = ".tab-new" },
                new { key = "w", shift = false, selector = ".tab-active .tab-close" }
            });

            var status = await TabPresenter.RestoreStateAsync();
            if (status == LoadStatus.Corrupted)
            {
                ToastService.ShowError(
                    "Saved session was corrupted and has been backed up. Starting fresh.");
            }

            var capabilities = await DesktopCapabilityProvider.GetCapabilitiesAsync();
            if (capabilities.CanReceivePendingFiles)
            {
                // Desktop-only: command-line file opens are exposed by the
                // wrapper as a pending-file queue. Drain once at startup, then
                // keep polling so secondary launches can open files in this
                // already-running tab session.
                await TryOpenPendingFilesAsync();
                StartPendingFilePolling();
            }
        }
    }

    private void StartPendingFilePolling()
    {
        _pendingFilePollCts = new CancellationTokenSource();
        _ = PollPendingFilesAsync(_pendingFilePollCts.Token);
    }

    private async Task PollPendingFilesAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(TryOpenPendingFilesAsync);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task TryOpenPendingFilesAsync()
    {
        try
        {
            var pendingFiles = await JS.InvokeAsync<PendingFile[]?>("fileInterop.fetchPendingFiles");
            if (pendingFiles is null || pendingFiles.Length == 0) return;

            foreach (var pending in pendingFiles)
            {
                if (string.IsNullOrEmpty(pending.Name)) continue;
                await TabPresenter.OpenFileInNewTabAsync(pending.Name, pending.Content ?? string.Empty);
            }
        }
        catch
        {
            // The Desktop marker enables this path, but the queue endpoint can
            // still become unavailable while the host is shutting down.
        }
    }

    private sealed class PendingFile
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private void OnAutoSaveFailed(string message)
    {
        ToastService.ShowError($"Autosave failed: {message}");
    }

    [JSInvokable]
    public async Task OnFileDrop(string fileName, string content)
    {
        await TabPresenter.OpenFileInNewTabAsync(fileName, content);
    }

    [JSInvokable]
    public async Task OnBibDrop(string fileName, string content)
    {
        await BibPresenter.OnBibFileUploadedAsync(fileName, content);
    }

    // ---- Splitter -----------------------------------------------------
    //
    // The splitter element is a Blazor <div> with @onmousedown — the start
    // gesture is handled natively. While dragging, dom-bridge.js attaches
    // a temporary mousemove/mouseup pair at document level and forwards
    // each clientX up to OnSplitterDrag below; the percentage math + clamp
    // live in C# (a single multiply, a divide, a Clamp). Mouseup cleans
    // up the listeners and calls OnSplitterEnd.

    private async Task OnSplitterMouseDown(MouseEventArgs _)
    {
        if (_dotNetRef is null) return;
        await JS.InvokeVoidAsync("domBridge.startSplitterDrag", _dotNetRef);
    }

    [JSInvokable]
    public void OnSplitterDrag(double clientX, double containerLeft, double containerWidth)
    {
        if (containerWidth <= 0) return;
        var pct = (clientX - containerLeft) / containerWidth * 100.0;
        _leftPct = Math.Clamp(pct, MinPct, MaxPct);
        StateHasChanged();
    }

    [JSInvokable]
    public void OnSplitterEnd()
    {
        // No-op for now — the drag listeners self-clean on mouseup inside
        // dom-bridge. Hook left here so the JS shim has a known endpoint.
    }

    public void Dispose()
    {
        _pendingFilePollCts?.Cancel();
        _pendingFilePollCts?.Dispose();
        TabPresenter.AutoSaveFailed -= OnAutoSaveFailed;
        _dotNetRef?.Dispose();
    }
}
