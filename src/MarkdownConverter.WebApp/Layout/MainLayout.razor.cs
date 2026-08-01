using Microsoft.AspNetCore.Components;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Layout;

public partial class MainLayout
{
    [Inject] private IThemeService ThemeService { get; set; } = default!;

    private bool _isDark;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isDark = await ThemeService.GetIsDarkModeAsync();
            StateHasChanged();
        }
    }

    private async Task ToggleTheme()
    {
        _isDark = !_isDark;
        await ThemeService.SetDarkModeAsync(_isDark);
    }
}
