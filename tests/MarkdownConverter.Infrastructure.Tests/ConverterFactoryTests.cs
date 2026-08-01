using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Infrastructure.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace MarkdownConverter.Infrastructure.Tests;

public class ConverterFactoryTests
{
    [Fact]
    public void GetConverter_ReturnsRegisteredConverter()
    {
        var services = new ServiceCollection();
        var mockConverter = new StubConverter(ExportFormat.Word);
        services.AddKeyedSingleton<IFormatConverter>(ExportFormat.Word, mockConverter);
        var sp = services.BuildServiceProvider();
        var factory = new ConverterFactory(sp);

        var result = factory.GetConverter(ExportFormat.Word);

        Assert.Same(mockConverter, result);
    }

    [Fact]
    public void GetConverter_ThrowsForUnregisteredFormat()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var factory = new ConverterFactory(sp);

        var ex = Assert.Throws<NotSupportedException>(() =>
            factory.GetConverter(ExportFormat.Pdf));

        Assert.Contains("Pdf", ex.Message);
    }

    [Fact]
    public void GetSupportedFormats_ReturnsOnlyRegisteredFormats()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IFormatConverter>(ExportFormat.Word, new StubConverter(ExportFormat.Word));
        services.AddKeyedSingleton<IFormatConverter>(ExportFormat.Latex, new StubConverter(ExportFormat.Latex));
        var sp = services.BuildServiceProvider();
        var factory = new ConverterFactory(sp);

        var formats = factory.GetSupportedFormats();

        Assert.Equal(2, formats.Count);
        Assert.Contains(ExportFormat.Word, formats);
        Assert.Contains(ExportFormat.Latex, formats);
        Assert.DoesNotContain(ExportFormat.Pdf, formats);
    }

    [Fact]
    public void GetSupportedFormats_ReturnsEmptyWhenNoneRegistered()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var factory = new ConverterFactory(sp);

        var formats = factory.GetSupportedFormats();

        Assert.Empty(formats);
    }

    private sealed class StubConverter : IFormatConverter
    {
        public StubConverter(ExportFormat format) => Format = format;
        public ExportFormat Format { get; }
        public Task<Core.Models.ConversionResult> ConvertAsync(
            Core.Models.ConversionRequest request, CancellationToken ct = default)
            => Task.FromResult(Core.Models.ConversionResult.Ok("", 0));
    }
}
