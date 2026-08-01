using System.Globalization;

namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Formats a <see cref="FindResult"/> into the short status string shown
/// in the find bar — e.g. <c>"3 / 17"</c>, <c>"No matches"</c>, or
/// <c>"Invalid regex"</c>.
/// </summary>
public static class FindStatusFormatter
{
    public const string InvalidRegex = "Invalid regex";
    public const string SearchTimedOut = "Search timed out";
    public const string NoMatches = "No matches";

    public static string Format(FindResult? result)
    {
        if (result is null) return string.Empty;
        if (result.IsStale) return string.Empty;
        var failure = FormatFailure(result.Failure);
        if (!string.IsNullOrEmpty(failure)) return failure;
        if (result.Total == 0) return NoMatches;

        // Total is 1-based for the user; Index is 0-based internally.
        // Clamp into range defensively in case the JS layer returns
        // something nonsensical — better to display a stable string than
        // throw at render time.
        var oneBased = Math.Clamp(result.Index, 0, result.Total - 1) + 1;
        return string.Format(
            CultureInfo.InvariantCulture, "{0} / {1}", oneBased, result.Total);
    }

    public static string FormatFailure(FindFailure failure) => failure switch
    {
        FindFailure.InvalidPattern => InvalidRegex,
        FindFailure.TimedOut => SearchTimedOut,
        _ => string.Empty
    };
}
