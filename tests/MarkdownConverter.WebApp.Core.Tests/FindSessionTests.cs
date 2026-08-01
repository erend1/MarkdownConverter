using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class FindSessionTests
{
    private readonly FindEngine _engine = new();

    private FindSession New() => new(_engine);

    // ---------- recompute / cache-hit ----------

    [Fact]
    public void EnsureUpToDate_FreshSession_RunsScan()
    {
        var sut = New();

        sut.EnsureUpToDate("hello world hello", "hello", FindOptions.Default);

        Assert.Equal(2, sut.Matches.Count);
        Assert.Equal(FindFailure.None, sut.Failure);
    }

    [Fact]
    public void EnsureUpToDate_SameInputs_DoesNotResetIndex()
    {
        var sut = New();
        sut.EnsureUpToDate("hello world hello", "hello", FindOptions.Default);
        sut.Next();                                   // index → 0
        sut.Next();                                   // index → 1
        var idxBefore = sut.CurrentIndex;

        sut.EnsureUpToDate("hello world hello", "hello", FindOptions.Default);

        Assert.Equal(idxBefore, sut.CurrentIndex);
    }

    [Fact]
    public void EnsureUpToDate_TextChanged_ResetsIndex()
    {
        var sut = New();
        sut.EnsureUpToDate("hello hello", "hello", FindOptions.Default);
        sut.Next();
        Assert.Equal(0, sut.CurrentIndex);

        sut.EnsureUpToDate("hello", "hello", FindOptions.Default);

        Assert.Equal(-1, sut.CurrentIndex); // fresh seed
        Assert.Single(sut.Matches);
    }

    [Fact]
    public void EnsureUpToDate_OptionsChanged_ResetsIndex()
    {
        var sut = New();
        sut.EnsureUpToDate("Hello hello HELLO", "hello", FindOptions.Default);
        sut.Next();

        sut.EnsureUpToDate("Hello hello HELLO", "hello",
            new FindOptions { MatchCase = true });

        Assert.Equal(-1, sut.CurrentIndex);
        Assert.Single(sut.Matches); // only the lowercase one
    }

    // ---------- Next / Prev navigation ----------

    [Fact]
    public void Next_FromInitialState_LandsOnFirstMatch()
    {
        var sut = New();
        sut.EnsureUpToDate("foo bar foo bar foo", "foo", FindOptions.Default);

        var r = sut.Next();

        Assert.Equal(3, r.Total);
        Assert.Equal(0, r.Index);
    }

    [Fact]
    public void Next_PastLast_WrapsToFirst()
    {
        var sut = New();
        sut.EnsureUpToDate("foo foo", "foo", FindOptions.Default);
        sut.Next();                                   // → 0
        sut.Next();                                   // → 1

        var r = sut.Next();                           // wraps

        Assert.Equal(0, r.Index);
    }

    [Fact]
    public void Prev_FromInitialState_WrapsToLast()
    {
        var sut = New();
        sut.EnsureUpToDate("foo foo foo", "foo", FindOptions.Default);

        var r = sut.Prev();

        Assert.Equal(2, r.Index); // last
    }

    [Fact]
    public void Prev_BeforeFirst_WrapsToLast()
    {
        var sut = New();
        sut.EnsureUpToDate("foo foo foo", "foo", FindOptions.Default);
        sut.Next();                                   // → 0

        var r = sut.Prev();                           // wraps

        Assert.Equal(2, r.Index);
    }

    [Fact]
    public void Next_NoMatches_ReturnsZeroTotal()
    {
        var sut = New();
        sut.EnsureUpToDate("foo bar", "xxxx", FindOptions.Default);

        var r = sut.Next();

        Assert.Equal(0, r.Total);
        Assert.Equal(-1, r.Index);
    }

    // ---------- caret seeding ----------

    [Fact]
    public void SeedFromCaret_BeforeFirstMatch_NextLandsOnFirst()
    {
        var sut = New();
        sut.EnsureUpToDate("xxx foo yyy foo zzz", "foo", FindOptions.Default);

        sut.SeedFromCaret(0);
        var r = sut.Next();

        Assert.Equal(0, r.Index);
    }

    [Fact]
    public void SeedFromCaret_BetweenMatches_NextLandsOnTheOneAfter()
    {
        var sut = New();
        sut.EnsureUpToDate("xxx foo yyy foo zzz", "foo", FindOptions.Default);

        sut.SeedFromCaret(8);  // between the two matches (4..7 and 12..15)
        var r = sut.Next();

        Assert.Equal(1, r.Index);
    }

    [Fact]
    public void SeedFromCaret_PastLast_NextWrapsToFirst()
    {
        var sut = New();
        sut.EnsureUpToDate("xxx foo yyy foo zzz", "foo", FindOptions.Default);

        sut.SeedFromCaret(100); // far past every match
        var r = sut.Next();

        Assert.Equal(0, r.Index); // wrap
    }

    // ---------- invalid pattern ----------

    [Fact]
    public void EnsureUpToDate_InvalidRegex_SetsInvalidFlag()
    {
        var sut = New();

        sut.EnsureUpToDate("any text", "[unclosed",
            new FindOptions { Regex = true });

        Assert.Equal(FindFailure.InvalidPattern, sut.Failure);
        Assert.Empty(sut.Matches);
    }

    [Fact]
    public void Next_AfterInvalidPattern_ReturnsTypedFailure()
    {
        var sut = New();
        sut.EnsureUpToDate("any text", "[unclosed",
            new FindOptions { Regex = true });

        var r = sut.Next();

        Assert.Equal(FindFailure.InvalidPattern, r.Failure);
    }

    // ---------- Reset ----------

    [Fact]
    public void Reset_WipesEverything()
    {
        var sut = New();
        sut.EnsureUpToDate("foo foo", "foo", FindOptions.Default);
        sut.Next();

        sut.Reset();

        Assert.Empty(sut.Matches);
        Assert.Equal(-1, sut.CurrentIndex);
        Assert.Equal(FindFailure.None, sut.Failure);
        Assert.Null(sut.Pattern);
        Assert.Null(sut.TextSnapshot);
    }

    [Fact]
    public void EnsureUpToDate_ValidSearchClearsPreviousFailure()
    {
        var sut = New();
        sut.EnsureUpToDate("any text", "[unclosed",
            new FindOptions { Regex = true });
        Assert.Equal(FindFailure.InvalidPattern, sut.Failure);

        sut.EnsureUpToDate("foo bar", "foo", FindOptions.Default);

        Assert.Equal(FindFailure.None, sut.Failure);
        Assert.Single(sut.Matches);
    }

    [Fact]
    public void EnsureUpToDate_TimeoutSetsTimedOutFailure()
    {
        var engine = new FindEngine(TimeSpan.FromMilliseconds(10));
        var sut = new FindSession(engine);
        var input = new string('a', 50_000) + "!";

        sut.EnsureUpToDate(input, "^(a+)+$", new FindOptions { Regex = true });

        Assert.Equal(FindFailure.TimedOut, sut.Failure);
        Assert.Empty(sut.Matches);
    }

    // ---------- Current ----------

    [Fact]
    public void Current_BeforeAnyNext_IsNull()
    {
        var sut = New();
        sut.EnsureUpToDate("foo foo", "foo", FindOptions.Default);

        Assert.Null(sut.Current);
    }

    [Fact]
    public void Current_AfterNext_ReflectsActiveMatch()
    {
        var sut = New();
        sut.EnsureUpToDate("foo bar foo", "foo", FindOptions.Default);
        sut.Next();

        Assert.Equal(new TextMatch(0, 3), sut.Current);
    }

    // ---------- scope (find in selection) ----------

    [Fact]
    public void EnsureUpToDate_WithScope_FiltersMatches()
    {
        var sut = New();

        sut.EnsureUpToDate("foo bar foo bar foo", "foo", FindOptions.Default,
            scopeStart: 4, scopeEnd: 12);

        // Matches at 0, 8, 16 unscoped; with scope [4..12) only the one at 8.
        Assert.Single(sut.Matches);
        Assert.Equal(8, sut.Matches[0].Start);
    }

    [Fact]
    public void EnsureUpToDate_ScopeChange_TriggersRecompute()
    {
        var sut = New();
        sut.EnsureUpToDate("foo bar foo bar foo", "foo", FindOptions.Default);
        Assert.Equal(3, sut.Matches.Count);

        sut.EnsureUpToDate("foo bar foo bar foo", "foo", FindOptions.Default,
            scopeStart: 0, scopeEnd: 8);

        Assert.Single(sut.Matches); // only the one at 0
        Assert.Equal(0, sut.ScopeStart);
        Assert.Equal(8, sut.ScopeEnd);
    }

    [Fact]
    public void Reset_ClearsScope()
    {
        var sut = New();
        sut.EnsureUpToDate("foo foo foo", "foo", FindOptions.Default,
            scopeStart: 0, scopeEnd: 8);
        Assert.NotNull(sut.ScopeStart);

        sut.Reset();

        Assert.Null(sut.ScopeStart);
        Assert.Null(sut.ScopeEnd);
    }
}
