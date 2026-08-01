namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Options for the editor's Find / Replace bar. Record type so the
/// <see cref="FindSession"/> can value-compare instances to decide
/// whether the cached match list is still valid.
/// </summary>
public sealed record FindOptions
{
    /// <summary>Match the search term only at word boundaries.</summary>
    public bool WholeWord { get; init; }

    /// <summary>Treat the search term as a regular expression.</summary>
    public bool Regex { get; init; }

    /// <summary>
    /// Case-sensitive match. Default is <c>false</c>, matching VS Code's
    /// "Aa toggle off = case-insensitive" convention.
    /// </summary>
    public bool MatchCase { get; init; }

    /// <summary>
    /// Restrict the search to the user's current textarea selection.
    /// Default <c>false</c>.
    /// </summary>
    public bool InSelection { get; init; }

    public static FindOptions Default { get; } = new();
}
