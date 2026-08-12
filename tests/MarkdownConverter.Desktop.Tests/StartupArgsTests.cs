using MarkdownConverter.Desktop;

namespace MarkdownConverter.Desktop.Tests;

public class StartupArgsTests
{
    private static readonly Func<string, bool> AlwaysExists = _ => true;
    private static readonly Func<string, bool> NeverExists = _ => false;

    [Fact]
    public void NoArgs_ReturnsNull()
    {
        Assert.Null(StartupArgs.GetPendingFilePath(Array.Empty<string>(), AlwaysExists));
    }

    [Fact]
    public void NullArgs_ReturnsNull()
    {
        Assert.Null(StartupArgs.GetPendingFilePath(null!, AlwaysExists));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void EmptyOrWhitespaceArg_ReturnsNull(string arg)
    {
        Assert.Null(StartupArgs.GetPendingFilePath(new[] { arg }, AlwaysExists));
    }

    [Theory]
    [InlineData(@"C:\notes\readme.md")]
    [InlineData(@"C:\notes\readme.markdown")]
    [InlineData(@"C:\notes\readme.txt")]
    [InlineData(@"C:\notes\README.MD")]      // Mixed-case extension still accepted.
    [InlineData(@"C:\notes\readme.MarkDown")]
    public void AcceptedExtension_AndFileExists_ReturnsPath(string path)
    {
        var result = StartupArgs.GetPendingFilePath(new[] { path }, AlwaysExists);

        Assert.Equal(path, result);
    }

    [Theory]
    [InlineData(@"C:\notes\paper.pdf")]
    [InlineData(@"C:\notes\photo.png")]
    [InlineData(@"C:\notes\archive.zip")]
    public void RejectedExtension_ReturnsNullEvenIfFileExists(string path)
    {
        Assert.Null(StartupArgs.GetPendingFilePath(new[] { path }, AlwaysExists));
    }

    [Fact]
    public void NoExtension_ReturnsNull()
    {
        Assert.Null(StartupArgs.GetPendingFilePath(new[] { @"C:\notes\readme" }, AlwaysExists));
    }

    [Fact]
    public void AcceptedExtension_ButFileMissing_ReturnsNull()
    {
        Assert.Null(StartupArgs.GetPendingFilePath(new[] { @"C:\notes\readme.md" }, NeverExists));
    }

    [Fact]
    public void OnlyFirstArgIsConsidered()
    {
        // Multiple files passed (e.g. a future "open many" use case) — for now
        // we open just the first one and ignore the rest.
        var args = new[] { @"C:\a.md", @"C:\b.md", @"C:\c.md" };

        Assert.Equal(@"C:\a.md", StartupArgs.GetPendingFilePath(args, AlwaysExists));
    }

    [Fact]
    public void PathWithSpaces_AcceptedWhenFileExists()
    {
        var path = @"C:\My Documents\some file.md";
        Assert.Equal(path, StartupArgs.GetPendingFilePath(new[] { path }, AlwaysExists));
    }

    [Fact]
    public void UnicodePathWithSpaces_IsPreservedExactly()
    {
        var path = @"C:\Kullanıcılar\Hüseyin\Çalışma Notları\ölçüm özeti.md";

        Assert.Equal(path, StartupArgs.GetPendingFilePath(new[] { path }, AlwaysExists));
    }

    [Fact]
    public void OverloadWithoutFileExistsSeam_UsesRealFileSystem()
    {
        // Unique non-existent path — should return null without throwing.
        var bogus = Path.Combine(Path.GetTempPath(), "definitely-not-here-" + Guid.NewGuid().ToString("N") + ".md");

        Assert.Null(StartupArgs.GetPendingFilePath(new[] { bogus }));
    }
}
