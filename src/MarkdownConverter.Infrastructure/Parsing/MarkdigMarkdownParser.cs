using Markdig;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.MarkdigExtensions;

namespace MarkdownConverter.Infrastructure.Parsing;

public sealed class MarkdigMarkdownParser : IMarkdownParser
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdigMarkdownParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMathematics()
            .UsePipeTables()
            .Use(new CitationExtension())
            .Build();
    }

    public MarkdownDocument Parse(string rawMarkdown)
    {
        var ast = Markdig.Markdown.Parse(rawMarkdown, _pipeline);

        return new MarkdownDocument
        {
            RawMarkdown = rawMarkdown,
            ParsedAst = ast
        };
    }
}
