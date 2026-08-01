using System.Diagnostics.CodeAnalysis;

namespace MarkdownConverter.Desktop;

public static class LaunchRequestKinds
{
    public const string Focus = "focus";
    public const string OpenFile = "openFile";
}

public sealed class LaunchRequest
{
    public string Kind { get; init; } = LaunchRequestKinds.Focus;
    public string? FilePath { get; init; }
}

public static class LaunchRequestFactory
{
    public static LaunchRequest FromArgs(string[] args, Func<string, bool> fileExists)
    {
        var pendingFilePath = StartupArgs.GetPendingFilePath(args, fileExists);
        return FromPendingFilePath(pendingFilePath);
    }

    public static LaunchRequest FromPendingFilePath(string? pendingFilePath) =>
        string.IsNullOrWhiteSpace(pendingFilePath)
            ? new LaunchRequest()
            : new LaunchRequest
            {
                Kind = LaunchRequestKinds.OpenFile,
                FilePath = pendingFilePath
            };

    public static bool TryGetValidFilePath(
        LaunchRequest? request,
        Func<string, bool> fileExists,
        [NotNullWhen(true)] out string? filePath)
    {
        filePath = null;
        if (request?.Kind != LaunchRequestKinds.OpenFile) return false;
        if (string.IsNullOrWhiteSpace(request.FilePath)) return false;

        filePath = StartupArgs.GetPendingFilePath(new[] { request.FilePath }, fileExists);
        return filePath is not null;
    }
}
