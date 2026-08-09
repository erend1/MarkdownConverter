using System.Text.Json;

namespace MarkdownConverter.Desktop.Tests;

public class PwaAssetContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ProductionHostPage_RegistersBaseRelativeWorkerWithoutHttpCaching()
    {
        var index = ReadRepositoryFile("src", "MarkdownConverter.WebApp", "wwwroot", "index.html");

        Assert.Contains(
            "navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });",
            index);
        Assert.DoesNotContain("navigator.serviceWorker.register('/service-worker.js'", index);
    }

    [Fact]
    public void Manifest_DefinesRepositoryRelativeInstallScopeAndRequiredIcons()
    {
        var manifest = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "manifest.webmanifest");
        using var document = JsonDocument.Parse(manifest);
        var root = document.RootElement;

        Assert.Equal("./", root.GetProperty("start_url").GetString());
        Assert.Equal("./", root.GetProperty("scope").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());

        var icons = root.GetProperty("icons").EnumerateArray().ToArray();
        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "icon-192.png"
            && icon.GetProperty("sizes").GetString() == "192x192"
            && icon.GetProperty("type").GetString() == "image/png");
        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "icon-512.png"
            && icon.GetProperty("sizes").GetString() == "512x512"
            && icon.GetProperty("type").GetString() == "image/png");
    }

    [Fact]
    public void PublishedWorker_UsesScopedShellFallbackAndVersionedCacheCleanup()
    {
        var worker = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "service-worker.published.js");

        Assert.Contains("/\\.webmanifest$/", worker);
        Assert.Contains("const scopeUrl = new URL(self.registration.scope);", worker);
        Assert.Contains("new URL(asset.url, scopeUrl)", worker);
        Assert.Contains("event.request.mode === 'navigate'", worker);
        Assert.Contains("new Request(new URL('index.html', scopeUrl))", worker);
        Assert.Contains("key.startsWith(cacheNamePrefix) && key !== cacheName", worker);
        Assert.DoesNotContain("skipWaiting", worker);
    }

    [Fact]
    public void DevelopmentAndDesktopWorkers_RemainNonCaching()
    {
        var developmentWorker = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "service-worker.js");
        var desktopHost = ReadRepositoryFile("src", "MarkdownConverter.Desktop", "Program.cs");

        Assert.DoesNotContain("addEventListener('fetch'", developmentWorker);
        Assert.DoesNotContain("caches.", developmentWorker);
        Assert.Contains("if (requestPath == \"service-worker.js\")", desktopHost);
        Assert.Contains("self.addEventListener('install'", desktopHost);
        Assert.DoesNotContain("caches.", desktopHost);
    }

    [Fact]
    public void PagesPreparation_RehashesTheTransformedAppShell()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "ci.yml");

        Assert.Contains("$assetManifestPath = Join-Path $siteRoot 'service-worker-assets.js'", workflow);
        Assert.Contains("$index = $index.Replace($rootBaseTag", workflow);
        Assert.Contains("$sha256.ComputeHash([IO.File]::ReadAllBytes($indexPath))", workflow);
        Assert.Contains("$indexAssetMatches.Count -ne 1", workflow);
        Assert.Contains("Set-Content -LiteralPath $assetManifestPath", workflow);
    }

    [Fact]
    public void PagesUpload_UsesOfficialNode24CompatibleAction()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "ci.yml");

        Assert.Contains("uses: actions/upload-pages-artifact@v5", workflow);
        Assert.DoesNotContain("uses: actions/upload-pages-artifact@v4", workflow);
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
