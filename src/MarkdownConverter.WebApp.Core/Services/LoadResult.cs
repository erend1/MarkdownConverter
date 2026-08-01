namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Result of <see cref="ILocalStorageService.LoadTabsAsync"/>.
/// </summary>
public sealed class LoadResult
{
    public LoadStatus Status { get; init; }
    public IReadOnlyList<TabState> Tabs { get; init; } = Array.Empty<TabState>();
    public int ActiveIndex { get; init; }
    public string? ErrorMessage { get; init; }

    public static LoadResult Empty() => new() { Status = LoadStatus.Empty };

    public static LoadResult Loaded(IReadOnlyList<TabState> tabs, int activeIndex) =>
        new() { Status = LoadStatus.Loaded, Tabs = tabs, ActiveIndex = activeIndex };

    public static LoadResult Corrupted(string errorMessage) =>
        new() { Status = LoadStatus.Corrupted, ErrorMessage = errorMessage };
}
