using Markdig;
using Markdig.Syntax;
using MarkdownConverter.Infrastructure.MarkdigExtensions;

namespace MarkdownConverter.Infrastructure.Tests;

public class AttributionDetectorTests
{
    private static QuoteBlock ParseQuote(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var doc = Markdown.Parse(markdown, pipeline);
        return doc.Descendants<QuoteBlock>().First();
    }

    [Fact]
    public void Detect_UnicodeEmDash_ReturnsAuthor()
    {
        // Two separate paragraphs in the blockquote (empty > line separates them)
        var quote = ParseQuote("> Be the change.\n>\n> \u2014 Gandhi");
        var (isAttribution, author) = AttributionDetector.Detect(quote);

        Assert.True(isAttribution);
        Assert.Equal("Gandhi", author);
    }

    [Fact]
    public void Detect_PlainQuote_ReturnsFalse()
    {
        var quote = ParseQuote("> Just a regular quote");
        var (isAttribution, _) = AttributionDetector.Detect(quote);

        Assert.False(isAttribution);
    }

    [Fact]
    public void Detect_SingleParagraph_ReturnsFalse()
    {
        // Need at least 2 paragraphs (content + attribution)
        var quote = ParseQuote("> \u2014 Author only");
        var (isAttribution, _) = AttributionDetector.Detect(quote);

        Assert.False(isAttribution);
    }

    [Fact]
    public void Detect_DoubleDash_ReturnsAuthor()
    {
        var quote = ParseQuote("> Great quote.\n>\n> -- Author Name");
        var (isAttribution, author) = AttributionDetector.Detect(quote);

        Assert.True(isAttribution);
        Assert.Equal("Author Name", author);
    }

    [Fact]
    public void Detect_TripleDash_ReturnsAuthor()
    {
        var quote = ParseQuote("> Wise words.\n>\n> --- Famous Person");
        var (isAttribution, author) = AttributionDetector.Detect(quote);

        Assert.True(isAttribution);
        Assert.Equal("Famous Person", author);
    }

    [Fact]
    public void Detect_NoDash_ReturnsFalse()
    {
        var quote = ParseQuote("> Line one.\n>\n> Line two.");
        var (isAttribution, _) = AttributionDetector.Detect(quote);

        Assert.False(isAttribution);
    }
}
