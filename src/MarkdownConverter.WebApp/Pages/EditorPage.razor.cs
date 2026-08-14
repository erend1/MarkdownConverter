using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;

namespace MarkdownConverter.WebApp.Pages;

public partial class EditorPage : IAsyncDisposable
{
    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IBibliographyPresenter BibPresenter { get; set; } = default!;
    [Inject] private IToastService ToastService { get; set; } = default!;
    [Inject] private IDesktopCapabilityProvider DesktopCapabilityProvider { get; set; } = default!;
    [Inject] private IEditorBridge EditorBridge { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private DotNetObjectReference<EditorPage>? _dotNetRef;
    private CancellationTokenSource? _pendingFilePollCts;

    private readonly WorkspaceLayoutState _workspace = new();
    private bool _focusSourceAfterRender;

    private string WorkspaceCssClass => _workspace.SelectedPane == WorkspacePane.Source
        ? "editor-layout workspace-source-selected"
        : "editor-layout workspace-preview-selected";

    private string WorkspaceStyle => FormattableString.Invariant(
        $"--source-pane-percentage: {_workspace.SourcePanePercentage:0.##}%;");

    private string SplitAriaValue => FormattableString.Invariant(
        $"{_workspace.SourcePanePercentage:0.##}");

    private string SplitAriaValueText => FormattableString.Invariant(
        $"Source {_workspace.SourcePanePercentage:0.##}%, preview {_workspace.PreviewPanePercentage:0.##}%");

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

        if (_focusSourceAfterRender)
        {
            _focusSourceAfterRender = false;
            await EditorBridge.FocusAsync(".editor-textarea");
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

    private bool IsPaneSelected(WorkspacePane pane) => _workspace.SelectedPane == pane;

    private string GetPaneButtonClass(WorkspacePane pane) => IsPaneSelected(pane)
        ? "workspace-view-button workspace-view-button-selected"
        : "workspace-view-button";

    private async Task SelectPaneAsync(WorkspacePane pane)
    {
        _workspace.SelectPane(pane);
        _focusSourceAfterRender = pane == WorkspacePane.Source;
        await InvokeAsync(StateHasChanged);
    }

    // Pointer capture and document-level move delivery are browser mechanics;
    // percentage ownership, clamping, keyboard policy, and reset behavior stay
    // in this C# component and its plain WorkspaceLayoutState.
    private async Task OnSplitterPointerDown(PointerEventArgs e)
    {
        if (_dotNetRef is null || e.Button != 0) return;
        await JS.InvokeVoidAsync(
            "domBridge.startSplitterDrag",
            _dotNetRef,
            e.PointerId,
            e.ClientX,
            e.ClientY);
    }

    [JSInvokable]
    public void OnSplitterDrag(double clientX, double containerLeft, double containerWidth)
    {
        if (containerWidth <= 0) return;
        var pct = (clientX - containerLeft) / containerWidth * 100.0;
        _workspace.SetSourcePanePercentage(pct);
        StateHasChanged();
    }

    [JSInvokable]
    public void OnSplitterEnd(bool moved)
    {
        if (!moved)
        {
            _workspace.ResetSplit();
            StateHasChanged();
        }
    }

    private void OnSplitterKeyDown(KeyboardEventArgs e)
    {
        var step = e.ShiftKey ? 10.0 : 2.0;

        if (e.Key is "ArrowLeft" or "ArrowDown")
            _workspace.SetSourcePanePercentage(_workspace.SourcePanePercentage - step);
        else if (e.Key is "ArrowRight" or "ArrowUp")
            _workspace.SetSourcePanePercentage(_workspace.SourcePanePercentage + step);
        else if (e.Key is "Home" or "Enter" or " " or "0")
            _workspace.ResetSplit();
        else
            return;

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _pendingFilePollCts?.Cancel();
        _pendingFilePollCts?.Dispose();
        TabPresenter.AutoSaveFailed -= OnAutoSaveFailed;

        try
        {
            await JS.InvokeVoidAsync("domBridge.detachDragDrop");
            await JS.InvokeVoidAsync("domBridge.cancelSplitterDrag");
        }
        catch (JSDisconnectedException)
        {
            // The browser context is already gone, so temporary pointer
            // listeners disappeared with it.
        }

        _dotNetRef?.Dispose();
    }
}
