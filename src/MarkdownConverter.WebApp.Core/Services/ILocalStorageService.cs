namespace MarkdownConverter.WebApp.Core.Services;

public interface ILocalStorageService
{
    Task<SaveResult> SaveTabsAsync(IReadOnlyList<TabState> tabs, int activeIndex);
    Task<LoadResult> LoadTabsAsync();
}
