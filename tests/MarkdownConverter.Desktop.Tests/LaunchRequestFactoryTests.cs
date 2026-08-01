using MarkdownConverter.Desktop;

namespace MarkdownConverter.Desktop.Tests;

public class LaunchRequestFactoryTests
{
    private static readonly Func<string, bool> AlwaysExists = _ => true;
    private static readonly Func<string, bool> NeverExists = _ => false;

    [Fact]
    public void FromArgs_ValidFilePath_CreatesOpenFileRequest()
    {
        var request = LaunchRequestFactory.FromArgs(new[] { @"C:\notes\a.md" }, AlwaysExists);

        Assert.Equal(LaunchRequestKinds.OpenFile, request.Kind);
        Assert.Equal(@"C:\notes\a.md", request.FilePath);
    }

    [Fact]
    public void FromArgs_NoFile_CreatesFocusRequest()
    {
        var request = LaunchRequestFactory.FromArgs(Array.Empty<string>(), AlwaysExists);

        Assert.Equal(LaunchRequestKinds.Focus, request.Kind);
        Assert.Null(request.FilePath);
    }

    [Fact]
    public void FromArgs_InvalidOrMissingFile_CreatesFocusRequest()
    {
        var request = LaunchRequestFactory.FromArgs(new[] { @"C:\notes\a.pdf" }, AlwaysExists);
        var missing = LaunchRequestFactory.FromArgs(new[] { @"C:\notes\a.md" }, NeverExists);

        Assert.Equal(LaunchRequestKinds.Focus, request.Kind);
        Assert.Equal(LaunchRequestKinds.Focus, missing.Kind);
    }

    [Fact]
    public void TryGetValidFilePath_ReusesStartupArgsExtensionPolicy()
    {
        var request = new LaunchRequest
        {
            Kind = LaunchRequestKinds.OpenFile,
            FilePath = @"C:\notes\a.markdown"
        };

        var isValid = LaunchRequestFactory.TryGetValidFilePath(
            request,
            AlwaysExists,
            out var filePath);

        Assert.True(isValid);
        Assert.Equal(@"C:\notes\a.markdown", filePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\notes\a.pdf")]
    public void TryGetValidFilePath_InvalidRequest_ReturnsFalse(string? filePath)
    {
        var request = new LaunchRequest
        {
            Kind = LaunchRequestKinds.OpenFile,
            FilePath = filePath
        };

        var isValid = LaunchRequestFactory.TryGetValidFilePath(
            request,
            AlwaysExists,
            out var result);

        Assert.False(isValid);
        Assert.Null(result);
    }
}
