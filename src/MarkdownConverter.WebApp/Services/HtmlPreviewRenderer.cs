using Markdig;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.Infrastructure.MarkdigExtensions;

namespace MarkdownConverter.WebApp.Services;

public sealed class HtmlPreviewRenderer : IHtmlPreviewRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public HtmlPreviewRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMathematics()
            .UsePipeTables()
            .Use(new CitationExtension())
            .Build();
    }

    public string RenderToHtml(string rawMarkdown)
    {
        if (string.IsNullOrEmpty(rawMarkdown))
            return string.Empty;

        return Markdown.ToHtml(rawMarkdown, _pipeline);
    }
}
