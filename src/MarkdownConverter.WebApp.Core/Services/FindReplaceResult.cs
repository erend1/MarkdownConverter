namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Typed outcome of a replace-one or replace-all operation.
/// </summary>
public sealed record FindReplaceResult
{
    public int Count { get; init; }
    public FindFailure Failure { get; init; }

    /// <summary>
    /// Retained for compatibility with the former replace-or-navigate
    /// contract. Strict replace-current operations always leave this null.
    /// </summary>
    public FindResult? Navigation { get; init; }

    /// <summary>
    /// True when a newer find command superseded this operation while it was
    /// awaiting the editor surface. Superseded results must not repaint UI.
    /// </summary>
    public bool IsStale { get; init; }

    public static FindReplaceResult Stale { get; } = new() { IsStale = true };
}
