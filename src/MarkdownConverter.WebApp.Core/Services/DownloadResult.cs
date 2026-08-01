namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Result of <see cref="IFileDownloadService.DownloadAsync"/>.
/// </summary>
public sealed class DownloadResult
{
    public DownloadOutcome Outcome { get; init; }
    public string? ErrorMessage { get; init; }

    public static DownloadResult Saved() =>
        new() { Outcome = DownloadOutcome.Saved };

    public static DownloadResult Cancelled() =>
        new() { Outcome = DownloadOutcome.Cancelled };

    public static DownloadResult FellBackToDownload() =>
        new() { Outcome = DownloadOutcome.FellBackToDownload };

    public static DownloadResult Error(string errorMessage) =>
        new() { Outcome = DownloadOutcome.Error, ErrorMessage = errorMessage };
}
