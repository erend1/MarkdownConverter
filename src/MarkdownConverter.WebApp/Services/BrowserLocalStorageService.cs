using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Services;

public sealed class BrowserLocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "mdconverter_tabs";
    private const string BackupKey = "mdconverter_tabs.bak";

    public BrowserLocalStorageService(IJSRuntime js) => _js = js;

    public async Task<SaveResult> SaveTabsAsync(IReadOnlyList<TabState> tabs, int activeIndex)
    {
        try
        {
            var json = LocalStorageSerializer.Serialize(tabs, activeIndex);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Error(ex.Message);
        }
    }

    public async Task<LoadResult> LoadTabsAsync()
    {
        string? json;
        try
        {
            json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (Exception ex)
        {
            return LoadResult.Corrupted($"Could not read storage: {ex.Message}");
        }

        var result = LocalStorageSerializer.Deserialize(json);

        // If the stored payload is corrupt, move it aside so the next save
        // does not overwrite it — the user can recover it manually if needed.
        if (result.Status == LoadStatus.Corrupted && !string.IsNullOrEmpty(json))
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", BackupKey, json);
                await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
            catch
            {
                // Best-effort backup; if it fails the original error is what matters.
            }
        }

        return result;
    }
}
