using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Infrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace MarkdownConverter.Infrastructure.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMarkdownConverter_RegistersConversionService()
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();

        var service = sp.GetService<IConversionService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddMarkdownConverter_RegistersMarkdownParser()
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();

        var parser = sp.GetService<IMarkdownParser>();

        Assert.NotNull(parser);
    }

    [Fact]
    public void AddMarkdownConverter_RegistersFileSystem()
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();

        var fs = sp.GetService<IFileSystem>();

        Assert.NotNull(fs);
    }

    [Fact]
    public void AddMarkdownConverter_RegistersProcessRunner()
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();

        var runner = sp.GetService<IProcessRunner>();

        Assert.NotNull(runner);
    }

    [Fact]
    public void AddMarkdownConverter_RegistersConverterFactory()
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();

        var factory = sp.GetService<IConverterFactory>();

        Assert.NotNull(factory);
    }

    [Theory]
    [InlineData(ExportFormat.Word)]
    [InlineData(ExportFormat.Pdf)]
    [InlineData(ExportFormat.Latex)]
    public void AddMarkdownConverter_RegistersAllFormatConverters(ExportFormat format)
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();

        var converter = sp.GetKeyedService<IFormatConverter>(format);

        Assert.NotNull(converter);
        Assert.Equal(format, converter.Format);
    }

    [Fact]
    public void AddMarkdownConverter_FactoryResolvesAllFormats()
    {
        var services = new ServiceCollection();
        services.AddMarkdownConverter();
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IConverterFactory>();

        var formats = factory.GetSupportedFormats();

        Assert.Contains(ExportFormat.Word, formats);
        Assert.Contains(ExportFormat.Pdf, formats);
        Assert.Contains(ExportFormat.Latex, formats);
    }

    [Fact]
    public void AddMarkdownConverter_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddMarkdownConverter();

        Assert.Same(services, result);
    }
}
