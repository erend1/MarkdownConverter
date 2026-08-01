using MarkdownConverter.Core.Enums;

namespace MarkdownConverter.Core.Models;

public sealed class ConversionRequest
{
    public required MarkdownDocument Document { get; init; }
    public required ExportFormat TargetFormat { get; init; }
    public required string OutputFilePath { get; init; }

    /// <summary>
    /// Optional key-value bag for format-specific options
    /// (e.g., PDF page size, Word template path).
    /// </summary>
    public IDictionary<string, string> Options { get; init; }
        = new Dictionary<string, string>();
}
