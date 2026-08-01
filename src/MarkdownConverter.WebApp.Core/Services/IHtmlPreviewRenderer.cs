namespace MarkdownConverter.WebApp.Core.Services;

public interface IHtmlPreviewRenderer
{
    string RenderToHtml(string rawMarkdown);
}
