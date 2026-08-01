namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Outcome of an <see cref="IFileDownloadService"/> save attempt.
/// </summary>
public enum DownloadOutcome
{
    /// <summary>
    /// User picked a destination via the OS save dialog and the file was written.
    /// </summary>
    Saved,

    /// <summary>
    /// User dismissed the save dialog. No file was written.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Browser does not support the File System Access API; the file was
    /// delivered via a legacy &lt;a download&gt; trigger (typically into the
    /// Downloads folder).
    /// </summary>
    FellBackToDownload,

    /// <summary>
    /// An unexpected failure occurred. <see cref="DownloadResult.ErrorMessage"/>
    /// describes it.
    /// </summary>
    Error
}
