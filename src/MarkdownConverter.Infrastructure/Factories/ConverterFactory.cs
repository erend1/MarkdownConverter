using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MarkdownConverter.Infrastructure.Factories;

public sealed class ConverterFactory : IConverterFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ConverterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IFormatConverter GetConverter(ExportFormat format)
    {
        var converter = _serviceProvider.GetKeyedService<IFormatConverter>(format);
        return converter
            ?? throw new NotSupportedException(
                $"No converter registered for format '{format}'.");
    }

    public IReadOnlyCollection<ExportFormat> GetSupportedFormats()
    {
        return Enum.GetValues<ExportFormat>()
            .Where(f => _serviceProvider.GetKeyedService<IFormatConverter>(f) is not null)
            .ToList()
            .AsReadOnly();
    }
}
