using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.Converters;
using Moq;

namespace MarkdownConverter.Infrastructure.Tests;

public class LatexFormatConverterTests
{
    private readonly Mock<IFileSystem> _fsMock;
    private readonly LatexFormatConverter _sut;

    public LatexFormatConverterTests()
    {
        _fsMock = new Mock<IFileSystem>();
        _sut = new LatexFormatConverter(_fsMock.Object);
    }

    [Fact]
    public void Format_ReturnsLatex()
    {
        Assert.Equal(ExportFormat.Latex, _sut.Format);
    }

    [Fact]
    public async Task ConvertAsync_WritesLatexContent()
    {
        string? writtenContent = null;
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => writtenContent = content)
            .Returns(Task.CompletedTask);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1000);

        var request = MakeRequest("# Hello", "/out/test.tex");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(writtenContent);
        Assert.Contains(@"\documentclass", writtenContent);
        Assert.Contains(@"\begin{document}", writtenContent);
    }

    [Fact]
    public async Task ConvertAsync_ReturnsCorrectFileSize()
    {
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fsMock.Setup(f => f.GetFileSize("/out/test.tex")).Returns(2048);

        var request = MakeRequest("test", "/out/test.tex");
        var result = await _sut.ConvertAsync(request);

        Assert.Equal(2048, result.BytesWritten);
        Assert.Equal("/out/test.tex", result.OutputFilePath);
    }

    [Fact]
    public async Task ConvertAsync_WritesToCorrectPath()
    {
        string? writtenPath = null;
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, _, _) => writtenPath = path)
            .Returns(Task.CompletedTask);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(0);

        var request = MakeRequest("test", "/output/file.tex");
        await _sut.ConvertAsync(request);

        Assert.Equal("/output/file.tex", writtenPath);
    }

    private static ConversionRequest MakeRequest(string md, string outputPath) =>
        new()
        {
            Document = new MarkdownDocument { RawMarkdown = md, ParsedAst = new object() },
            TargetFormat = ExportFormat.Latex,
            OutputFilePath = outputPath
        };
}
