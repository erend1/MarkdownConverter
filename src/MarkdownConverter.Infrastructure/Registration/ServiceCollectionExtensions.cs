using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Services;
using MarkdownConverter.Infrastructure.Converters;
using MarkdownConverter.Infrastructure.Factories;
using MarkdownConverter.Infrastructure.FileSystem;
using MarkdownConverter.Infrastructure.Parsing;
using MarkdownConverter.Infrastructure.Process;
using Microsoft.Extensions.DependencyInjection;

namespace MarkdownConverter.Infrastructure.Registration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMarkdownConverter(this IServiceCollection services)
    {
        // -- Core orchestration --
        services.AddSingleton<IConversionService, ConversionService>();

        // -- Parser --
        services.AddSingleton<IMarkdownParser, MarkdigMarkdownParser>();

        // -- Infrastructure seams --
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();

        // -- Strategy converters (keyed services) --
        services.AddKeyedSingleton<IFormatConverter, PdfLatexFormatConverter>(ExportFormat.Pdf);
        services.AddKeyedSingleton<IFormatConverter, LatexFormatConverter>(ExportFormat.Latex);
        services.AddKeyedSingleton<IFormatConverter, WordFormatConverter>(ExportFormat.Word);

        // -- Factory --
        services.AddSingleton<IConverterFactory, ConverterFactory>();

        return services;
    }
}
