using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Components.Primitives;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Components;

public partial class MarkdownEditor : IEditorView, IAsyncDisposable
{
    private const string TextareaSelector = ".editor-textarea";
    private const string MirrorSelector = ".editor-mirror";

    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IFindPresenter FindPresenter { get; set; } = default!;
    [Inject] private IEditorBridge EditorBridge { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Visibility for the find bar — owned here because the toolbar /
    // Ctrl+F shortcut both flip it from outside AppFindBar. The actual
    // find / replace logic lives inside AppFindBar (composes IFindPresenter).
    private bool _showFind;
    private bool _showReplace;

    // Tracks which tab the editor-element JS handlers are currently bound to.
    // The textarea has @key=ActiveTab.Id, so opening a file / switching tabs
    // tears down the DOM element and the listeners go with it. We rebind only
    // when the active tab actually changes — keystroke renders are no-ops.
    private string? _lastBoundTabId;
    private DotNetObjectReference<MarkdownEditor>? _self;
    private readonly string _findShortcutOwnerId = Guid.NewGuid().ToString("N");
    private bool _findShortcutAttached;
    private bool _focusFindInputAfterRender;

    // Captured via @ref so OnInput can poke the find bar when the user
    // types — keeps the all-match overlay in sync without making
    // MarkdownEditor a duplicate of AppFindBar's state.
    private AppFindBar? _findBar;

    protected override void OnInitialized()
    {
        TabPresenter.AttachEditor(this);
        FindPresenter.Reset();
        _self = DotNetObjectReference.Create(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Find shortcut binds at the document level — bind once.
            await JS.InvokeVoidAsync(
                "domBridge.attachFindShortcut", _self, _findShortcutOwnerId);
            _findShortcutAttached = true;
        }

        var currentTabId = TabPresenter.ActiveTab.Id;
        if (firstRender || _lastBoundTabId != currentTabId)
        {
            var activeDocumentChanged = !firstRender && _lastBoundTabId is not null;
            _lastBoundTabId = currentTabId;
            // Textarea-scoped editing chords (Tab / Ctrl+B/I/K/`) — the JS
            // shim only does preventDefault and forwards the chord; all
            // logic lives in EditorShortcutHandler.
            await JS.InvokeVoidAsync("domBridge.attachEditorKeyShim", TextareaSelector, _self);
            // High-frequency scroll-sync and double-click-jump intentionally
            // run locally in JS (routing every scroll tick through Interop
            // is visibly laggy). Isolated in dom-events.js — pure DOM
            // geometry, no business logic.
            await JS.InvokeVoidAsync("domEvents.attachScrollSync", TextareaSelector, ".preview-content");
            await JS.InvokeVoidAsync("domEvents.attachDoubleClickJump", TextareaSelector, ".preview-content");
            // Highlight-overlay scroll mirroring (textarea → .editor-mirror).
            await JS.InvokeVoidAsync("domEvents.attachHighlightScrollSync", TextareaSelector, MirrorSelector);
            // Match ranges and selection scope belong to one document. Clear
            // them before painting the new tab so old offsets never leak.
            if (activeDocumentChanged && _findBar is not null)
                await _findBar.OnActiveDocumentChangedAsync();

            // After a tab change the mirror is a new element — paint only
            // the reset/current find state.
            await RenderHighlightsAsync();
            await EditorBridge.SetScrollRatioAsync(TextareaSelector, TabPresenter.ActiveTab.ScrollRatio);
        }

        if (_focusFindInputAfterRender && _findBar is not null)
        {
            _focusFindInputAfterRender = false;
            await _findBar.FocusQueryAsync();
        }
    }

    [JSInvokable]
    public Task ShowFindBar(bool withReplace) => InvokeAsync(() =>
        {
            _showFind = true;
            _showReplace = withReplace;
            _focusFindInputAfterRender = true;
            StateHasChanged();
        });

    /// <summary>
    /// Called by <c>dom-bridge.js</c>'s editor-key shim when it sees one
    /// of our textarea-scoped chords. The shim has already called
    /// preventDefault on the original event; this handler does the
    /// actual edit via <see cref="IEditorBridge"/>.
    /// </summary>
    [JSInvokable]
    public async Task OnEditorChord(string chordName, string selectedText, int selStart, int selEnd)
    {
        if (!Enum.TryParse<EditorChord>(chordName, out var chord)) return;
        var action = EditorShortcutHandler.Handle(selectedText, chord);
        if (action is null) return;

        await EditorBridge.InsertTextAtCursorAsync(TextareaSelector, action.Replacement);

        if (action.InnerStartOffset is int innerStart
            && action.InnerEndOffset is int innerEnd)
        {
            // The replacement starts at the original selection's start
            // position; offsets returned by the handler are relative to
            // that. Map them back into textarea coordinates.
            await EditorBridge.SetSelectionAsync(
                TextareaSelector, selStart + innerStart, selStart + innerEnd);
        }

        // Mirror the post-edit value back through TabPresenter so
        // ActiveTab.MarkdownText stays in sync with the DOM.
        var newText = await EditorBridge.GetValueAsync(TextareaSelector);
        TabPresenter.OnActiveTabTextChanged(newText);
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        TabPresenter.OnActiveTabTextChanged(text);

        // Keep the all-match overlay in sync while the user is typing —
        // VS Code re-paints highlights on every keystroke. AppFindBar
        // owns the find state (search term, options); we ask it to
        // refresh against the new content and it re-renders the overlay
        // via the OnAfterFindChanged callback.
        if (_findBar is not null)
        {
            await _findBar.OnTextareaContentChangedAsync();
        }
    }

    private Task OnDrop(DragEventArgs e) => Task.CompletedTask;

    private void OnShowReplaceChanged(bool value) => _showReplace = value;

    private async Task OnFindBarClosed()
    {
        _showFind = false;
        _showReplace = false;
        // Clear the all-match overlay alongside the find bar.
        await JS.InvokeVoidAsync("domEvents.renderHighlights",
            MirrorSelector, string.Empty, Array.Empty<TextMatch>(), -1);
        // Find doesn't move focus while it's running (so Enter stays bound
        // to "find next" instead of inserting a newline). On close, hand
        // focus back so the user lands in the editor.
        await EditorBridge.FocusAsync(TextareaSelector);
    }

    /// <summary>
    /// Repaints the .editor-mirror overlay so every match in the current
    /// find session is highlighted; the active match gets the
    /// <c>.match-current</c> shade. Called after every Next / Prev /
    /// Replace via AppFindBar's OnAfterFindChanged callback, and after
    /// any tab change. Cheap when nothing changed because
    /// FindSession.EnsureUpToDate caches by (text, pattern, options, scope).
    /// </summary>
    private async Task RenderHighlightsAsync()
    {
        var text = TabPresenter.ActiveTab.MarkdownText;
        var matches = FindPresenter.Matches;
        await JS.InvokeVoidAsync("domEvents.renderHighlights",
            MirrorSelector, text, matches, FindPresenter.CurrentIndex);
    }

    public void RequestRender() => InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        FindPresenter.Reset();

        if (_findShortcutAttached)
        {
            try
            {
                await JS.InvokeVoidAsync(
                    "domBridge.detachFindShortcut", _findShortcutOwnerId);
            }
            catch (JSDisconnectedException)
            {
                // The browser context is already gone; its listeners are gone
                // with it, so there is nothing left to detach.
            }
        }

        _self?.Dispose();
    }
}
