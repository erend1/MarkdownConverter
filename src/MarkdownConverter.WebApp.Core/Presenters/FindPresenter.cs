using System.Text;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Presenters;

public sealed class FindPresenter : IFindPresenter
{
    private readonly FindEngine _engine;
    private readonly FindSession _session;
    private readonly IEditorBridge _bridge;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _generationSync = new();
    private long _operationGeneration;
    private string? _activePattern;
    private FindOptions? _activeOptions;

    /// <summary>
    /// Tracks whether <see cref="_session"/> has been seeded from the
    /// caret yet. Reset when the cached state is invalidated so the next
    /// navigation call seeds from where the user was typing.
    /// </summary>
    private bool _seededFromCaret;

    /// <summary>
    /// Find-in-selection scope. When both non-null, the engine restricts
    /// matches to <c>[ScopeStart, ScopeEnd)</c>.
    /// </summary>
    private int? _scopeStart;
    private int? _scopeEnd;
    private string? _scopeTextSnapshot;

    public FindPresenter(FindEngine engine, FindSession session, IEditorBridge bridge)
    {
        _engine = engine;
        _session = session;
        _bridge = bridge;
    }

    public async Task<FindResult> NextAsync(string selector, string pattern, FindOptions options)
        => await RunOperationAsync(
            BeginQueryOperation(pattern, options),
            generation => NavigateAsync(selector, pattern, options, forward: true, generation),
            FindResult.Stale);

    public async Task<FindResult> PrevAsync(string selector, string pattern, FindOptions options)
        => await RunOperationAsync(
            BeginQueryOperation(pattern, options),
            generation => NavigateAsync(selector, pattern, options, forward: false, generation),
            FindResult.Stale);

    private async Task<FindResult> NavigateAsync(
        string selector,
        string pattern,
        FindOptions options,
        bool forward,
        long generation)
    {
        if (string.IsNullOrEmpty(pattern))
            return new FindResult { Total = 0, Index = -1 };

        var text = await _bridge.GetValueAsync(selector);
        if (!IsCurrent(generation)) return FindResult.Stale;
        ValidateScopeForText(text);

        var needsRecompute = _session.NeedsRecompute(text, pattern, options, _scopeStart, _scopeEnd);
        _session.EnsureUpToDate(text, pattern, options, _scopeStart, _scopeEnd);

        if (needsRecompute) _seededFromCaret = false;

        if (!_seededFromCaret && _session.Matches.Count > 0)
        {
            var range = await _bridge.GetSelectionAsync(selector);
            if (!IsCurrent(generation)) return FindResult.Stale;
            if (forward)
                _session.SeedFromCaret(range.Start);
            else
                _session.SeedPreviousFromCaret(range.Start);
            _seededFromCaret = true;
        }

        var result = forward ? _session.Next() : _session.Prev();

        if (_session.Current is { } match)
        {
            if (!IsCurrent(generation)) return FindResult.Stale;
            await _bridge.RevealSelectionAsync(selector, match.Start, match.End);
            if (!IsCurrent(generation)) return FindResult.Stale;
        }

        return result;
    }

    public async Task<FindReplaceResult> ReplaceCurrentAsync(
        string selector, string pattern, string replacement, FindOptions options)
        => await RunOperationAsync(
            BeginQueryOperation(pattern, options),
            generation => ReplaceCurrentCoreAsync(
                selector, pattern, replacement, options, generation),
            FindReplaceResult.Stale);

    public Task<FindReplaceResult> ReplaceNextAsync(
        string selector, string pattern, string replacement, FindOptions options)
        => ReplaceCurrentAsync(selector, pattern, replacement, options);

    private async Task<FindReplaceResult> ReplaceCurrentCoreAsync(
        string selector,
        string pattern,
        string replacement,
        FindOptions options,
        long generation)
    {
        if (string.IsNullOrEmpty(pattern)) return new FindReplaceResult();

        var text = await _bridge.GetValueAsync(selector);
        if (!IsCurrent(generation)) return FindReplaceResult.Stale;
        ValidateScopeForText(text);
        _session.EnsureUpToDate(text, pattern, options, _scopeStart, _scopeEnd);
        if (_session.Failure != FindFailure.None)
            return new FindReplaceResult { Failure = _session.Failure };
        if (_session.Current is not { } match) return new FindReplaceResult();

        // The presenter's session is the source of truth. Reapply its current
        // range immediately before the native-undo edit instead of trusting a
        // DOM selection that a browser/render cycle may have collapsed.
        if (!IsCurrent(generation)) return FindReplaceResult.Stale;
        await _bridge.SetSelectionAsync(selector, match.Start, match.End);
        if (!IsCurrent(generation)) return FindReplaceResult.Stale;
        await _bridge.InsertTextAtCursorAsync(selector, replacement);
        ResetAfterDocumentEdit();
        return IsCurrent(generation)
            ? new FindReplaceResult { Count = 1 }
            : FindReplaceResult.Stale;
    }

    public async Task<FindReplaceResult> ReplaceAllAsync(
        string selector, string pattern, string replacement, FindOptions options)
        => await RunOperationAsync(
            BeginQueryOperation(pattern, options),
            generation => ReplaceAllCoreAsync(
                selector, pattern, replacement, options, generation),
            FindReplaceResult.Stale);

