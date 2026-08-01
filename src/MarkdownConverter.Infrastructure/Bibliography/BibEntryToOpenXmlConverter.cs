using System.Text;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Infrastructure.Bibliography;

internal static class BibEntryToOpenXmlConverter
{
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["article"] = "JournalArticle",
        ["book"] = "Book",
        ["inbook"] = "BookSection",
        ["incollection"] = "BookSection",
        ["inproceedings"] = "ConferenceProceedings",
        ["conference"] = "ConferenceProceedings",
        ["mastersthesis"] = "Report",
        ["phdthesis"] = "Report",
        ["techreport"] = "Report",
        ["misc"] = "Misc",
        ["unpublished"] = "Misc",
        ["manual"] = "Report",
        ["online"] = "InternetSite",
    };

    public static string ToSourcesXml(IReadOnlyDictionary<string, BibEntry> entries)
    {
        const string ns = "http://schemas.openxmlformats.org/officeDocument/2006/bibliography";

        var sb = new StringBuilder();
        sb.AppendLine($@"<b:Sources xmlns:b=""{ns}"" SelectedStyle=""/APASixthEditionOfficeOnline.xsl"">");

        foreach (var entry in entries.Values)
        {
            sb.AppendLine("  <b:Source>");
            sb.AppendLine($"    <b:Tag>{Escape(entry.Key)}</b:Tag>");
            sb.AppendLine($"    <b:SourceType>{GetSourceType(entry.EntryType)}</b:SourceType>");

            if (!string.IsNullOrEmpty(entry.Author))
            {
                sb.AppendLine("    <b:Author>");
                sb.AppendLine("      <b:Author>");
                sb.AppendLine("        <b:NameList>");
                foreach (var person in ParseAuthors(entry.Author))
                {
                    sb.AppendLine("          <b:Person>");
                    sb.AppendLine($"            <b:Last>{Escape(person.Last)}</b:Last>");
                    if (!string.IsNullOrEmpty(person.First))
                        sb.AppendLine($"            <b:First>{Escape(person.First)}</b:First>");
                    sb.AppendLine("          </b:Person>");
                }
                sb.AppendLine("        </b:NameList>");
                sb.AppendLine("      </b:Author>");
                sb.AppendLine("    </b:Author>");
            }

            if (!string.IsNullOrEmpty(entry.Title))
                sb.AppendLine($"    <b:Title>{Escape(entry.Title)}</b:Title>");
            if (!string.IsNullOrEmpty(entry.Year))
                sb.AppendLine($"    <b:Year>{Escape(entry.Year)}</b:Year>");
            if (!string.IsNullOrEmpty(entry.Journal))
                sb.AppendLine($"    <b:JournalName>{Escape(entry.Journal)}</b:JournalName>");
            if (!string.IsNullOrEmpty(entry.Publisher))
                sb.AppendLine($"    <b:Publisher>{Escape(entry.Publisher)}</b:Publisher>");
            if (!string.IsNullOrEmpty(entry.Volume))
                sb.AppendLine($"    <b:Volume>{Escape(entry.Volume)}</b:Volume>");
            if (!string.IsNullOrEmpty(entry.Pages))
                sb.AppendLine($"    <b:Pages>{Escape(entry.Pages)}</b:Pages>");
            if (!string.IsNullOrEmpty(entry.Url))
                sb.AppendLine($"    <b:URL>{Escape(entry.Url)}</b:URL>");
            if (!string.IsNullOrEmpty(entry.Doi))
                sb.AppendLine($"    <b:DOI>{Escape(entry.Doi)}</b:DOI>");

            sb.AppendLine("  </b:Source>");
        }

        sb.AppendLine("</b:Sources>");
        return sb.ToString();
    }

    private static string GetSourceType(string entryType)
    {
        return TypeMap.TryGetValue(entryType, out var mapped) ? mapped : "Misc";
    }

    internal static List<(string Last, string? First)> ParseAuthors(string authorField)
    {
        var result = new List<(string Last, string? First)>();

        // Split by " and " (BibTeX convention)
        var authors = authorField.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var author in authors)
        {
            var trimmed = author.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Check for "Last, First" format
            var commaIdx = trimmed.IndexOf(',');
            if (commaIdx >= 0)
            {
                var last = trimmed.Substring(0, commaIdx).Trim();
                var first = trimmed.Substring(commaIdx + 1).Trim();
                result.Add((last, string.IsNullOrEmpty(first) ? null : first));
            }
            else
            {
                // "First Last" format — last word is last name
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    result.Add((parts[0], null));
                }
                else
                {
                    var last = parts[^1];
                    var first = string.Join(" ", parts[..^1]);
                    result.Add((last, first));
                }
            }
        }

        return result;
    }

    private static string Escape(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("{", "")
            .Replace("}", "");
    }
}
