namespace MarkdownConverter.WebApp.Core.ViewModels;

public sealed class PreviewViewModel
{
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsRendering { get; set; }
}
