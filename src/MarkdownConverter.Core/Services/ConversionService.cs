using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Services;

/// <summary>
/// The single orchestration class in Core. Depends only on interfaces,
/// making it fully testable and UI-agnostic.
/// </summary>
public sealed class ConversionService : IConversionService
{
    private readonly IMarkdownParser _parser;
    private readonly IConverterFactory _factory;

    public ConversionService(IMarkdownParser parser, IConverterFactory factory)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<ConversionResult> ConvertAsync(
        string rawMarkdown,
        ExportFormat targetFormat,
        string outputFilePath,
        IDictionary<string, string>? options = null,
        CancellationToken cancellationToken = default)
    {
        var document = _parser.Parse(rawMarkdown);
        var converter = _factory.GetConverter(targetFormat);

        var request = new ConversionRequest
        {
            Document = document,
            TargetFormat = targetFormat,
            OutputFilePath = outputFilePath,
            Options = options ?? new Dictionary<string, string>()
        };

        return await converter.ConvertAsync(request, cancellationToken);
    }
}
