using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.Converters;
using Moq;

namespace MarkdownConverter.Infrastructure.Tests;

public class PdfLatexFormatConverterTests
{
    private readonly Mock<IFileSystem> _fsMock;
    private readonly Mock<IProcessRunner> _procMock;
    private readonly PdfLatexFormatConverter _sut;

    public PdfLatexFormatConverterTests()
    {
        _fsMock = new Mock<IFileSystem>();
        _procMock = new Mock<IProcessRunner>();
        _sut = new PdfLatexFormatConverter(_fsMock.Object, _procMock.Object);
    }

    [Fact]
    public void Format_ReturnsPdf()
    {
        Assert.Equal(ExportFormat.Pdf, _sut.Format);
    }

    [Fact]
    public async Task ConvertAsync_WritesTempTexFile()
    {
        string? writtenContent = null;
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => writtenContent = content)
            .Returns(Task.CompletedTask);
        _procMock.Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        _fsMock.Setup(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = MakeRequest("# Test", "/out/test.pdf");
        await _sut.ConvertAsync(request);

        Assert.NotNull(writtenContent);
        Assert.Contains(@"\documentclass", writtenContent);
    }

    [Fact]
    public async Task ConvertAsync_RunsPdflatexTwice()
    {
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _procMock.Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1 });
        _fsMock.Setup(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = MakeRequest("test", "/out/test.pdf");
        await _sut.ConvertAsync(request);

        _procMock.Verify(
            p => p.RunAsync("pdflatex", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ConvertAsync_FailsWhenPdflatexFails()
    {
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _procMock.Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult { ExitCode = 1, StandardOutput = "", StandardError = "fatal error" });

        var request = MakeRequest("test", "/out/test.pdf");
        var result = await _sut.ConvertAsync(request);

        Assert.False(result.Success);
        Assert.Contains("pdflatex failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ConvertAsync_FailsWhenPdfNotProduced()
    {
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _procMock.Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var request = MakeRequest("test", "/out/test.pdf");
        var result = await _sut.ConvertAsync(request);

        Assert.False(result.Success);
        Assert.Contains("no PDF file was produced", result.ErrorMessage);
    }

    [Fact]
    public async Task ConvertAsync_CopiesPdfToOutputPath()
    {
        string? outputPath = null;
        byte[]? outputBytes = null;

        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _procMock.Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        _fsMock.Setup(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((p, b, _) => { outputPath = p; outputBytes = b; })
            .Returns(Task.CompletedTask);

        var request = MakeRequest("test", "/final/output.pdf");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
        Assert.Equal("/final/output.pdf", outputPath);
        Assert.Equal(4, outputBytes!.Length);
        Assert.Equal(4, result.BytesWritten);
    }

    private static ConversionRequest MakeRequest(string md, string outputPath) =>
        new()
        {
            Document = new MarkdownDocument { RawMarkdown = md, ParsedAst = new object() },
            TargetFormat = ExportFormat.Pdf,
            OutputFilePath = outputPath
        };
}
