using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Infrastructure.Bibliography;

internal static class BibTexParser
{
    private static readonly HashSet<string> SkipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "comment", "preamble", "string"
    };

    private static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "author", "title", "year", "journal", "publisher", "booktitle",
        "url", "doi", "pages", "volume", "number"
    };

    public static IReadOnlyDictionary<string, BibEntry> Parse(string bibContent)
    {
        var entries = new Dictionary<string, BibEntry>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(bibContent))
            return entries;

        var pos = 0;
        while (pos < bibContent.Length)
        {
            // Find next @
            pos = bibContent.IndexOf('@', pos);
            if (pos < 0) break;
            pos++; // skip @

            // Read entry type
            var typeStart = pos;
            while (pos < bibContent.Length && char.IsLetterOrDigit(bibContent[pos]))
                pos++;

            var entryType = bibContent[typeStart..pos].Trim();

            // Skip whitespace
            while (pos < bibContent.Length && char.IsWhiteSpace(bibContent[pos]))
                pos++;

            if (pos >= bibContent.Length || bibContent[pos] != '{')
                continue;
            pos++; // skip {

            if (SkipTypes.Contains(entryType))
            {
                // Skip to matching closing brace
                SkipBracedContent(bibContent, ref pos);
                continue;
            }

            // Read citation key (until comma or whitespace)
            var keyStart = pos;
            while (pos < bibContent.Length && bibContent[pos] != ',' && bibContent[pos] != '}')
                pos++;

            var key = bibContent[keyStart..pos].Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            if (pos < bibContent.Length && bibContent[pos] == ',')
                pos++; // skip comma

            // Parse fields
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (pos < bibContent.Length && bibContent[pos] != '}')
            {
                // Skip whitespace
                while (pos < bibContent.Length && char.IsWhiteSpace(bibContent[pos]))
                    pos++;

                if (pos >= bibContent.Length || bibContent[pos] == '}')
                    break;

                // Read field name
                var fieldStart = pos;
                while (pos < bibContent.Length && bibContent[pos] != '=' && bibContent[pos] != '}' && !char.IsWhiteSpace(bibContent[pos]))
                    pos++;

                var fieldName = bibContent[fieldStart..pos].Trim().TrimEnd(',');

                // Skip whitespace and =
                while (pos < bibContent.Length && (char.IsWhiteSpace(bibContent[pos]) || bibContent[pos] == '='))
                    pos++;

                if (pos >= bibContent.Length || bibContent[pos] == '}')
                    break;

                // Read field value
                var value = ReadFieldValue(bibContent, ref pos);

                if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(value))
                {
                    fields[fieldName] = value;
                }

                // Skip comma and whitespace
                while (pos < bibContent.Length && (bibContent[pos] == ',' || char.IsWhiteSpace(bibContent[pos])))
                    pos++;
            }

            if (pos < bibContent.Length && bibContent[pos] == '}')
                pos++; // skip closing }

            entries[key] = new BibEntry
            {
                Key = key,
                EntryType = entryType.ToLowerInvariant(),
                Author = fields.GetValueOrDefault("author"),
                Title = fields.GetValueOrDefault("title"),
                Year = fields.GetValueOrDefault("year"),
                Journal = fields.GetValueOrDefault("journal"),
                Publisher = fields.GetValueOrDefault("publisher"),
                BookTitle = fields.GetValueOrDefault("booktitle"),
                Url = fields.GetValueOrDefault("url"),
                Doi = fields.GetValueOrDefault("doi"),
                Pages = fields.GetValueOrDefault("pages"),
                Volume = fields.GetValueOrDefault("volume"),
                Number = fields.GetValueOrDefault("number"),
                ExtraFields = fields
                    .Where(kv => !KnownFields.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            };
        }

        return entries;
    }

    private static string ReadFieldValue(string content, ref int pos)
    {
        if (pos >= content.Length) return "";

        if (content[pos] == '{')
        {
            // Braced value: handle nested braces
            pos++; // skip opening {
            var depth = 1;
            var start = pos;
            while (pos < content.Length && depth > 0)
            {
                if (content[pos] == '{') depth++;
                else if (content[pos] == '}') depth--;
                if (depth > 0) pos++;
            }
            var value = content[start..pos];
            if (pos < content.Length) pos++; // skip closing }
            return value;
        }
        else if (content[pos] == '"')
        {
            // Quoted value
            pos++; // skip opening "
            var start = pos;
            while (pos < content.Length && content[pos] != '"')
                pos++;
            var value = content[start..pos];
            if (pos < content.Length) pos++; // skip closing "
            return value;
        }
        else
        {
            // Unquoted value (number or macro)
            var start = pos;
            while (pos < content.Length && content[pos] != ',' && content[pos] != '}' && !char.IsWhiteSpace(content[pos]))
                pos++;
            return content[start..pos].Trim();
        }
    }

    private static void SkipBracedContent(string content, ref int pos)
    {
        var depth = 1;
        while (pos < content.Length && depth > 0)
        {
            if (content[pos] == '{') depth++;
            else if (content[pos] == '}') depth--;
            pos++;
        }
    }
}
