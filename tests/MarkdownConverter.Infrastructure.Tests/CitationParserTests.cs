using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownConverter.Infrastructure.MarkdigExtensions;

namespace MarkdownConverter.Infrastructure.Tests;

public class CitationParserTests
{
    private static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Use(new CitationExtension())
            .Build();
    }

    private static CitationInline? FindCitation(string markdown)
    {
        var doc = Markdig.Markdown.Parse(markdown, CreatePipeline());

        foreach (var block in doc)
        {
            if (block is ParagraphBlock para && para.Inline != null)
            {
                foreach (var inline in para.Inline)
                {
                    if (inline is CitationInline citation)
                        return citation;
                }
            }
        }
        return null;
    }

    [Fact]
    public void Parse_SingleCitation()
    {
        var citation = FindCitation("See [@smith2023] for details.");

        Assert.NotNull(citation);
        Assert.Single(citation.Citations);
        Assert.Equal("smith2023", citation.Citations[0].Key);
        Assert.Null(citation.Citations[0].Locator);
    }

    [Fact]
    public void Parse_MultipleCitations()
    {
        var citation = FindCitation("See [@smith2023; @jones2024].");

        Assert.NotNull(citation);
        Assert.Equal(2, citation.Citations.Count);
        Assert.Equal("smith2023", citation.Citations[0].Key);
        Assert.Equal("jones2024", citation.Citations[1].Key);
    }

    [Fact]
    public void Parse_CitationWithLocator()
    {
        var citation = FindCitation("See [@smith2023, p. 42].");

        Assert.NotNull(citation);
        Assert.Single(citation.Citations);
        Assert.Equal("smith2023", citation.Citations[0].Key);
        Assert.Equal("p. 42", citation.Citations[0].Locator);
    }

    [Fact]
    public void Parse_DoesNotMatchPlainLink()
    {
        var doc = Markdig.Markdown.Parse("[Google](https://google.com)", CreatePipeline());
        var para = doc.Descendants<ParagraphBlock>().First();

        // Should be a LinkInline, not a CitationInline
        var hasCitation = para.Inline!.Any(i => i is CitationInline);
        var hasLink = para.Inline!.Any(i => i is LinkInline);

        Assert.False(hasCitation);
        Assert.True(hasLink);
    }

    [Fact]
    public void Parse_DoesNotMatchBracketWithoutAt()
    {
        var citation = FindCitation("See [some text] here.");

        Assert.Null(citation);
    }

    [Fact]
    public void Parse_CitationInMiddleOfText()
    {
        var doc = Markdig.Markdown.Parse("According to [@author2020], this is true.", CreatePipeline());
        var para = doc.Descendants<ParagraphBlock>().First();

        var inlines = para.Inline!.ToList();
        var hasCitation = inlines.Any(i => i is CitationInline);
        var hasLiteral = inlines.Any(i => i is LiteralInline);

        Assert.True(hasCitation);
        Assert.True(hasLiteral);
    }
}
