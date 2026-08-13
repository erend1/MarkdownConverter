using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Components.Primitives;

/// <summary>
/// Reusable find / replace bar. All find / replace orchestration is
/// delegated to <see cref="IFindPresenter"/>; this component only owns
/// the bar's view state (search term, replace term, toggle flags,
/// status string) and forwards user gestures.
/// </summary>
public partial class AppFindBar
{
    [Inject] private IFindPresenter FindPresenter { get; set; } = default!;
    [Inject] private IEditorBridge EditorBridge { get; set; } = default!;
    [Inject] private IToastService ToastService { get; set; } = default!;

    [Parameter] public bool ShowReplace { get; set; }
    [Parameter] public EventCallback<bool> ShowReplaceChanged { get; set; }

    /// <summary>Raised when the user dismisses the find bar (× / Escape).</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>
    /// Raised after every navigation / replace operation so the host
    /// component can repaint the all-match overlay. Decouples
    /// AppFindBar from the highlight mirror — the host owns that DOM.
    /// </summary>
    [Parameter] public EventCallback OnAfterFindChanged { get; set; }

    /// <summary>
    /// The textarea selector the find bar operates against. Defaulted so
    /// the existing single-editor screen needs no extra wiring, but
    /// exposed for a future multi-editor layout.
    /// </summary>
    [Parameter] public string TextareaSelector { get; set; } = ".editor-textarea";

    private AppTextInput? _findInput;
    private string _findText = string.Empty;
    private string _replaceText = string.Empty;
    private string _matchInfo = string.Empty;
    private bool _useWholeWord;
    private bool _useRegex;
    private bool _useCaseSensitive;
    private bool _useInSelection;
    private bool _invalidRegex;
    private bool _showingReplacementResult;
    private bool _restoreCurrentMatchAfterRender;

    private FindOptions CurrentOptions => new()
    {
        WholeWord = _useWholeWord,
        Regex = _useRegex,
        MatchCase = _useCaseSensitive,
        InSelection = _useInSelection
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_restoreCurrentMatchAfterRender) return;

        _restoreCurrentMatchAfterRender = false;
        var currentIndex = FindPresenter.CurrentIndex;
        var matches = FindPresenter.Matches;
        if (currentIndex < 0 || currentIndex >= matches.Count) return;

