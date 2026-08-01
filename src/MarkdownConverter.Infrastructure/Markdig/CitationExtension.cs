using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;

namespace MarkdownConverter.Infrastructure.MarkdigExtensions;

public sealed class CitationExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.InlineParsers.Contains<CitationParser>())
        {
            pipeline.InlineParsers.InsertBefore<LinkInlineParser>(new CitationParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        // No custom HTML renderer needed
    }
}
