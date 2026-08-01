namespace MarkdownConverter.WebApp.Core.Services;

public interface IThemeService
{
    Task<bool> GetIsDarkModeAsync();
    Task SetDarkModeAsync(bool isDarkMode);
}
