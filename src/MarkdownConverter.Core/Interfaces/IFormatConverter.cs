using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Interfaces;

/// <summary>
/// Strategy interface. Each export format provides exactly one
/// implementation. Implementations live in Infrastructure, never in Core.
/// </summary>
public interface IFormatConverter
{
    ExportFormat Format { get; }
    Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default);
}
