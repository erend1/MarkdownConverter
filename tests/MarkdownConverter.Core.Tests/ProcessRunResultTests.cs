using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Tests;

public class ProcessRunResultTests
{
    [Fact]
    public void Succeeded_ReturnsTrueWhenExitCodeIsZero()
    {
        var result = new ProcessRunResult
        {
            ExitCode = 0,
            StandardOutput = "ok",
            StandardError = ""
        };

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(127)]
    public void Succeeded_ReturnsFalseWhenExitCodeIsNonZero(int exitCode)
    {
        var result = new ProcessRunResult
        {
            ExitCode = exitCode,
            StandardOutput = "",
            StandardError = "error occurred"
        };

        Assert.False(result.Succeeded);
    }
}
