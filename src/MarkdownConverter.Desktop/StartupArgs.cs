namespace MarkdownConverter.Desktop;

/// <summary>
/// Extracts a markdown file path from the executable's command-line
/// arguments. Used to support double-clicking a `.md` file in Explorer
/// (and being set as the default opener) — Windows launches the .exe
/// with the file path as <c>args[0]</c>.
/// </summary>
public static class StartupArgs
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".txt"
    };

    /// <summary>
    /// Returns the first command-line argument when it points to an
    /// existing markdown / text file, otherwise <c>null</c>. The
    /// <paramref name="fileExists"/> seam keeps this unit-testable
    /// without touching the real file system.
    /// </summary>
    public static string? GetPendingFilePath(string[] args, Func<string, bool> fileExists)
    {
        if (args is null || args.Length == 0) return null;

        var path = args[0];
        if (string.IsNullOrWhiteSpace(path)) return null;

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return null;
        if (!AcceptedExtensions.Contains(ext)) return null;

        return fileExists(path) ? path : null;
    }

    /// <summary>
    /// Convenience overload that uses the real file system.
    /// </summary>
    public static string? GetPendingFilePath(string[] args) =>
        GetPendingFilePath(args, File.Exists);
}
