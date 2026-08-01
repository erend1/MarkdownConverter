using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Tests;

public class ConversionResultTests
{
    [Fact]
    public void Ok_SetsSuccessTrue()
    {
        var result = ConversionResult.Ok("/output/test.pdf", 1024);

        Assert.True(result.Success);
        Assert.Equal("/output/test.pdf", result.OutputFilePath);
        Assert.Equal(1024, result.BytesWritten);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var result = ConversionResult.Fail("/output/test.pdf", "pdflatex failed");

        Assert.False(result.Success);
        Assert.Equal("/output/test.pdf", result.OutputFilePath);
        Assert.Equal("pdflatex failed", result.ErrorMessage);
        Assert.Equal(0, result.BytesWritten);
    }

    [Fact]
    public void Ok_WithZeroBytes_Succeeds()
    {
        var result = ConversionResult.Ok("/output/test.pdf", 0);

        Assert.True(result.Success);
        Assert.Equal(0, result.BytesWritten);
    }
}
