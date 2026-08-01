using System.Text.Json;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class FindOptionsTests
{
    private static readonly JsonSerializerOptions WasmJsonInteropDefaults = new()
    {
        // Mirrors the System.Text.Json conventions Blazor WASM uses for JS
        // interop: PascalCase C# properties serialize to camelCase keys, and
        // deserialization is case-insensitive.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Default_AllFlagsOff()
    {
        Assert.False(FindOptions.Default.WholeWord);
        Assert.False(FindOptions.Default.Regex);
    }

    [Fact]
    public void Serialize_UsesCamelCaseKeys()
    {
        // The JS side reads opts.wholeWord and opts.regex literally —
        // pin the contract so a future rename does not silently break find.
        var opts = new FindOptions { WholeWord = true, Regex = false };

        var json = JsonSerializer.Serialize(opts, WasmJsonInteropDefaults);

        Assert.Contains("\"wholeWord\":true", json);
        Assert.Contains("\"regex\":false", json);
    }

    [Fact]
    public void RoundTrip_PreservesAllFlagCombinations()
    {
        var combos = new[]
        {
            new FindOptions { WholeWord = false, Regex = false },
            new FindOptions { WholeWord = true,  Regex = false },
            new FindOptions { WholeWord = false, Regex = true  },
            new FindOptions { WholeWord = true,  Regex = true  }
        };

        foreach (var original in combos)
        {
            var json = JsonSerializer.Serialize(original, WasmJsonInteropDefaults);
            var copy = JsonSerializer.Deserialize<FindOptions>(json, WasmJsonInteropDefaults);

            Assert.NotNull(copy);
            Assert.Equal(original.WholeWord, copy!.WholeWord);
            Assert.Equal(original.Regex, copy.Regex);
        }
    }

    [Fact]
    public void Deserialize_TolerantOfMissingKeys()
    {
        // If JS ever sends a partial payload, we should fall back to defaults
        // rather than throw — silent matching is preferable to crash here.
        var json = "{}";

        var opts = JsonSerializer.Deserialize<FindOptions>(json, WasmJsonInteropDefaults);

        Assert.NotNull(opts);
        Assert.False(opts!.WholeWord);
        Assert.False(opts.Regex);
    }
}
