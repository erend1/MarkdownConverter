using Markdig.Syntax.Inlines;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Infrastructure.MarkdigExtensions;

public sealed class CitationInline : LeafInline
{
    public List<CitationInfo> Citations { get; } = new();
}
