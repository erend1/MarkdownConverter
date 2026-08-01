using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Interfaces;

/// <summary>
/// Parses raw Markdown text into a <see cref="MarkdownDocument"/>.
/// This is the seam between Core and the parsing library (e.g., Markdig).
/// </summary>
public interface IMarkdownParser
{
    MarkdownDocument Parse(string rawMarkdown);
}
