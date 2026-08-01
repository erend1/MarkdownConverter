using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class ToastDurationsTests
{
    [Fact]
    public void Error_StaysLongerThanSuccessAndInfo()
    {
        // Errors must give the user time to read,
        // and crucially must not vanish before success/info would.
        Assert.True(ToastDurations.Error > ToastDurations.Success);
        Assert.True(ToastDurations.Error > ToastDurations.Info);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("info")]
    [InlineData("error")]
    public void AutoDismissFor_ReturnsTypeSpecificDuration(string type)
    {
        var expected = type switch
        {
            "success" => ToastDurations.Success,
            "info" => ToastDurations.Info,
            "error" => ToastDurations.Error,
            _ => ToastDurations.Default
        };

        Assert.Equal(expected, ToastDurations.AutoDismissFor(type));
    }

    [Fact]
    public void AutoDismissFor_UnknownType_ReturnsDefault()
    {
        Assert.Equal(ToastDurations.Default, ToastDurations.AutoDismissFor("warning"));
        Assert.Equal(ToastDurations.Default, ToastDurations.AutoDismissFor(""));
    }

    [Fact]
    public void Error_AtLeastTenSeconds()
    {
        // Pin the contract: errors get a generous read window. If this drops
        // below 10 seconds, it's almost certainly an accidental regression.
        Assert.True(ToastDurations.Error >= TimeSpan.FromSeconds(10));
    }
}
