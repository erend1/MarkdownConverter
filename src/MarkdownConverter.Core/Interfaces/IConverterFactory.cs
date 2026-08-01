using MarkdownConverter.Core.Enums;

namespace MarkdownConverter.Core.Interfaces;

/// <summary>
/// Resolves the correct <see cref="IFormatConverter"/> for a given <see cref="ExportFormat"/>.
/// </summary>
public interface IConverterFactory
{
    /// <exception cref="NotSupportedException">
    /// Thrown when no converter is registered for the given format.
    /// </exception>
    IFormatConverter GetConverter(ExportFormat format);

    IReadOnlyCollection<ExportFormat> GetSupportedFormats();
}
