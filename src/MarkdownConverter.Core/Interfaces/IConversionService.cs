using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Interfaces;

/// <summary>
/// High-level orchestration service. All UIs (Console, WinForms, Blazor)
/// depend only on this interface for end-to-end conversion.
/// </summary>
public interface IConversionService
{
    Task<ConversionResult> ConvertAsync(
        string rawMarkdown,
        ExportFormat targetFormat,
        string outputFilePath,
        IDictionary<string, string>? options = null,
        CancellationToken cancellationToken = default);
}
