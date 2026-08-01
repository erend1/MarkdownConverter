using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.Converters;
using Moq;

namespace MarkdownConverter.Infrastructure.Tests;

public class WordFormatConverterTests
{
    private readonly Mock<IFileSystem> _fsMock;
    private readonly WordFormatConverter _sut;

    public WordFormatConverterTests()
    {
        _fsMock = new Mock<IFileSystem>();
        _sut = new WordFormatConverter(_fsMock.Object);
    }

    [Fact]
    public void Format_ReturnsWord()
    {
        Assert.Equal(ExportFormat.Word, _sut.Format);
    }

    [Fact]
    public async Task ConvertAsync_CreatesDocxFile()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(5000);

        var request = MakeRequest("# Hello World", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
        Assert.Equal("/out/test.docx", result.OutputFilePath);
        Assert.Equal(5000, result.BytesWritten);
    }

    [Fact]
    public async Task ConvertAsync_WritesValidDocx()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1000);

        var request = MakeRequest("Hello", "/out/test.docx");
        await _sut.ConvertAsync(request);

        // Stream should have content written (OpenXml produces a ZIP/docx)
        Assert.True(stream.ToArray().Length > 0);
    }

    [Fact]
    public async Task ConvertAsync_HandlesHeadings()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("# H1\n## H2\n### H3", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesBoldAndItalic()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("**bold** and *italic*", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesInlineCode()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("Use `code` here", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesCodeBlock()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("```csharp\nvar x = 1;\n```", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesList()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("1. first\n2. second\n\n- a\n- b", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesTable()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var request = MakeRequest(md, "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesHorizontalRule()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("above\n\n---\n\nbelow", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesBlockQuote()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("> This is a quote", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesInlineMath()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("The value $x^2$ is important", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesDisplayMath()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var md = "$$\nE = mc^2\n$$";
        var request = MakeRequest(md, "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesLink()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("[Google](https://google.com)", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesEmptyDocument()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(0);

        var request = MakeRequest("", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesComplexDocument()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(5000);

        var md = """
            # Main Title

            This is **bold** and *italic* text with `code`.

            ## Section 2

            1. First item
            2. Second item

            > A blockquote

            | Col | Val |
            |-----|-----|
            | A   | 1   |

            ---

            The formula $E = mc^2$ is famous.

            $$
            \sum_{i=1}^{n} x_i
            $$
            """;

        var request = MakeRequest(md, "/out/complex.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    // --- Enhanced Blockquote Tests ---

    [Fact]
    public async Task ConvertAsync_HandlesNestedBlockQuote()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("> outer\n> > inner text", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesBlockQuoteAttribution()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var request = MakeRequest("> Be the change you wish to see.\n>\n> \u2014 Gandhi", "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("NOTE")]
    [InlineData("WARNING")]
    [InlineData("TIP")]
    [InlineData("IMPORTANT")]
    [InlineData("CAUTION")]
    public async Task ConvertAsync_HandlesCallout(string type)
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var md = $"> [!{type}]\n> This is a {type.ToLower()} callout";
        var request = MakeRequest(md, "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesCalloutWithCustomTitle()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var md = "> [!NOTE] Custom Title\n> Content here";
        var request = MakeRequest(md, "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesCalloutWithMultipleParagraphs()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1);

        var md = "> [!WARNING]\n> First paragraph.\n>\n> Second paragraph with **bold**.";
        var request = MakeRequest(md, "/out/test.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ConvertAsync_HandlesComplexBlockQuoteDocument()
    {
        var stream = new MemoryStream();
        _fsMock.Setup(f => f.CreateFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _fsMock.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(5000);

        var md = """
            # Document with Blockquotes

            > A simple styled quote with *italic*.

            > [!NOTE]
            > This is an important note.

            > [!WARNING]
            > Be careful with this operation.

            > The only limit is your imagination.
            >
            > --- Albert Einstein

            > Outer quote
            > > Nested quote
            """;

        var request = MakeRequest(md, "/out/complex.docx");
        var result = await _sut.ConvertAsync(request);

        Assert.True(result.Success);
    }

    private static ConversionRequest MakeRequest(string md, string outputPath) =>
        new()
        {
            Document = new MarkdownDocument { RawMarkdown = md, ParsedAst = new object() },
            TargetFormat = ExportFormat.Word,
            OutputFilePath = outputPath
        };
}
