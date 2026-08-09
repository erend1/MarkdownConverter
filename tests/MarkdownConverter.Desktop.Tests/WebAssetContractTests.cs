using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MarkdownConverter.Desktop.Tests;

public class WebAssetContractTests
{
    private const long RuntimeByteBudget = 1_048_576;

    private const string KatexTransformation =
        "Derived katex.min.css from dist/katex.min.css by removing the WOFF and TTF fallback sources from each font face; all WOFF2 sources and remaining minified CSS are retained.";

    private const string LucideTransformation =
        "Derived lucide.css from font/lucide.css by retaining the WOFF2 font face and mappings for exactly file-plus, folder-open, save, download, pencil, sun, and moon, renamed to the lucide-* classes already emitted by the application.";

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly string VendorRoot = Path.Combine(
        RepositoryRoot,
        "src",
        "MarkdownConverter.WebApp",
        "wwwroot",
        "vendor");

    private static readonly string[] ExpectedIcons =
    [
        "download",
        "file-plus",
        "folder-open",
        "moon",
        "pencil",
        "save",
        "sun"
    ];

    private static readonly string[] ExpectedKatexFiles =
    [
        "katex/0.16.47/LICENSE",
        "katex/0.16.47/katex.min.css",
        "katex/0.16.47/katex.min.js",
        "katex/0.16.47/fonts/KaTeX_AMS-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Caligraphic-Bold.woff2",
        "katex/0.16.47/fonts/KaTeX_Caligraphic-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Fraktur-Bold.woff2",
        "katex/0.16.47/fonts/KaTeX_Fraktur-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Main-Bold.woff2",
        "katex/0.16.47/fonts/KaTeX_Main-BoldItalic.woff2",
        "katex/0.16.47/fonts/KaTeX_Main-Italic.woff2",
        "katex/0.16.47/fonts/KaTeX_Main-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Math-BoldItalic.woff2",
        "katex/0.16.47/fonts/KaTeX_Math-Italic.woff2",
        "katex/0.16.47/fonts/KaTeX_SansSerif-Bold.woff2",
        "katex/0.16.47/fonts/KaTeX_SansSerif-Italic.woff2",
        "katex/0.16.47/fonts/KaTeX_SansSerif-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Script-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Size1-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Size2-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Size3-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Size4-Regular.woff2",
        "katex/0.16.47/fonts/KaTeX_Typewriter-Regular.woff2"
    ];

    private static readonly string[] ExpectedLucideFiles =
    [
        "lucide/0.460.0/LICENSE",
        "lucide/0.460.0/lucide.css",
        "lucide/0.460.0/lucide.woff2"
    ];

    [Fact]
    public void HostPage_UsesOnlyBaseRelativeLocalRuntimeAssets()
    {
        var index = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "index.html");