        // A render triggered by a find/replace button can restore the
        // textarea's post-edit caret after navigation selected the next
        // match. Reapply the C#-owned current range after that render, then
        // let the browser adapter centre its rendered highlight geometry.
        var match = matches[currentIndex];
        await EditorBridge.SetSelectionAfterRenderAsync(
            TextareaSelector, match.Start, match.End);
        await EditorBridge.ScrollSelectionIntoViewAsync(TextareaSelector);
    }

    private void OnFindTextChanged(string value)
    {
        FindPresenter.CancelPendingOperations();
        _restoreCurrentMatchAfterRender = false;
        _findText = value;
        _matchInfo = string.Empty;
        _invalidRegex = false;
        _showingReplacementResult = false;
    }

    private void OnReplaceTextChanged(string value)
    {
        FindPresenter.CancelPendingOperations();
        _replaceText = value;
    }

    private Task OnFindNext() =>
        ExecuteGuardedAsync(FindNextCoreAsync, "Find operation failed");

    private async Task FindNextCoreAsync()
    {
        if (string.IsNullOrEmpty(_findText)) return;
        var result = await FindPresenter.NextAsync(TextareaSelector, _findText, CurrentOptions);
        if (result.IsStale) return;
        _useInSelection = FindPresenter.HasScope;
        _showingReplacementResult = false;
        ScheduleCurrentMatchRestore(result);
        UpdateMatchInfo(result);
        await OnAfterFindChanged.InvokeAsync();
    }

    private Task OnFindPrev() =>
        ExecuteGuardedAsync(FindPrevCoreAsync, "Find operation failed");

    private async Task FindPrevCoreAsync()
    {
        if (string.IsNullOrEmpty(_findText)) return;
        var result = await FindPresenter.PrevAsync(TextareaSelector, _findText, CurrentOptions);
        if (result.IsStale) return;
        _useInSelection = FindPresenter.HasScope;
        _showingReplacementResult = false;
        ScheduleCurrentMatchRestore(result);
        UpdateMatchInfo(result);
        await OnAfterFindChanged.InvokeAsync();
    }

    private Task OnReplace() =>
        ExecuteGuardedAsync(ReplaceCoreAsync, "Replace operation failed");

    private async Task ReplaceCoreAsync()
    {
        if (string.IsNullOrEmpty(_findText)) return;
        var result = await FindPresenter.ReplaceNextAsync(
            TextareaSelector, _findText, _replaceText, CurrentOptions);
        if (result.IsStale) return;
        _useInSelection = FindPresenter.HasScope;
        if (result.Failure != FindFailure.None)
        {
            UpdateFailure(result.Failure);
            return;
        }
        _invalidRegex = false;
        if (result.Navigation is not null)
        {
            _showingReplacementResult = false;
            ScheduleCurrentMatchRestore(result.Navigation);
            UpdateMatchInfo(result.Navigation);
        }
        else
        {
            _restoreCurrentMatchAfterRender = false;
            _showingReplacementResult = true;
            _matchInfo = $"Replaced {result.Count}";
        }
        await OnAfterFindChanged.InvokeAsync();
    }

    private Task OnReplaceAll() =>
        ExecuteGuardedAsync(ReplaceAllCoreAsync, "Replace operation failed");

    private async Task ReplaceAllCoreAsync()
    {
        if (string.IsNullOrEmpty(_findText)) return;
        var result = await FindPresenter.ReplaceAllAsync(
            TextareaSelector, _findText, _replaceText, CurrentOptions);
        if (result.IsStale) return;
        _useInSelection = FindPresenter.HasScope;
        if (result.Failure != FindFailure.None)
        {
            UpdateFailure(result.Failure);
            return;
        }
        _invalidRegex = false;
        _restoreCurrentMatchAfterRender = false;
        _showingReplacementResult = true;
        _matchInfo = $"Replaced {result.Count}";
        await OnAfterFindChanged.InvokeAsync();
    }

    private async Task OnFindKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await OnCloseClicked();
        else if (e.Key == "Enter" && e.ShiftKey)
            await OnFindPrev();
        else if (e.Key == "Enter")
            await OnFindNext();
    }

    private async Task OnReplaceKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await OnCloseClicked();
    }

    private Task ToggleReplace() => ShowReplaceChanged.InvokeAsync(!ShowReplace);

    private async Task ToggleWholeWord()
    {
        _useWholeWord = !_useWholeWord;
        await RefreshAsync();
    }

    private async Task ToggleRegex()
    {
        _useRegex = !_useRegex;
        await RefreshAsync();
    }

    private async Task ToggleCaseSensitive()
    {
        _useCaseSensitive = !_useCaseSensitive;
        await RefreshAsync();
    }

    /// <summary>
    /// Toggles VS-Code-style "find in selection" mode. On enable, the
    /// textarea's current selection is captured as the search scope.
    /// Empty selection on enable is a no-op (flag stays off). On
    /// disable, the scope clears and finds run against the whole
    /// document again.
    /// </summary>
    private Task ToggleInSelection() =>
        ExecuteGuardedAsync(ToggleInSelectionCoreAsync, "Find scope operation failed");

    private async Task ToggleInSelectionCoreAsync()
    {
        if (_useInSelection)
        {
            _useInSelection = false;
            FindPresenter.ClearScope();
        }
        else
        {
            await FindPresenter.SetScopeFromSelectionAsync(TextareaSelector);
            if (FindPresenter.HasScope) _useInSelection = true;
        }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_findText)) return;
        await OnFindNext();
    }

    /// <summary>
    /// Called by the host component (typically <c>MarkdownEditor</c>) on
    /// every textarea content change while the find bar is open. Keeps
    /// the cached match list in sync without changing the navigation
    /// index, then signals the host to re-paint the overlay. Cheap when
    /// nothing actually changed — <see cref="FindSession"/> caches by
    /// (text, pattern, options, scope).
    /// </summary>
    public async Task OnTextareaContentChangedAsync()
        => await ExecuteGuardedAsync(
            OnTextareaContentChangedCoreAsync,
            "Find refresh failed");

    private async Task OnTextareaContentChangedCoreAsync()
    {
        if (string.IsNullOrEmpty(_findText)) return;
        _restoreCurrentMatchAfterRender = false;
        var text = await EditorBridge.GetValueAsync(TextareaSelector);
        var result = FindPresenter.RefreshAgainst(text, _findText, CurrentOptions);
        _useInSelection = FindPresenter.HasScope;
        if (_showingReplacementResult)
            StateHasChanged();
        else
            UpdateMatchInfo(result);
        await OnAfterFindChanged.InvokeAsync();
    }

    /// <summary>
    /// Focuses the query input after the host has rendered the bar. The host
    /// calls this for both first-open and repeated find/replace shortcuts.
    /// </summary>
    public ValueTask FocusQueryAsync() =>
        _findInput?.FocusAsync() ?? ValueTask.CompletedTask;

    /// <summary>
    /// Clears ranges captured from the prior active document while retaining
    /// the user's query and option controls. The next command recomputes
    /// against the newly rendered textarea.
    /// </summary>
    public async Task OnActiveDocumentChangedAsync()
    {
        FindPresenter.Reset();
        _restoreCurrentMatchAfterRender = false;
        _useInSelection = false;
        _invalidRegex = false;
        _showingReplacementResult = false;
        _matchInfo = string.Empty;
        StateHasChanged();
        await OnAfterFindChanged.InvokeAsync();
    }

    private async Task OnCloseClicked()
    {
        FindPresenter.Reset();
        _restoreCurrentMatchAfterRender = false;
        await OnClose.InvokeAsync();
    }

    private void ScheduleCurrentMatchRestore(FindResult result)
    {
        _restoreCurrentMatchAfterRender = result.Failure == FindFailure.None
            && result.Index >= 0;
    }

    private void UpdateMatchInfo(FindResult result)
    {
        _invalidRegex = result.Failure == FindFailure.InvalidPattern;
        _matchInfo = FindStatusFormatter.Format(result);
        StateHasChanged();
    }

    private void UpdateFailure(FindFailure failure)
    {
        _invalidRegex = failure == FindFailure.InvalidPattern;
        _matchInfo = FindStatusFormatter.FormatFailure(failure);
        StateHasChanged();
    }

    private async Task ExecuteGuardedAsync(Func<Task> operation, string message)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            ToastService.ShowError(message, ex.ToString());
        }
    }
}
