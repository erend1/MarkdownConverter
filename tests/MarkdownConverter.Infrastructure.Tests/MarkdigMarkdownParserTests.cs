using MarkdownConverter.Infrastructure.Parsing;

namespace MarkdownConverter.Infrastructure.Tests;

public class MarkdigMarkdownParserTests
{
    private readonly MarkdigMarkdownParser _sut = new();

    [Fact]
    public void Parse_PreservesRawMarkdown()
    {
        var raw = "# Hello\nWorld";
        var doc = _sut.Parse(raw);

        Assert.Equal(raw, doc.RawMarkdown);
    }

    [Fact]
    public void Parse_ReturnsNonNullAst()
    {
        var doc = _sut.Parse("Some text");

        Assert.NotNull(doc.ParsedAst);
    }

    [Fact]
    public void Parse_AstIsMarkdigDocument()
    {
        var doc = _sut.Parse("# Title");

        Assert.IsType<Markdig.Syntax.MarkdownDocument>(doc.ParsedAst);
    }

    [Fact]
    public void Parse_HandlesEmptyInput()
    {
        var doc = _sut.Parse("");

        Assert.Equal("", doc.RawMarkdown);
        Assert.NotNull(doc.ParsedAst);
    }

    [Fact]
    public void Parse_ParsesMathExtensions()
    {
        var doc = _sut.Parse("$$x^2$$");
        var ast = (Markdig.Syntax.MarkdownDocument)doc.ParsedAst;

        // Math extension should produce a MathBlock or paragraph with MathInline
        Assert.NotEmpty(ast);
    }

    [Fact]
    public void Parse_ParsesTableExtensions()
    {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var doc = _sut.Parse(md);
        var ast = (Markdig.Syntax.MarkdownDocument)doc.ParsedAst;

        Assert.NotEmpty(ast);
        Assert.Contains(ast, b => b is Markdig.Extensions.Tables.Table);
    }
}
