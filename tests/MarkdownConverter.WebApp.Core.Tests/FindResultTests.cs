using System.Text.Json;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class FindResultTests
{
    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Default_AllZero()
    {
        var sut = new FindResult();

        Assert.Equal(0, sut.Total);
        Assert.Equal(0, sut.Index);
    }

    [Fact]
    public void Deserialize_FromCamelCaseJson_Succeeds()
    {
        var json = """{"total":17,"index":2}""";

        var result = JsonSerializer.Deserialize<FindResult>(json, Camel);

        Assert.NotNull(result);
        Assert.Equal(17, result!.Total);
        Assert.Equal(2, result.Index);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var original = new FindResult { Total = 42, Index = 7 };

        var json = JsonSerializer.Serialize(original, Camel);
        var roundTripped = JsonSerializer.Deserialize<FindResult>(json, Camel);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Total, roundTripped!.Total);
        Assert.Equal(original.Index, roundTripped.Index);
    }

    [Fact]
    public void InvalidRegexFailure_RoundTrips()
    {
        var original = new FindResult
        {
            Index = -1,
            Failure = FindFailure.InvalidPattern
        };

        var json = JsonSerializer.Serialize(original, Camel);
        var result = JsonSerializer.Deserialize<FindResult>(json, Camel);

        Assert.NotNull(result);
        Assert.Equal(FindFailure.InvalidPattern, result!.Failure);
        Assert.Equal(-1, result.Index);
    }

    [Fact]
    public void TimeoutFailure_RoundTrips()
    {
        var original = new FindResult
        {
            Index = -1,
            Failure = FindFailure.TimedOut
        };

        var json = JsonSerializer.Serialize(original, Camel);
        var result = JsonSerializer.Deserialize<FindResult>(json, Camel);

        Assert.NotNull(result);
        Assert.Equal(FindFailure.TimedOut, result!.Failure);
    }

    [Fact]
    public void NoMatchesSentinel_RoundTrips()
    {
        // total: 0, index: -1 means "search ran, nothing found".
        var sut = JsonSerializer.Deserialize<FindResult>(
            """{"total":0,"index":-1}""", Camel);

        Assert.NotNull(sut);
        Assert.Equal(0, sut!.Total);
        Assert.Equal(-1, sut.Index);
    }
}
