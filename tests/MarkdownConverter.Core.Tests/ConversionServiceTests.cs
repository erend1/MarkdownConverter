using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Core.Services;
using Moq;

namespace MarkdownConverter.Core.Tests;

public class ConversionServiceTests
{
    private readonly Mock<IMarkdownParser> _parserMock;
    private readonly Mock<IConverterFactory> _factoryMock;
    private readonly Mock<IFormatConverter> _converterMock;
    private readonly ConversionService _sut;

    public ConversionServiceTests()
    {
        _parserMock = new Mock<IMarkdownParser>();
        _factoryMock = new Mock<IConverterFactory>();
        _converterMock = new Mock<IFormatConverter>();
        _sut = new ConversionService(_parserMock.Object, _factoryMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsOnNullParser()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ConversionService(null!, _factoryMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ConversionService(_parserMock.Object, null!));
    }

    [Fact]
    public async Task ConvertAsync_ParsesMarkdownAndDelegates()
    {
        var markdown = "# Hello";
        var doc = new MarkdownDocument { RawMarkdown = markdown, ParsedAst = new object() };
        var expected = ConversionResult.Ok("/out/test.docx", 500);

        _parserMock.Setup(p => p.Parse(markdown)).Returns(doc);
        _factoryMock.Setup(f => f.GetConverter(ExportFormat.Word)).Returns(_converterMock.Object);
        _converterMock
            .Setup(c => c.ConvertAsync(It.IsAny<ConversionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.ConvertAsync(markdown, ExportFormat.Word, "/out/test.docx");

        Assert.True(result.Success);
        Assert.Equal(500, result.BytesWritten);
        _parserMock.Verify(p => p.Parse(markdown), Times.Once);
        _factoryMock.Verify(f => f.GetConverter(ExportFormat.Word), Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_PassesDocumentAndFormatInRequest()
    {
        var markdown = "test";
        var doc = new MarkdownDocument { RawMarkdown = markdown, ParsedAst = new object() };
        ConversionRequest? capturedRequest = null;

        _parserMock.Setup(p => p.Parse(markdown)).Returns(doc);
        _factoryMock.Setup(f => f.GetConverter(ExportFormat.Pdf)).Returns(_converterMock.Object);
        _converterMock
            .Setup(c => c.ConvertAsync(It.IsAny<ConversionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConversionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(ConversionResult.Ok("/out/test.pdf", 100));

        await _sut.ConvertAsync(markdown, ExportFormat.Pdf, "/out/test.pdf");

        Assert.NotNull(capturedRequest);
        Assert.Same(doc, capturedRequest.Document);
        Assert.Equal(ExportFormat.Pdf, capturedRequest.TargetFormat);
        Assert.Equal("/out/test.pdf", capturedRequest.OutputFilePath);
    }

    [Fact]
    public async Task ConvertAsync_UsesProvidedOptions()
    {
        var options = new Dictionary<string, string> { ["key"] = "value" };
        var doc = new MarkdownDocument { RawMarkdown = "", ParsedAst = new object() };
        ConversionRequest? capturedRequest = null;

        _parserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        _factoryMock.Setup(f => f.GetConverter(It.IsAny<ExportFormat>())).Returns(_converterMock.Object);
        _converterMock
            .Setup(c => c.ConvertAsync(It.IsAny<ConversionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConversionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(ConversionResult.Ok("", 0));

        await _sut.ConvertAsync("", ExportFormat.Latex, "", options);

        Assert.NotNull(capturedRequest);
        Assert.Equal("value", capturedRequest.Options["key"]);
    }

    [Fact]
    public async Task ConvertAsync_DefaultsOptionsToEmptyDictionary()
    {
        var doc = new MarkdownDocument { RawMarkdown = "", ParsedAst = new object() };
        ConversionRequest? capturedRequest = null;

        _parserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        _factoryMock.Setup(f => f.GetConverter(It.IsAny<ExportFormat>())).Returns(_converterMock.Object);
        _converterMock
            .Setup(c => c.ConvertAsync(It.IsAny<ConversionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConversionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(ConversionResult.Ok("", 0));

        await _sut.ConvertAsync("", ExportFormat.Latex, "");

        Assert.NotNull(capturedRequest);
        Assert.Empty(capturedRequest.Options);
    }

    [Fact]
    public async Task ConvertAsync_PropagatesConverterFailure()
    {
        var doc = new MarkdownDocument { RawMarkdown = "", ParsedAst = new object() };
        var failure = ConversionResult.Fail("/out/test.pdf", "latex error");

        _parserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        _factoryMock.Setup(f => f.GetConverter(It.IsAny<ExportFormat>())).Returns(_converterMock.Object);
        _converterMock
            .Setup(c => c.ConvertAsync(It.IsAny<ConversionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _sut.ConvertAsync("", ExportFormat.Pdf, "/out/test.pdf");

        Assert.False(result.Success);
        Assert.Equal("latex error", result.ErrorMessage);
    }
}
