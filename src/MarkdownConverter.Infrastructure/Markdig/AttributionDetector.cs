using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkdownConverter.Infrastructure.MarkdigExtensions;

/// <summary>
/// Inspects the last paragraph in a QuoteBlock to detect attribution lines
/// (e.g., "— Author Name" or "--- Author Name").
/// </summary>
internal static class AttributionDetector
{
    // Em-dash variants: Unicode em-dash, en-dash, and ASCII triple/double dash
    private static readonly string[] AttributionPrefixes =
    [
        "\u2014",  // — em-dash (SmartyPants converts --- to this)
        "\u2013",  // – en-dash
        "---",     // triple dash (raw)
        "--"       // double dash (raw)
    ];

    /// <summary>
    /// Checks if the last paragraph in the quote is an attribution line.
    /// Returns (true, authorName) if detected, (false, null) otherwise.
    /// </summary>
    public static (bool IsAttribution, string? Author) Detect(QuoteBlock quote)
    {
        if (quote.Count < 2) // Need at least one content paragraph + attribution
            return (false, null);

        var lastBlock = quote[quote.Count - 1];
        if (lastBlock is not ParagraphBlock paragraph || paragraph.Inline == null)
            return (false, null);

        var firstInline = paragraph.Inline.FirstChild;
        if (firstInline is not LiteralInline literal)
            return (false, null);

        var text = literal.Content.ToString().TrimStart();

        foreach (var prefix in AttributionPrefixes)
        {
            if (text.StartsWith(prefix))
            {
                var author = text.Substring(prefix.Length).Trim();
                if (!string.IsNullOrEmpty(author))
                    return (true, author);
            }
        }

        return (false, null);
    }
}