    private async Task<FindReplaceResult> ReplaceAllCoreAsync(
        string selector,
        string pattern,
        string replacement,
        FindOptions options,
        long generation)
    {
        if (string.IsNullOrEmpty(pattern)) return new FindReplaceResult();

        var text = await _bridge.GetValueAsync(selector);
        if (!IsCurrent(generation)) return FindReplaceResult.Stale;
        ValidateScopeForText(text);
        IReadOnlyList<TextMatch> matches;
        try
        {
            matches = _engine.FindAll(text, pattern, options, _scopeStart, _scopeEnd);
        }
        catch (FindPatternException)
        {
            return new FindReplaceResult { Failure = FindFailure.InvalidPattern };
        }
        catch (FindTimeoutException)
        {
            return new FindReplaceResult { Failure = FindFailure.TimedOut };
        }
        if (matches.Count == 0) return new FindReplaceResult();

        var newText = BuildReplacedText(text, matches, replacement);

        // Single undoable edit: select the entire textarea, then insertText
        // with the replacement document. Preserves the browser undo stack.
        if (!IsCurrent(generation)) return FindReplaceResult.Stale;
        await _bridge.SetSelectionAsync(selector, 0, text.Length);
        if (!IsCurrent(generation)) return FindReplaceResult.Stale;
        await _bridge.InsertTextAtCursorAsync(selector, newText);

        ResetAfterDocumentEdit();
        return IsCurrent(generation)
            ? new FindReplaceResult { Count = matches.Count }
            : FindReplaceResult.Stale;
    }

    public void Reset()
    {
        CancelPendingOperations();
        _session.Reset();
        _seededFromCaret = false;
        _scopeStart = null;
        _scopeEnd = null;
        _scopeTextSnapshot = null;
    }

    public IReadOnlyList<TextMatch> Matches => _session.Matches;

    public int CurrentIndex => _session.CurrentIndex;

    public bool HasScope => _scopeStart is not null && _scopeEnd is not null;

    public FindResult RefreshAgainst(string text, string pattern, FindOptions options)
    {
        CancelPendingOperations();
        if (string.IsNullOrEmpty(pattern))
        {
            _session.Reset();
            _seededFromCaret = false;
            return new FindResult { Index = -1 };
        }

        // A captured textarea range belongs to the exact document snapshot
        // it came from. Once that text changes, retaining the old offsets can
        // search or replace an unrelated range, so fall back to whole-document
        // search until the user explicitly captures a new selection.
        ValidateScopeForText(text);

        // EnsureUpToDate already short-circuits when none of the inputs
        // changed, so calling this on every keystroke is cheap unless the
        // user actually edited the document.
        _session.EnsureUpToDate(text, pattern, options, _scopeStart, _scopeEnd);
        return new FindResult
        {
            Total = _session.Matches.Count,
            Index = _session.CurrentIndex,
            Failure = _session.Failure
        };
    }

    public async Task SetScopeFromSelectionAsync(string selector)
        => await RunOperationAsync(
            CancelAndGetGeneration(),
            async generation =>
            {
                var range = await _bridge.GetSelectionAsync(selector);
                if (!IsCurrent(generation) || range.End <= range.Start) return false;
                var text = await _bridge.GetValueAsync(selector);
                if (!IsCurrent(generation)) return false;
                _scopeStart = range.Start;
                _scopeEnd = range.End;
                _scopeTextSnapshot = text;
                _seededFromCaret = false;
                return true;
            },
            false);

    public void ClearScope()
    {
        CancelPendingOperations();
        _scopeStart = null;
        _scopeEnd = null;
        _scopeTextSnapshot = null;
        _seededFromCaret = false;
    }

    public void CancelPendingOperations() => CancelAndGetGeneration();

    private async Task<T> RunOperationAsync<T>(
        long generation,
        Func<long, Task<T>> operation,
        T staleResult)
    {
        await _operationGate.WaitAsync();
        try
        {
            if (!IsCurrent(generation)) return staleResult;
            return await operation(generation);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private bool IsCurrent(long generation) =>
        Volatile.Read(ref _operationGeneration) == generation;

    private long BeginQueryOperation(string pattern, FindOptions options)
    {
        lock (_generationSync)
        {
            if (_activePattern != pattern || _activeOptions != options)
            {
                _activePattern = pattern;
                _activeOptions = options;
                _operationGeneration++;
            }

            return _operationGeneration;
        }
    }

    private long CancelAndGetGeneration()
    {
        lock (_generationSync)
        {
            _operationGeneration++;
            return _operationGeneration;
        }
    }

    private void ResetAfterDocumentEdit()
    {
        _session.Reset();
        _seededFromCaret = false;
        _scopeStart = null;
        _scopeEnd = null;
        _scopeTextSnapshot = null;
    }

    private void ValidateScopeForText(string text)
    {
        if (!HasScope || _scopeTextSnapshot == text) return;

        _scopeStart = null;
        _scopeEnd = null;
        _scopeTextSnapshot = null;
        _seededFromCaret = false;
    }

    private static string BuildReplacedText(
        string text, IReadOnlyList<TextMatch> matches, string replacement)
    {
        var sb = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var m in matches)
        {
            if (m.Start > cursor) sb.Append(text, cursor, m.Start - cursor);
            sb.Append(replacement);
            cursor = m.End;
        }
        if (cursor < text.Length) sb.Append(text, cursor, text.Length - cursor);
        return sb.ToString();
    }
}
