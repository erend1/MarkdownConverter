using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Services;

public sealed class ThemeService : IThemeService
{
    private readonly IJSRuntime _js;

    public ThemeService(IJSRuntime js) => _js = js;

    public async Task<bool> GetIsDarkModeAsync()
    {
        try
        {
            var value = await _js.InvokeAsync<string?>("localStorage.getItem", "theme");
            return value == "dark";
        }
        catch
        {
            return false;
        }
    }

    public async Task SetDarkModeAsync(bool isDarkMode)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", "theme", isDarkMode ? "dark" : "light");
    }
}
