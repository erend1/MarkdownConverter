using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Interop;
using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Services;
using MarkdownConverter.Infrastructure.Converters;
using MarkdownConverter.Infrastructure.Factories;
using MarkdownConverter.Infrastructure.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarkdownConverter.WebApp.Services;

public static class WasmServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorMarkdownConverter(this IServiceCollection services)
    {
        // -- Core orchestration (reuse) --
        services.AddSingleton<IConversionService, ConversionService>();
        services.AddSingleton<IMarkdownParser, MarkdigMarkdownParser>();

        // -- WASM-specific infrastructure seams --
        services.AddSingleton<BrowserFileSystem>();
        services.AddSingleton<IFileSystem>(sp => sp.GetRequiredService<BrowserFileSystem>());
        services.AddSingleton<IProcessRunner, NullProcessRunner>();

        // -- Format converters: Word and LaTeX only (no PDF in WASM) --
        services.AddKeyedSingleton<IFormatConverter, LatexFormatConverter>(ExportFormat.Latex);
        services.AddKeyedSingleton<IFormatConverter, WordFormatConverter>(ExportFormat.Word);
        services.AddSingleton<IConverterFactory, ConverterFactory>();

        // -- Blazor-specific services --
        services.AddSingleton<IHtmlPreviewRenderer, HtmlPreviewRenderer>();
        services.AddScoped<IFileDownloadService, BrowserFileDownloadService>();
        services.AddScoped<IFileUploadService, BrowserFileUploadService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<IDebouncer, Debouncer>();
        services.AddScoped<ILocalStorageService, BrowserLocalStorageService>();
        services.AddScoped<IToastService, ToastService>();
        services.AddScoped<IErrorDetailsService, ErrorDetailsService>();
        services.AddScoped<IEditorBridge, EditorBridge>();
        services.AddScoped<BrowserDesktopCapabilityAdapter>();
        services.AddScoped<IDesktopCapabilityProvider>(provider =>
        {
            var adapter = provider.GetRequiredService<BrowserDesktopCapabilityAdapter>();
            var logger = provider.GetRequiredService<ILogger<BrowserDesktopCapabilityAdapter>>();
            return new DesktopCapabilityProvider(
                adapter.ReadAsync,
                exception => logger.LogError(
                    exception,
                    "Desktop capability detection failed; Desktop-only features are disabled."));
        });
        services.AddSingleton<FindEngine>();
        services.AddScoped<FindSession>();
        services.AddScoped<KaTeXInterop>();

        // -- Presenters (scoped = singleton lifetime in WASM) --
        services.AddScoped<IPreviewPresenter, PreviewPresenter>();
        services.AddScoped<ITabPresenter, TabPresenter>();
        services.AddScoped<IEditorPresenter, EditorPresenter>();
        services.AddScoped<IExportPresenter, ExportPresenter>();
        services.AddScoped<IBibliographyPresenter, BibliographyPresenter>();
        services.AddScoped<IFindPresenter, FindPresenter>();

        return services;
    }
}
