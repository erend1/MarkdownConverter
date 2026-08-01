namespace MarkdownConverter.WebApp.Core.ViewModels;

public sealed class TabViewModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string FileName { get; set; } = "Untitled.md";
    public string MarkdownText { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public double ScrollRatio { get; set; }
    public int LineCount => string.IsNullOrEmpty(MarkdownText) ? 1 : MarkdownText.Split('\n').Length;
    public int WordCount => string.IsNullOrWhiteSpace(MarkdownText) ? 0 : MarkdownText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    public int CharCount => MarkdownText.Length;

    public string DisplayName => IsDirty ? $"{FileName} *" : FileName;
}
