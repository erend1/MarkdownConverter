using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class LocalStorageSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesAllTabsAndActiveIndex()
    {
        var tabs = new List<TabState>
        {
            new() { FileName = "first.md", Content = "# First" },
            new() { FileName = "second.md", Content = "Body of second\nwith newline" },
            new() { FileName = "Untitled 3.md", Content = string.Empty }
        };

        var json = LocalStorageSerializer.Serialize(tabs, activeIndex: 1);
        var result = LocalStorageSerializer.Deserialize(json);

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(3, result.Tabs.Count);
        Assert.Equal("first.md", result.Tabs[0].FileName);
        Assert.Equal("# First", result.Tabs[0].Content);
        Assert.Equal("Body of second\nwith newline", result.Tabs[1].Content);
        Assert.Equal(string.Empty, result.Tabs[2].Content);
        Assert.Equal(1, result.ActiveIndex);
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsEmpty()
    {
        var result = LocalStorageSerializer.Deserialize(null);

        Assert.Equal(LoadStatus.Empty, result.Status);
        Assert.Empty(result.Tabs);
    }

    [Fact]
    public void Deserialize_WhitespaceJson_ReturnsEmpty()
    {
        var result = LocalStorageSerializer.Deserialize("   \t  ");

        Assert.Equal(LoadStatus.Empty, result.Status);
    }

    [Fact]
    public void Deserialize_EmptyTabsArray_ReturnsEmpty()
    {
        var json = """{"Tabs":[],"ActiveIndex":0}""";

        var result = LocalStorageSerializer.Deserialize(json);

        Assert.Equal(LoadStatus.Empty, result.Status);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsCorruptedNotLost()
    {
        // Regression: prior behaviour silently returned "no data" on parse errors,
        // making the user think Save did nothing.
        var result = LocalStorageSerializer.Deserialize("{not valid json");

        Assert.Equal(LoadStatus.Corrupted, result.Status);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public void Deserialize_NullTabsField_ReturnsCorrupted()
    {
        var json = """{"Tabs":null,"ActiveIndex":0}""";

        var result = LocalStorageSerializer.Deserialize(json);

        Assert.Equal(LoadStatus.Corrupted, result.Status);
    }

    [Fact]
    public void Deserialize_ActiveIndexOutOfRange_IsClamped()
    {
        var tabs = new List<TabState>
        {
            new() { FileName = "a.md", Content = "a" },
            new() { FileName = "b.md", Content = "b" }
        };
        var json = LocalStorageSerializer.Serialize(tabs, activeIndex: 99);

        var result = LocalStorageSerializer.Deserialize(json);

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(1, result.ActiveIndex); // clamped to last valid index
    }

    [Fact]
    public void Deserialize_NegativeActiveIndex_IsClampedToZero()
    {
        var tabs = new List<TabState> { new() { FileName = "a.md", Content = "a" } };
        var json = LocalStorageSerializer.Serialize(tabs, activeIndex: -5);

        var result = LocalStorageSerializer.Deserialize(json);

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(0, result.ActiveIndex);
    }
}
