namespace MarkdownConverter.WebApp.Core.Services;

public interface IFileDownloadService
{
    /// <summary>
    /// Prompts the user for a save location and writes <paramref name="data"/>
    /// there. Falls back to a legacy &lt;a download&gt; trigger if the host
    /// browser does not support the File System Access API.
    /// </summary>
    /// <param name="suggestedFileName">Name pre-filled in the save dialog.</param>
    /// <param name="data">File contents.</param>
    /// <param name="mimeType">MIME type used in the dialog filter and Blob.</param>
    Task<DownloadResult> DownloadAsync(string suggestedFileName, byte[] data, string mimeType);
}
