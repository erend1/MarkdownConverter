namespace MarkdownConverter.Desktop.Tests;

public class ReleaseWorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Changelog_DefinesFirstAlphaReleaseMetadataAndLimitations()
    {
        var changelog = ReadRepositoryFile("CHANGELOG.md");

        Assert.Contains("## [0.1.0-alpha.1] - 2026-08-11", changelog);
        Assert.Contains("first public alpha prerelease", changelog);
        Assert.Contains("Windows 10/11 x64", changelog);
        Assert.Contains("Microsoft Edge WebView2 Runtime", changelog);
        Assert.Contains("### Known limitations", changelog);
        Assert.Contains("unsigned", changelog);
        Assert.Contains("local LaTeX installation", changelog);
    }

    [Fact]
    public void Workflow_RequiresCuratedNotesAndAppendsExactBuildProvenance()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "release.yml");

        Assert.Contains("throw \"CHANGELOG.md does not contain a curated section", workflow);
        Assert.DoesNotContain("See commit history - no curated changelog entry", workflow);
        Assert.Contains("$sourceSha = \"${{ github.sha }}\"", workflow);
        Assert.Contains("${{ github.server_url }}/${{ github.repository }}/commit/$sourceSha", workflow);
        Assert.Contains("- Release type: $releaseType", workflow);
        Assert.Contains("- Supported artifact: self-contained Windows x64 Desktop archive.", workflow);
        Assert.Contains("- Source tag: ${{ steps.parse.outputs.tag }}", workflow);
        Assert.Contains("- Source commit: [$sourceSha]($sourceUrl)", workflow);
    }

    [Fact]
    public void Workflow_PackagesAndValidatesTheExpectedPrereleaseArtifacts()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "release.yml");

        Assert.Contains("tags: ['v*']", workflow);
        Assert.Contains("--runtime win-x64 --self-contained true", workflow);
        Assert.Contains("/p:PublishSingleFile=true", workflow);
        Assert.Contains("publish/desktop/MarkdownConverter.exe", workflow);
        Assert.Contains("publish/desktop/app.ico", workflow);
        Assert.Contains("publish/desktop/LICENSE.txt", workflow);
        Assert.Contains("publish/desktop/THIRD-PARTY-NOTICES.md", workflow);
        Assert.Contains("Get-FileHash $env:zip -Algorithm SHA256", workflow);
        Assert.Contains("$ghArgs += \"--prerelease\"", workflow);
    }

    [Fact]
    public void DesktopProject_PublishesRuntimeIconSidecar()
    {
        var project = ReadRepositoryFile(
            "src",
            "MarkdownConverter.Desktop",
            "MarkdownConverter.Desktop.csproj");

        Assert.Contains("<ApplicationIcon>app.ico</ApplicationIcon>", project);
        Assert.Contains("CopyToOutputDirectory=\"PreserveNewest\"", project);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", project);
        Assert.Contains("ExcludeFromSingleFile=\"true\"", project);
    }

    [Fact]
    public void PortableAssociationGuidance_RequiresExactExecutableAndQuotedFileArgument()
    {
        var releasing = ReadRepositoryFile(".github", "RELEASING.md");
        var readme = ReadRepositoryFile("README.md");
        var program = ReadRepositoryFile("src", "MarkdownConverter.Desktop", "Program.cs");

        Assert.Contains("\"C:\\path\\to\\the\\extracted\\MarkdownConverter.exe\" \"%1\"", releasing);
        Assert.Contains("\"C:\\path\\to\\MarkdownConverter.exe\" \"%1\"", readme);
        Assert.Contains("does not install itself or automatically create or update Windows file associations", releasing);
        Assert.Contains("Settings → Apps → Default apps", releasing);
        Assert.Contains("FocusExistingWindow();", program);
        Assert.Contains("SetForegroundWindow(Handle);", program);
        Assert.DoesNotContain("Microsoft.Win32", program);
        Assert.DoesNotContain("Registry.", program);
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
