namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Persistable snapshot of a single editor tab.
/// </summary>
public sealed class TabState
{
    public string FileName { get; init; } = "Untitled.md";
    public string Content { get; init; } = string.Empty;
}