        Assert.Contains("href=\"vendor/katex/0.16.47/katex.min.css\"", index);
        Assert.Contains("href=\"vendor/lucide/0.460.0/lucide.css\"", index);
        Assert.Contains("src=\"vendor/katex/0.16.47/katex.min.js\"", index);
        Assert.DoesNotContain("0.16.9", index);
        Assert.DoesNotMatch(
            new Regex(
                @"<(?:script|link)\b[^>]*(?:src|href)\s*=\s*[""']https?://",
                RegexOptions.IgnoreCase),
            index);
        Assert.DoesNotContain("href=\"/vendor/", index);
        Assert.DoesNotContain("src=\"/vendor/", index);
    }

    [Fact]
    public void Inventory_RecordsExactlyTheApprovedPackagesAndFiles()
    {
        using var inventory = ReadInventory();
        var root = inventory.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        var packages = root.GetProperty("packages").EnumerateArray().ToArray();
        Assert.Equal(["katex", "lucide-static"], packages.Select(GetPackageName).ToArray());

        AssertPackage(
            packages[0],
            "0.16.47",
            "https://registry.npmjs.org/katex/-/katex-0.16.47.tgz",
            "https://github.com/KaTeX/KaTeX/tree/v0.16.47",
            "878a61be7743a8ec4ee725b0b5efa810b5167c79",
            "sha512-Eeo8Ys1doU1z+x8AZsPpQu+p/QcZBI5PeOo7QGQdy2x2m0MU/hYagBbGOmXwr5KVbEfVuWv9LpnQWeehogurjg==",
            "MIT",
            KatexTransformation,
            ExpectedKatexFiles);

        AssertPackage(
            packages[1],
            "0.460.0",
            "https://registry.npmjs.org/lucide-static/-/lucide-static-0.460.0.tgz",
            "https://github.com/lucide-icons/lucide/tree/0.460.0",
            "4d91fbb588f2864941dcfb12288a8b6291afa503",
            "sha512-X6pIdg7jVxv7YQ/uR241hwhNiztcAfmj181TbcX7HCxxk/3mGaRtAc6b2ftUvQBufbJE6ehgyzO2uVsa604tWg==",
            "ISC",
            LucideTransformation,
            ExpectedLucideFiles);

        Assert.Equal(
            ["katex", "lucide"],
            Directory.GetDirectories(VendorRoot)
                .Select(path => Path.GetFileName(path)!)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(["0.16.47"], GetVersionDirectories("katex"));
        Assert.Equal(["0.460.0"], GetVersionDirectories("lucide"));
    }

    [Fact]
    public void InventoryHashes_MatchEveryFileWithNoUntrackedVendoredFile()
    {
        using var inventory = ReadInventory();
        var inventoryFiles = EnumerateInventoryFiles(inventory.RootElement).ToArray();

        foreach (var entry in inventoryFiles)
        {
            var relativePath = entry.GetProperty("path").GetString()!;
            var fullPath = ResolveInside(VendorRoot, relativePath);

            Assert.True(File.Exists(fullPath), $"Missing inventoried file: {relativePath}");
            Assert.Equal(entry.GetProperty("bytes").GetInt64(), new FileInfo(fullPath).Length);

            var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath)))
                .ToLowerInvariant();
            Assert.Equal(entry.GetProperty("sha256").GetString(), actualHash);
        }

        var actualFiles = new[]
            {
                Path.Combine(VendorRoot, "katex", "0.16.47"),
                Path.Combine(VendorRoot, "lucide", "0.460.0")
            }
            .SelectMany(Directory.EnumerateFiles)
            .Concat(Directory.EnumerateFiles(Path.Combine(VendorRoot, "katex", "0.16.47", "fonts")))
            .Select(ToVendorRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var recordedFiles = inventoryFiles
            .Select(entry => entry.GetProperty("path").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(recordedFiles, actualFiles);
        Assert.DoesNotContain(actualFiles, path =>
            path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".eot", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VendoredCss_ReferencesOnlyInventoriedFilesInsideItsPackageRoot()
    {
        using var inventory = ReadInventory();
        var entries = EnumerateInventoryFiles(inventory.RootElement).ToArray();
        var inventoriedPaths = entries
            .Select(entry => entry.GetProperty("path").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var referencedPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries.Where(entry =>
                     entry.GetProperty("path").GetString()!.EndsWith(".css", StringComparison.Ordinal)))
        {
            var cssPath = entry.GetProperty("path").GetString()!;
            var cssFullPath = ResolveInside(VendorRoot, cssPath);
            var packageParts = cssPath.Split('/');
            var packageRoot = Path.Combine(VendorRoot, packageParts[0], packageParts[1]);
            var css = File.ReadAllText(cssFullPath);
            var matches = Regex.Matches(
                css,
                @"url\(\s*[""']?(?<url>[^""')]+)[""']?\s*\)",
                RegexOptions.IgnoreCase);

            Assert.NotEmpty(matches);
            foreach (Match match in matches)
            {
                var url = match.Groups["url"].Value;
                Assert.False(Uri.TryCreate(url, UriKind.Absolute, out _), $"Absolute CSS URL: {url}");
                Assert.False(url.StartsWith("/", StringComparison.Ordinal), $"Rooted CSS URL: {url}");
                Assert.DoesNotContain('?', url);
                Assert.DoesNotContain('#', url);

                var resolvedPath = ResolveInside(packageRoot, Path.Combine(
                    Path.GetRelativePath(packageRoot, Path.GetDirectoryName(cssFullPath)!),
                    url.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(resolvedPath), $"Missing CSS dependency: {url}");

                var relativePath = ToVendorRelativePath(resolvedPath);
                Assert.Contains(relativePath, inventoriedPaths);
                referencedPaths.Add(relativePath);
            }
        }

        var inventoriedFonts = inventoriedPaths
            .Where(path => path.EndsWith(".woff2", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(inventoriedFonts, referencedPaths.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void LucideStylesheet_MapsExactlyTheSevenApplicationIcons()
    {
        var css = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "vendor",
            "lucide",
            "0.460.0",
            "lucide.css");
        var mappings = Regex.Matches(
            css,
            @"\.lucide-(?<name>[a-z0-9-]+)::before\s*\{\s*content:\s*[""'](?<value>\\[0-9a-f]+)[""']\s*;",
            RegexOptions.IgnoreCase);

        Assert.Equal(7, mappings.Count);
        Assert.Equal(
            ExpectedIcons,
            mappings.Select(match => match.Groups["name"].Value)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.All(mappings, match =>
            Assert.Matches(@"^\\[0-9a-f]{4,}$", match.Groups["value"].Value));
        Assert.Equal(
            7,
            mappings.Select(match => match.Groups["value"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
    }

    [Fact]
    public void KatexAdapter_UsesExplicitUntrustedInputLimits()
    {
        var interop = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "js",
            "katex-interop.js");

        Assert.Equal(2, Regex.Matches(interop, @"throwOnError\s*:\s*false").Count);
        Assert.Equal(2, Regex.Matches(interop, @"trust\s*:\s*false").Count);
        var expansionLimits = Regex.Matches(interop, @"maxExpand\s*:\s*(?<value>\d+)");
        Assert.Equal(2, expansionLimits.Count);
        Assert.All(expansionLimits, match =>
            Assert.InRange(int.Parse(match.Groups["value"].Value), 1, 10_000));
    }

    [Fact]
    public void PublishedWorker_PrecachesWoff2WithoutCrossOriginRuntimeCaching()
    {
        var worker = ReadRepositoryFile(
            "src",
            "MarkdownConverter.WebApp",
            "wwwroot",
            "service-worker.published.js");

        Assert.Contains("/\\.woff2$/", worker);
        Assert.Contains("new URL(asset.url, scopeUrl)", worker);
        Assert.DoesNotContain("http://", worker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", worker, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VendoredRuntime_RemainsWithinApprovedByteBudget()
    {
        using var inventory = ReadInventory();
        var runtimeBytes = EnumerateInventoryFiles(inventory.RootElement)
            .Where(entry => entry.GetProperty("runtime").GetBoolean())
            .Sum(entry => entry.GetProperty("bytes").GetInt64());

        Assert.InRange(runtimeBytes, 1, RuntimeByteBudget);
    }

    [Fact]
    public void Notices_RecordLocalDistributionAndApprovedLicenses()
    {
        var notices = ReadRepositoryFile("THIRD-PARTY-NOTICES.md");

        Assert.Contains("## KaTeX 0.16.47", notices);
        Assert.Contains("## Lucide Static 0.460.0", notices);
        Assert.Contains("KaTeX is distributed with the WebApp as a reviewed local runtime subset", notices);
        Assert.Contains("Lucide Static is distributed with the WebApp as a reviewed local runtime subset", notices);
        Assert.Contains("KaTeX is distributed under the MIT License", notices);
        Assert.Contains("distributed under the ISC License", notices);
        Assert.DoesNotContain("loaded by the WebApp from a versioned CDN", notices);
    }

    private static void AssertPackage(
        JsonElement package,
        string version,
        string sourceUrl,
        string sourceTag,
        string sourceCommit,
        string npmIntegrity,
        string license,
        string transformation,
        string[] expectedFiles)
    {
        Assert.Equal(version, package.GetProperty("version").GetString());
        Assert.Equal(sourceUrl, package.GetProperty("sourceUrl").GetString());
        Assert.Equal(sourceTag, package.GetProperty("sourceTag").GetString());
        Assert.Equal(sourceCommit, package.GetProperty("sourceCommit").GetString());
        Assert.Equal(npmIntegrity, package.GetProperty("npmIntegrity").GetString());
        Assert.Equal(license, package.GetProperty("license").GetString());

        var transformations = package.GetProperty("transformationNotes").EnumerateArray().ToArray();
        Assert.Single(transformations);
        Assert.Equal(transformation, transformations[0].GetString());

        var actualFiles = package.GetProperty("files").EnumerateArray()
            .Select(entry => entry.GetProperty("path").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedFiles.Order(StringComparer.Ordinal).ToArray(), actualFiles);
    }

    private static IEnumerable<JsonElement> EnumerateInventoryFiles(JsonElement root) =>
        root.GetProperty("packages").EnumerateArray()
            .SelectMany(package => package.GetProperty("files").EnumerateArray());

    private static JsonDocument ReadInventory() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(VendorRoot, "asset-inventory.json")));

    private static string GetPackageName(JsonElement package) =>
        package.GetProperty("name").GetString()!;

    private static string[] GetVersionDirectories(string packageName) =>
        Directory.GetDirectories(Path.Combine(VendorRoot, packageName))
            .Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string ResolveInside(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(
            fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase),
            $"Path escapes its vendor root: {relativePath}");
        return fullPath;
    }

    private static string ToVendorRelativePath(string path) =>
        Path.GetRelativePath(VendorRoot, path).Replace(Path.DirectorySeparatorChar, '/');

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
