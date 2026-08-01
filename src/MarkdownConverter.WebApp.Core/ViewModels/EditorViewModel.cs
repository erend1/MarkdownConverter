namespace MarkdownConverter.WebApp.Core.ViewModels;

public sealed class EditorViewModel
{
    public string MarkdownText { get; set; } = string.Empty;
    public string FileName { get; set; } = "Untitled.md";
    public bool IsDirty { get; set; }
    public int LineCount => string.IsNullOrEmpty(MarkdownText) ? 1 : MarkdownText.Split('\n').Length;
}
