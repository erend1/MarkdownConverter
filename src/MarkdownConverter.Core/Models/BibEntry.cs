namespace MarkdownConverter.Core.Models;

public sealed class BibEntry
{
    public required string Key { get; init; }
    public required string EntryType { get; init; }
    public string? Author { get; init; }
    public string? Title { get; init; }
    public string? Year { get; init; }
    public string? Journal { get; init; }
    public string? Publisher { get; init; }
    public string? BookTitle { get; init; }
    public string? Url { get; init; }
    public string? Doi { get; init; }
    public string? Pages { get; init; }
    public string? Volume { get; init; }
    public string? Number { get; init; }
    public IDictionary<string, string> ExtraFields { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
