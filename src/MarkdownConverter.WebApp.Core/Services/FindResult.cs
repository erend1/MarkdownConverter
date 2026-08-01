namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Result of a find navigation operation. Carries both the total match count
/// and the current 0-based index so the find bar can render a "3 / 17"
/// position indicator.
/// </summary>
public sealed class FindResult
{
    /// <summary>Total matches for the current search term and options.</summary>
    public int Total { get; init; }

    /// <summary>
    /// 0-based index of the currently selected match, or <c>-1</c> when
    /// there are no matches at all.
    /// </summary>
    public int Index { get; init; }

    public FindFailure Failure { get; init; }

    /// <summary>
    /// True when a newer find command superseded this operation while it was
    /// awaiting the editor surface. Superseded results must not repaint UI.
    /// </summary>
    public bool IsStale { get; init; }

    public static FindResult Stale { get; } = new() { Index = -1, IsStale = true };
}
