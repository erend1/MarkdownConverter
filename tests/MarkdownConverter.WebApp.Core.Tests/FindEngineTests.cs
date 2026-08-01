using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class FindEngineTests
{
    private readonly FindEngine _sut = new();

    private static FindOptions Plain => FindOptions.Default;
    private static FindOptions WholeWord => new() { WholeWord = true };
    private static FindOptions Regex => new() { Regex = true };
    private static FindOptions CaseSensitive => new() { MatchCase = true };

    // ---------- empty / null inputs ----------

    [Fact]
    public void EmptyPattern_ReturnsEmpty()
        => Assert.Empty(_sut.FindAll("hello world", "", Plain));

    [Fact]
    public void NullPattern_ReturnsEmpty()
        => Assert.Empty(_sut.FindAll("hello world", null!, Plain));

    [Fact]
    public void NullText_ReturnsEmpty()
        => Assert.Empty(_sut.FindAll(null!, "x", Plain));

    [Fact]
    public void NoMatches_ReturnsEmpty()
        => Assert.Empty(_sut.FindAll("foo bar baz", "qux", Plain));

    // ---------- substring path ----------

    [Fact]
    public void SubstringSingleMatch_LocatesCorrectly()
    {
        var matches = _sut.FindAll("hello world", "world", Plain);

        Assert.Single(matches);
        Assert.Equal(new TextMatch(6, 11), matches[0]);
    }

    [Fact]
    public void SubstringMultipleMatches_FindAllOccurrences()
    {
        // "ababab" → 3 matches of "ab" at 0, 2, 4
        var matches = _sut.FindAll("ababab", "ab", Plain);

        Assert.Equal(3, matches.Count);
        Assert.Equal(new TextMatch(0, 2), matches[0]);
        Assert.Equal(new TextMatch(2, 4), matches[1]);
        Assert.Equal(new TextMatch(4, 6), matches[2]);
    }

    [Fact]
    public void SubstringOverlappingMatches_AdvancesByAtLeastOne()
    {
        // "aaaa" with pattern "aa": expected at 0, 2 (non-overlapping —
        // mirrors the JS `idx += max(1, len)` semantics).
        var matches = _sut.FindAll("aaaa", "aa", Plain);

        Assert.Equal(2, matches.Count);
        Assert.Equal(new TextMatch(0, 2), matches[0]);
        Assert.Equal(new TextMatch(2, 4), matches[1]);
    }

    [Fact]
    public void Substring_CaseInsensitiveByDefault()
    {
        // Default options (MatchCase=false) — VS Code-style.
        var matches = _sut.FindAll("Hello HELLO hello", "hello", Plain);

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Substring_CaseSensitiveWhenRequested()
    {
        var matches = _sut.FindAll("Hello HELLO hello", "hello", CaseSensitive);

        Assert.Single(matches);
        Assert.Equal(new TextMatch(12, 17), matches[0]);
    }

    // ---------- whole-word path ----------

    [Fact]
    public void WholeWord_EscapesRegexMetacharacters()
    {
        // Pattern "a.b" should literally match "a.b", not "a<any>b".
        var matches = _sut.FindAll("xax.bxa.byaxbx", "a.b", WholeWord);

        // Should match "a.b" only at word boundaries — substring "a.b" only
        // occurs surrounded by other chars. With our boundary rules, the
        // engine should reject the embedded occurrences.
        // (The exact count depends on \b's behaviour around '.'; verify it
        // doesn't crash and only the "a.b" runs preceded/followed by word
        // breaks fire. The key contract is: regex metas are escaped.)
        foreach (var m in matches)
            Assert.Equal("a.b", "xax.bxa.byaxbx".Substring(m.Start, m.Length));
    }

    [Fact]
    public void WholeWord_DoesNotMatchEmbeddedSubstring()
    {
        // "cat" should NOT match inside "category".
        var matches = _sut.FindAll("category and a cat", "cat", WholeWord);

        Assert.Single(matches);
        Assert.Equal(15, matches[0].Start);
    }

    [Fact]
    public void WholeWord_RespectsCaseSensitivity()
    {
        var ci = _sut.FindAll("Cat cat CAT", "cat", WholeWord);
        Assert.Equal(3, ci.Count);

        var cs = _sut.FindAll("Cat cat CAT", "cat",
            new FindOptions { WholeWord = true, MatchCase = true });
        Assert.Single(cs);
    }

    // ---------- regex path ----------

    [Fact]
    public void Regex_BasicPattern()
    {
        var matches = _sut.FindAll("abc 123 def 456", @"\d+", Regex);

        Assert.Equal(2, matches.Count);
        Assert.Equal(new TextMatch(4, 7), matches[0]);
        Assert.Equal(new TextMatch(12, 15), matches[1]);
    }

    [Fact]
    public void Regex_InvalidPattern_Throws()
    {
        Assert.Throws<FindPatternException>(
            () => _sut.FindAll("any text", "[unclosed", Regex));
    }

    [Fact]
    public async Task Regex_CatastrophicBacktracking_IsBoundedAndReportedAsTimeout()
    {
        var sut = new FindEngine(TimeSpan.FromMilliseconds(10));
        var input = new string('a', 50_000) + "!";

        var exception = await Assert.ThrowsAsync<FindTimeoutException>(
            () => Task.Run(() => sut.FindAll(input, "^(a+)+$", Regex))
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsType<System.Text.RegularExpressions.RegexMatchTimeoutException>(
            exception.InnerException);
    }

    [Fact]
    public void Regex_NormalPattern_IsUnaffectedByTimeout()
    {
        var sut = new FindEngine(TimeSpan.FromMilliseconds(10));

        var matches = sut.FindAll("abc 123 def 456", @"\d+", Regex);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Regex_InlineFlagsHonoured()
    {
        // `(?i)` makes the rest of the pattern case-insensitive regardless
        // of the MatchCase option — same behaviour as JS RegExp.
        var matches = _sut.FindAll("Hello HELLO hello", "(?i)hello",
            new FindOptions { Regex = true, MatchCase = true });

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Regex_ZeroWidthMatchAdvancesByOne()
    {
        // `(?=a)` is a zero-width lookahead. The engine must advance past
        // each empty match instead of looping forever. Empty matches are
        // skipped, so the resulting list is empty even though `Match.Success`
        // returns true at every position before an 'a'.
        var matches = _sut.FindAll("aaa", "(?=a)", Regex);

        Assert.Empty(matches);
    }

    [Fact]
    public void Regex_CaseInsensitiveByDefault()
    {
        var matches = _sut.FindAll("Hello HELLO hello", "h\\w+", Regex);

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Regex_CaseSensitiveWhenRequested()
    {
        var matches = _sut.FindAll("Hello HELLO hello", "h\\w+",
            new FindOptions { Regex = true, MatchCase = true });

        Assert.Single(matches);
    }

    // ---------- scope (find in selection) ----------

    [Fact]
    public void Scope_FiltersToInRangeMatches()
    {
        // Text: indices  0123456789012345678901
        //                "foo bar foo bar foo bar"
        // Plain "foo" matches at 0, 8, 16. With scope [4..16) only the
        // one at 8 should remain (16's end=19 exceeds scopeEnd=16).
        var matches = _sut.FindAll("foo bar foo bar foo bar", "foo", Plain,
            scopeStart: 4, scopeEnd: 16);

        Assert.Single(matches);
        Assert.Equal(8, matches[0].Start);
    }

    [Fact]
    public void Scope_ExcludesMatchCrossingBoundary()
    {
        // "abcdef" — match "cd" at index 2, ending at 4. With scope
        // [3..6) the match starts before scopeStart, so it's excluded.
        var matches = _sut.FindAll("abcdef", "cd", Plain,
            scopeStart: 3, scopeEnd: 6);

        Assert.Empty(matches);
    }

    [Fact]
    public void Scope_OnlyOneBoundProvided_IgnoresScope()
    {
        // scopeStart without scopeEnd should not filter — engine treats it
        // as "no scope" so the existing tests pass unchanged.
        var matches = _sut.FindAll("foo foo", "foo", Plain, scopeStart: 4);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Scope_EmptyRange_IgnoresScope()
    {
        // scopeEnd <= scopeStart → no filter (defensive against a stale
        // selection where end has rolled past start).
        var matches = _sut.FindAll("foo foo", "foo", Plain,
            scopeStart: 5, scopeEnd: 5);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Scope_AppliesToRegexResults()
    {
        var matches = _sut.FindAll("abc 123 def 456", @"\d+", Regex,
            scopeStart: 0, scopeEnd: 10);

        // Only "123" (4..7) lies inside [0..10).
        Assert.Single(matches);
        Assert.Equal(4, matches[0].Start);
    }
}
