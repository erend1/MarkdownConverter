namespace MarkdownConverter.Core.Models;

/// <summary>
/// Represents a parsed Markdown document.
/// <see cref="ParsedAst"/> is typed as <c>object</c> so Core carries no
/// dependency on any parsing library. Infrastructure casts it back to
/// the concrete AST type (e.g., Markdig.Syntax.MarkdownDocument) internally.
/// </summary>
public sealed class MarkdownDocument
{
    public required string RawMarkdown { get; init; }
    public required object ParsedAst { get; init; }
}
