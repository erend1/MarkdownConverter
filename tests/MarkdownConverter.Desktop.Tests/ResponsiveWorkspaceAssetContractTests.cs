namespace MarkdownConverter.Desktop.Tests;

public class ResponsiveWorkspaceAssetContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void HostPage_UsesKeyboardResizingSafeAreaViewportWithoutDisablingZoom()
    {
        var index = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "wwwroot", "index.html");

        Assert.Contains("viewport-fit=cover", index);
        Assert.Contains("interactive-widget=resizes-content", index);
        Assert.DoesNotContain("user-scalable=no", index, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("maximum-scale", index, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResponsiveCss_UsesApprovedWidthAndHybridInputBands()
    {
        var css = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "wwwroot", "css", "app.css");

        Assert.Contains("@media (max-width: 899.98px)", css);
        Assert.Contains("@media (max-width: 599.98px)", css);
        Assert.Contains("@media (any-pointer: coarse)", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("height: 100dvh", css);
        Assert.Contains("env(safe-area-inset-top", css);
        Assert.Contains("font-size: 16px", css);
    }

    [Fact]
    public void WorkspaceMarkup_KeepsBothPanesAndProvidesSemanticControls()
    {
        var page = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "Pages", "EditorPage.razor");

        Assert.Equal(1, Count(page, "<MarkdownEditor />"));
        Assert.Equal(1, Count(page, "<HtmlPreview />"));
        Assert.Contains("workspace-view-switch", page);
        Assert.Contains("aria-pressed", page);
        Assert.Contains("role=\"separator\"", page);
        Assert.Contains("aria-valuemin=\"20\"", page);
        Assert.Contains("aria-valuemax=\"80\"", page);
        Assert.DoesNotContain("@if (", page[..page.IndexOf("<MarkdownEditor />", StringComparison.Ordinal)]);
    }

    [Fact]
    public void SplitterAdapter_UsesThrottledPointerEventsAndExplicitCleanup()
    {
        var bridge = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "wwwroot", "js", "dom-bridge.js");

        Assert.Contains("startSplitterDrag: function", bridge);
        Assert.Contains("setPointerCapture", bridge);
        Assert.Contains("requestAnimationFrame(flushMove)", bridge);
        Assert.Contains("addEventListener('pointermove'", bridge);
        Assert.Contains("removeEventListener('pointermove'", bridge);
        Assert.Contains("addEventListener('pointercancel'", bridge);
        Assert.Contains("removeEventListener('pointercancel'", bridge);
        Assert.Contains("cancelSplitterDrag: function", bridge);
        Assert.DoesNotContain("addEventListener('mousemove'", bridge);
    }

    [Fact]
    public void FileUploadCommands_KeepNativeInputsKeyboardReachable()
    {
        var toolbar = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "Components", "Toolbar.razor");
        var bibliography = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "Components", "BibliographyUpload.razor");

        Assert.Contains("class=\"file-input-overlay\"", toolbar);
        Assert.Contains("aria-label=\"Open Markdown file\"", toolbar);
        Assert.Contains("class=\"file-input-overlay\"", bibliography);
        Assert.Contains("aria-label=\"Upload bibliography file\"", bibliography);
        Assert.DoesNotContain("display:none", toolbar, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display:none", bibliography, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EditorPage_DisposesRetainedBrowserListeners()
    {
        var page = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "Pages", "EditorPage.razor.cs");
        var bridge = ReadRepositoryFile(
            "src", "MarkdownConverter.WebApp", "wwwroot", "js", "dom-bridge.js");

        Assert.Contains("domBridge.detachDragDrop", page);
        Assert.Contains("detachDragDrop: function", bridge);
        Assert.Contains("removeEventListener('dragover'", bridge);
        Assert.Contains("removeEventListener('drop'", bridge);
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        var start = 0;
        while ((start = value.IndexOf(search, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += search.Length;
        }

        return count;
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

        throw new DirectoryNotFoundException(
            "Could not locate the MarkdownConverter repository root.");
    }
}
