using System.Text.RegularExpressions;

namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Pure C# replacement for the find / replace logic that used to live in
/// <c>editor-interop.js</c>. All algorithms are deliberately
/// JS-semantics-compatible:
/// <list type="bullet">
/// <item>Empty search term → empty match list (never throws).</item>
/// <item>Empty regex matches advance the cursor by one to avoid infinite loops.</item>
/// <item>Whole-word mode escapes the term and wraps it in <c>\b…\b</c>.</item>
/// <item>Regex mode honours user-supplied inline flags (e.g. <c>(?i)foo</c>).</item>
/// <item>Case-insensitive (the default when <see cref="FindOptions.MatchCase"/>
/// is <c>false</c>) uses <see cref="RegexOptions.IgnoreCase"/> for regex
/// paths and <see cref="StringComparison.OrdinalIgnoreCase"/> for substring.</item>
/// </list>
/// </summary>
public sealed class FindEngine
{
    /// <summary>
    /// Maximum time allowed for one regex match operation. This bounds
    /// catastrophic backtracking on the Blazor UI thread.
    /// </summary>
    public static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan _regexTimeout;

    public FindEngine()
        : this(DefaultRegexTimeout)
    {
    }

    public FindEngine(TimeSpan regexTimeout)
    {
        if (regexTimeout <= TimeSpan.Zero || regexTimeout == Regex.InfiniteMatchTimeout)
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));

        _regexTimeout = regexTimeout;
    }

    /// <summary>
    /// Returns every match of <paramref name="pattern"/> in
    /// <paramref name="text"/> under the supplied
    /// <paramref name="options"/>. Returns an empty list when
    /// <paramref name="pattern"/> is null or empty. Throws
    /// <see cref="FindPatternException"/> when regex compilation fails.
    ///
    /// When <paramref name="scopeStart"/> and <paramref name="scopeEnd"/>
    /// are both supplied, only matches that lie entirely within
    /// <c>[scopeStart, scopeEnd)</c> are returned — VS Code's "find in
    /// selection" mode.
    /// </summary>
    public IReadOnlyList<TextMatch> FindAll(
        string text,
        string pattern,
        FindOptions options,
        int? scopeStart = null,
        int? scopeEnd = null)
    {
        if (string.IsNullOrEmpty(pattern)) return Array.Empty<TextMatch>();
        if (text is null) return Array.Empty<TextMatch>();

        IReadOnlyList<TextMatch> raw;
        try
        {
            raw = options.Regex || options.WholeWord
                ? FindAllRegex(text, BuildRegex(pattern, options))
                : FindAllSubstring(text, pattern, options.MatchCase);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new FindTimeoutException(
                $"The search exceeded the {_regexTimeout.TotalMilliseconds:0} ms regex limit.",
                ex);
        }

        if (scopeStart is null || scopeEnd is null || scopeEnd <= scopeStart)
            return raw;

        var s = scopeStart.Value;
        var e = scopeEnd.Value;
        var inScope = new List<TextMatch>(raw.Count);
        foreach (var m in raw)
            if (m.Start >= s && m.End <= e) inScope.Add(m);
        return inScope;
    }

    private static IReadOnlyList<TextMatch> FindAllSubstring(string text, string pattern, bool matchCase)
    {
        var comparison = matchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = new List<TextMatch>();
        var idx = 0;
        while (idx <= text.Length)
        {
            var found = text.IndexOf(pattern, idx, comparison);
            if (found < 0) break;
            matches.Add(new TextMatch(found, found + pattern.Length));
            // Mirrors the JS `idx += Math.max(1, searchText.length)` step.
            idx = found + Math.Max(1, pattern.Length);
        }
        return matches;
    }

    private static IReadOnlyList<TextMatch> FindAllRegex(string text, Regex regex)
    {
        var matches = new List<TextMatch>();
        var startAt = 0;
        while (startAt <= text.Length)
        {
            var m = regex.Match(text, startAt);
            if (!m.Success) break;
            if (m.Length == 0)
            {
                // Empty-match guard — advance one character so we don't loop
                // forever on patterns like `a*` or `(?=foo)`.
                startAt = m.Index + 1;
                continue;
            }
            matches.Add(new TextMatch(m.Index, m.Index + m.Length));
            startAt = m.Index + m.Length;
        }
        return matches;
    }

    private Regex BuildRegex(string pattern, FindOptions options)
    {
        var flags = RegexOptions.CultureInvariant;
        if (!options.MatchCase) flags |= RegexOptions.IgnoreCase;

        var regexPattern = options.Regex
            ? pattern
            : $@"\b{Regex.Escape(pattern)}\b"; // whole-word path

        try
        {
            return new Regex(regexPattern, flags, _regexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new FindPatternException(ex.Message, ex);
        }
    }
}
