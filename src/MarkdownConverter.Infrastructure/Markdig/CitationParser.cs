using Markdig.Helpers;
using Markdig.Parsers;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Infrastructure.MarkdigExtensions;

public sealed class CitationParser : InlineParser
{
    public CitationParser()
    {
        OpeningCharacters = ['['];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        // Must be [@...] pattern
        var startPos = slice.Start;

        if (slice.PeekCharExtra(1) != '@')
            return false;

        // Find closing bracket
        var content = slice.Text;
        var pos = slice.Start + 2; // skip '[' and '@'
        var depth = 1;

        while (pos < content.Length && depth > 0)
        {
            if (content[pos] == '[') depth++;
            else if (content[pos] == ']') depth--;
            pos++;
        }

        if (depth != 0)
            return false;

        // Extract content between [@ and ]
        var innerStart = slice.Start + 2; // after '[@'
        var innerEnd = pos - 2;           // before ']'

        if (innerEnd < innerStart)
            return false;

        var inner = content.Substring(innerStart, innerEnd - innerStart + 1).Trim();

        if (string.IsNullOrEmpty(inner))
            return false;

        var citation = new CitationInline();

        // Split by semicolons for multiple citations: [@key1; @key2]
        var parts = inner.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // Remove leading @ if present (first entry won't have it since we skipped it,
            // but subsequent entries after ; will have @)
            if (trimmed.StartsWith('@'))
                trimmed = trimmed.Substring(1).Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Check for locator after comma: key, p. 42
            var commaIdx = trimmed.IndexOf(',');
            if (commaIdx >= 0)
            {
                var key = trimmed.Substring(0, commaIdx).Trim();
                var locator = trimmed.Substring(commaIdx + 1).Trim();
                citation.Citations.Add(new CitationInfo
                {
                    Key = key,
                    Locator = string.IsNullOrEmpty(locator) ? null : locator
                });
            }
            else
            {
                citation.Citations.Add(new CitationInfo { Key = trimmed });
            }
        }

        if (citation.Citations.Count == 0)
            return false;

        // Set span
        citation.Span.Start = processor.GetSourcePosition(slice.Start, out var line, out var column);
        citation.Span.End = citation.Span.Start + (pos - startPos - 1);
        citation.Line = line;
        citation.Column = column;

        processor.Inline = citation;
        slice.Start = pos;

        return true;
    }
}
