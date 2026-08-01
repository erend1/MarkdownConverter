using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class FindStatusFormatterTests
{
    // The find bar must show "current / total" rather than just
    // the total, so the user knows where they are in the navigation cycle.

    [Fact]
    public void Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FindStatusFormatter.Format(null));
    }

    [Fact]
    public void InvalidPattern_RendersInvalidRegex()
    {
        var result = new FindResult
        {
            Index = -1,
            Failure = FindFailure.InvalidPattern
        };

        Assert.Equal(FindStatusFormatter.InvalidRegex, FindStatusFormatter.Format(result));
    }

    [Fact]
    public void TimedOut_RendersSearchTimedOut()
    {
        var result = new FindResult
        {
            Index = -1,
            Failure = FindFailure.TimedOut
        };

        Assert.Equal(FindStatusFormatter.SearchTimedOut, FindStatusFormatter.Format(result));
    }

    [Fact]
    public void StaleResult_RendersEmpty()
    {
        Assert.Equal(string.Empty, FindStatusFormatter.Format(FindResult.Stale));
    }

    [Fact]
    public void ZeroTotal_RendersNoMatches()
    {
        var result = new FindResult { Total = 0, Index = -1 };

        Assert.Equal(FindStatusFormatter.NoMatches, FindStatusFormatter.Format(result));
    }

    [Theory]
    [InlineData(0, 17, "1 / 17")] // first match
    [InlineData(2, 17, "3 / 17")] // middle
    [InlineData(16, 17, "17 / 17")] // last
    [InlineData(0, 1, "1 / 1")] // single match
    public void Format_Renders1BasedCurrentOverTotal(int index, int total, string expected)
    {
        var result = new FindResult { Total = total, Index = index };

        Assert.Equal(expected, FindStatusFormatter.Format(result));
    }

    [Fact]
    public void NegativeIndex_ClampedTo1OfTotal()
    {
        // Defensive: should never happen with non-zero total, but pin the
        // graceful behaviour so a stale state can't crash the renderer.
        var result = new FindResult { Total = 5, Index = -1 };

        Assert.Equal("1 / 5", FindStatusFormatter.Format(result));
    }

    [Fact]
    public void IndexBeyondTotal_ClampedToLastOfTotal()
    {
        // Same defensive rationale as above for the upper bound.
        var result = new FindResult { Total = 5, Index = 99 };

        Assert.Equal("5 / 5", FindStatusFormatter.Format(result));
    }

    [Fact]
    public void Format_InvariantCulture()
    {
        // The "/" separator must not change with culture (tr-TR replaces
        // some delimiters in numeric formatting). Use a value that would
        // be reformatted under a non-invariant culture to confirm.
        var result = new FindResult { Total = 1000, Index = 0 };

        Assert.Equal("1 / 1000", FindStatusFormatter.Format(result));
    }
}
