using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Presenters;

/// <summary>
/// Orchestrates find / replace operations on a textarea element. Owns a
/// <see cref="FindSession"/> for cached match state and uses
/// <see cref="IEditorBridge"/> for the DOM-side reads / writes.
/// </summary>
public interface IFindPresenter
{
    /// <summary>
    /// Advances to the next match and updates the textarea selection
    /// accordingly. Seeds from the user's caret on the first call so the
    /// initial Next lands on the next match after where they were typing.
    /// </summary>
    Task<FindResult> NextAsync(string selector, string pattern, FindOptions options);

    /// <summary>
    /// Steps to the previous match and updates the textarea selection.
    /// </summary>
    Task<FindResult> PrevAsync(string selector, string pattern, FindOptions options);

    /// <summary>
    /// If the textarea's current selection is itself a match, replaces it
    /// with <paramref name="replacement"/> while preserving the browser's
    /// native undo stack. Otherwise navigates to the next match without
    /// replacing.
    /// Returns a typed replacement outcome.
    /// </summary>
    Task<FindReplaceResult> ReplaceNextAsync(
        string selector, string pattern, string replacement, FindOptions options);

    /// <summary>
    /// Replaces every match in the textarea with <paramref name="replacement"/>
    /// in a single undoable edit.
    /// </summary>
    Task<FindReplaceResult> ReplaceAllAsync(
        string selector, string pattern, string replacement, FindOptions options);

    /// <summary>
    /// Supersedes any operation currently awaiting the editor surface.
    /// Used when query text changes before a new search command is issued.
    /// </summary>
    void CancelPendingOperations();

    /// <summary>
    /// Wipes the cached find state. Call when the find bar closes so the
    /// next session starts with a fresh scan.
    /// </summary>
    void Reset();

    /// <summary>
    /// The cached match list from the most recent find operation. Empty
    /// when there is no active search. Consumed by the match-highlight
    /// overlay so the mirror &lt;div&gt; can paint every match.
    /// </summary>
    IReadOnlyList<TextMatch> Matches { get; }

    /// <summary>
    /// 0-based index of the current match (-1 when none). The overlay
    /// renders this match in a brighter shade than the others.
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Re-runs <see cref="FindEngine"/> against the supplied text without
    /// changing the navigation index. Used to keep the all-match overlay
    /// in sync when the user types into the textarea while the find bar
    /// is open.
    /// </summary>
    void RefreshAgainst(string text, string pattern, FindOptions options);

    /// <summary>
    /// Locks subsequent finds to the textarea's current selection range.
    /// No-op when the selection is empty. VS Code's "find in selection"
    /// mode.
    /// </summary>
    Task SetScopeFromSelectionAsync(string selector);

    /// <summary>
    /// Removes any active scope so finds run against the whole document.
    /// </summary>
    void ClearScope();

    /// <summary>
    /// <c>true</c> when a scope range is currently active.
    /// </summary>
    bool HasScope { get; }
}
