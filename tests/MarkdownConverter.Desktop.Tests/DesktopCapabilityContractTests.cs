namespace MarkdownConverter.Desktop.Tests;

public class DesktopCapabilityContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void DesktopHost_DeclaresBothCapabilitiesBeforeNavigation()
    {
        var program = ReadRepositoryFile("src", "MarkdownConverter.Desktop", "Program.cs");
        var declarationIndex = program.IndexOf(
            "AddScriptToExecuteOnDocumentCreatedAsync",
            StringComparison.Ordinal);
        var navigationIndex = program.IndexOf("CoreWebView2.Navigate", StringComparison.Ordinal);

        Assert.True(declarationIndex >= 0, "Desktop capability declaration is missing.");
        Assert.True(
            declarationIndex < navigationIndex,
            "Desktop capabilities must be installed before the first application navigation.");
        Assert.Contains("canCompilePdf: true", program);
        Assert.Contains("canReceivePendingFiles: true", program);
        Assert.Contains("Object.freeze", program);
        Assert.Contains("requestPath == \"api/desktop-status\"", program);
    }

    [Fact]
    public void BrowserAdapter_ReadsOnlyTheExplicitMarker()
    {
        var interop = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "js",
            "file-interop.js");

        Assert.Contains("window.desktopCapabilities", interop);
        Assert.Contains("window.__markdownConverterDesktopCapabilities", interop);
        Assert.DoesNotContain("/api/desktop-status", interop);
        Assert.DoesNotContain("isDesktopMode", interop);
        Assert.DoesNotContain("navigator.userAgent", interop);
        Assert.DoesNotContain("window.chrome", interop);
    }

    [Fact]
    public void WebConsumers_UseTheirCorrespondingTypedCapability()
    {
        var exportPanel = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "Components",
            "ExportPanel.razor.cs");
        var exportMarkup = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "Components",
            "ExportPanel.razor");
        var editorPage = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "Pages",
            "EditorPage.razor.cs");

        Assert.Contains("IDesktopCapabilityProvider", exportPanel);
        Assert.Contains("capabilities.CanCompilePdf", exportPanel);
        Assert.DoesNotContain("CanReceivePendingFiles", exportPanel);
        Assert.Contains("@if (_canCompilePdf)", exportMarkup);

        Assert.Contains("IDesktopCapabilityProvider", editorPage);
        Assert.Contains("capabilities.CanReceivePendingFiles", editorPage);
        Assert.DoesNotContain("CanCompilePdf", editorPage);

        Assert.DoesNotContain("fileInterop.isDesktopMode", exportPanel);
        Assert.DoesNotContain("fileInterop.isDesktopMode", editorPage);
    }

    [Fact]
    public void CompositionRoot_RegistersOneScopedCachedProvider()
    {
        var compositionRoot = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "Services",
            "WasmServiceCollectionExtensions.cs");

        Assert.Contains("services.AddScoped<BrowserDesktopCapabilityAdapter>();", compositionRoot);
        Assert.Contains("services.AddScoped<IDesktopCapabilityProvider>", compositionRoot);
        Assert.Contains("new DesktopCapabilityProvider(", compositionRoot);
    }

    private static string ReadRepositoryFile(params string[] pathSegments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathSegments]));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MarkdownConverter.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the MarkdownConverter repository root.");
    }
}
