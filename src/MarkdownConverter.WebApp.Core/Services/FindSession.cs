namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Stateful holder for a find / replace session. Caches the last computed
/// match list, the inputs that produced it, and the current navigation
/// index, so consecutive Next / Prev calls don't recompute and the index
/// survives Blazor re-renders that may reset the textarea selection.
/// Reproduces <c>editor-interop.js</c> <c>_ensureFindState</c> in C#.
/// </summary>
public sealed class FindSession
{
    private readonly FindEngine _engine;

    public string? Pattern { get; private set; }
    public FindOptions Options { get; private set; } = FindOptions.Default;
    public string? TextSnapshot { get; private set; }
    public int? ScopeStart { get; private set; }
    public int? ScopeEnd { get; private set; }

    /// <summary>
    /// The cached match list, or an empty list when no search has run, no
    /// matches were found, or the operation failed (see
    /// <see cref="Failure"/>).
    /// </summary>
    public IReadOnlyList<TextMatch> Matches { get; private set; } = Array.Empty<TextMatch>();

    /// <summary>0-based index of the current match, or -1 if none yet.</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>
    /// Expected failure from the latest recomputation.
    /// </summary>
    public FindFailure Failure { get; private set; }

    public FindSession(FindEngine engine) => _engine = engine;

    /// <summary>
    /// Wipes all cached state. Call when the find bar closes so a future
    /// reopen starts with a fresh scan.
    /// </summary>
    public void Reset()
    {
        Pattern = null;
        Options = FindOptions.Default;
        TextSnapshot = null;
        ScopeStart = null;
        ScopeEnd = null;
        Matches = Array.Empty<TextMatch>();
        CurrentIndex = -1;
        Failure = FindFailure.None;
    }

    /// <summary>
    /// Refreshes the cached match list if any of the inputs changed,
    /// otherwise returns the cache. Sets <see cref="IsInvalidPattern"/>
    /// when the supplied regex pattern fails to compile.
    ///
    /// When <paramref name="scopeStart"/> and <paramref name="scopeEnd"/>
    /// are both supplied, the underlying engine restricts matches to
    /// <c>[scopeStart, scopeEnd)</c> — VS Code's "find in selection".
    /// </summary>
    public void EnsureUpToDate(
        string text,
        string pattern,
        FindOptions options,
        int? scopeStart = null,
        int? scopeEnd = null)
    {
        if (!NeedsRecompute(text, pattern, options, scopeStart, scopeEnd)) return;

        Pattern = pattern;
        Options = options;
        TextSnapshot = text;
        ScopeStart = scopeStart;
        ScopeEnd = scopeEnd;

        try
        {
            Matches = _engine.FindAll(text, pattern, options, scopeStart, scopeEnd);
            Failure = FindFailure.None;
        }
        catch (FindPatternException)
        {
            Matches = Array.Empty<TextMatch>();
            Failure = FindFailure.InvalidPattern;
        }
        catch (FindTimeoutException)
        {
            Matches = Array.Empty<TextMatch>();
            Failure = FindFailure.TimedOut;
        }
        CurrentIndex = -1;
    }

    /// <summary>
    /// True if any of (text, pattern, options, scope) differ from the cache.
    /// </summary>
    public bool NeedsRecompute(
        string text,
        string pattern,
        FindOptions options,
        int? scopeStart = null,
        int? scopeEnd = null)
        => TextSnapshot != text
        || Pattern != pattern
        || Options != options
        || ScopeStart != scopeStart
        || ScopeEnd != scopeEnd;

    /// <summary>
    /// Positions <see cref="CurrentIndex"/> so the next <see cref="Next"/>
    /// call lands on the first match at or after the supplied caret
    /// position. If the caret is past every match, the next Next() will
    /// wrap to the first match.
    /// </summary>
    public void SeedFromCaret(int caretPosition)
    {
        for (var i = 0; i < Matches.Count; i++)
        {
            if (Matches[i].Start >= caretPosition)
            {
                CurrentIndex = i - 1;
                return;
            }
        }
        // Caret is past every match: wrap on next Next().
        CurrentIndex = Matches.Count - 1;
    }

    /// <summary>
    /// Positions <see cref="CurrentIndex"/> so the next <see cref="Prev"/>
    /// call lands on the nearest match before the supplied caret position.
    /// If the caret is before the first match, Prev wraps to the last match.
    /// </summary>
    public void SeedPreviousFromCaret(int caretPosition)
    {
        for (var i = 0; i < Matches.Count; i++)
        {
            if (Matches[i].Start >= caretPosition)
            {
                CurrentIndex = i;
                return;
            }
        }

        // The caret is past every match. A one-past-the-end seed lets
        // Prev() land on the final match without special navigation policy.
        CurrentIndex = Matches.Count;
    }

    /// <summary>
    /// Advances <see cref="CurrentIndex"/> to the next match (wrapping at
    /// the end of the list). Returns the new position as a
    /// <see cref="FindResult"/>.
    /// </summary>
    public FindResult Next()
    {
        if (Failure != FindFailure.None)
            return new FindResult { Index = -1, Failure = Failure };
        if (Matches.Count == 0) return new FindResult { Total = 0, Index = -1 };
        // From -1 (initial) → 0; from N-1 → 0; otherwise +1. Explicit
        // form rather than modular arithmetic because (-1 - 1 + N) % N
        // gives N-2, which is not the "wrap to last match" we want for
        // Prev() from the initial state.
        CurrentIndex = CurrentIndex + 1 >= Matches.Count ? 0 : CurrentIndex + 1;
        return new FindResult { Total = Matches.Count, Index = CurrentIndex };
    }

    /// <summary>
    /// Steps <see cref="CurrentIndex"/> back to the previous match
    /// (wrapping at the start of the list). Returns the new position.
    /// From the initial state (<c>CurrentIndex == -1</c>), wraps to the
    /// last match — matching VS Code's behaviour when Shift+Enter is
    /// pressed before any forward navigation.
    /// </summary>
    public FindResult Prev()
    {
        if (Failure != FindFailure.None)
            return new FindResult { Index = -1, Failure = Failure };
        if (Matches.Count == 0) return new FindResult { Total = 0, Index = -1 };
        CurrentIndex = CurrentIndex <= 0 ? Matches.Count - 1 : CurrentIndex - 1;
        return new FindResult { Total = Matches.Count, Index = CurrentIndex };
    }

    /// <summary>
    /// The currently selected match, or <c>null</c> when there are no
    /// matches or none has been visited yet.
    /// </summary>
    public TextMatch? Current
        => CurrentIndex >= 0 && CurrentIndex < Matches.Count
            ? Matches[CurrentIndex]
            : null;
}
