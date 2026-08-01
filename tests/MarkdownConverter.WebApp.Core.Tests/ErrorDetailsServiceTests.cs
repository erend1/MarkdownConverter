using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class ErrorDetailsServiceTests
{
    [Fact]
    public void Show_RaisesOnShowWithSuppliedTitleAndDetails()
    {
        var sut = new ErrorDetailsService();
        ErrorDetails? captured = null;
        sut.OnShow += d => captured = d;

        sut.Show("PDF compilation failed", "long log here");

        Assert.NotNull(captured);
        Assert.Equal("PDF compilation failed", captured!.Title);
        Assert.Equal("long log here", captured.Details);
    }

    [Fact]
    public void Show_NoSubscribers_DoesNotThrow()
    {
        var sut = new ErrorDetailsService();
        var ex = Record.Exception(() => sut.Show("title", "details"));
        Assert.Null(ex);
    }

    [Fact]
    public void MultipleShowCalls_EachRaiseSeparately()
    {
        var sut = new ErrorDetailsService();
        var captured = new List<ErrorDetails>();
        sut.OnShow += d => captured.Add(d);

        sut.Show("first", "a");
        sut.Show("second", "b");

        Assert.Equal(2, captured.Count);
        Assert.Equal("first", captured[0].Title);
        Assert.Equal("second", captured[1].Title);
    }
}
