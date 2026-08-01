using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Services;

public sealed class BrowserFileDownloadService : IFileDownloadService
{
    private readonly IJSRuntime _js;

    public BrowserFileDownloadService(IJSRuntime js) => _js = js;

    public async Task<DownloadResult> DownloadAsync(string suggestedFileName, byte[] data, string mimeType)
    {
        try
        {
            var outcomeStr = await _js.InvokeAsync<string>(
                "fileInterop.saveFile", suggestedFileName, data, mimeType);

            return outcomeStr switch
            {
                "saved" => DownloadResult.Saved(),
                "cancelled" => DownloadResult.Cancelled(),
                "fallback" => DownloadResult.FellBackToDownload(),
                _ => DownloadResult.Error($"Unknown save outcome '{outcomeStr}'.")
            };
        }
        catch (Exception ex)
        {
            return DownloadResult.Error(ex.Message);
        }
    }
}
