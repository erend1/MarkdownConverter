using System.Text.Json;

namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Pure JSON (de)serialization for the persisted tab session.
/// Kept separate from <see cref="ILocalStorageService"/> so it can be
/// unit-tested without an IJSRuntime / browser dependency.
/// </summary>
public static class LocalStorageSerializer
{
    public static string Serialize(IReadOnlyList<TabState> tabs, int activeIndex)
    {
        var data = new StoredData
        {
            Tabs = tabs.ToList(),
            ActiveIndex = activeIndex
        };
        return JsonSerializer.Serialize(data);
    }

    public static LoadResult Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return LoadResult.Empty();

        StoredData? data;
        try
        {
            data = JsonSerializer.Deserialize<StoredData>(json);
        }
        catch (JsonException ex)
        {
            return LoadResult.Corrupted($"Stored session is not valid JSON: {ex.Message}");
        }

        if (data is null) return LoadResult.Corrupted("Stored session deserialized to null.");
        if (data.Tabs is null) return LoadResult.Corrupted("Stored session has no Tabs list.");
        if (data.Tabs.Count == 0) return LoadResult.Empty();

        var activeIndex = Math.Clamp(data.ActiveIndex, 0, data.Tabs.Count - 1);
        return LoadResult.Loaded(data.Tabs, activeIndex);
    }

    private sealed class StoredData
    {
        public List<TabState> Tabs { get; set; } = new();
        public int ActiveIndex { get; set; }
    }
}
