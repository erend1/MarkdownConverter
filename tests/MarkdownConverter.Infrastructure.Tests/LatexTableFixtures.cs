namespace MarkdownConverter.Infrastructure.Tests;

/// <summary>
/// The Markdown tables under Fixtures/LatexTables that reproduce the page-overflow defects.
/// </summary>
internal static class LatexTableFixtures
{
    public static readonly string[] Names =
    [
        "short-cells-3col",
        "asymmetric-3col",
        "prose-4col",
        "wide-6col",
        "unbreakable-content",
        "split-tokens-and-formulas",
        "alignment-and-markup",
        "dense-13col",
        "multipage-4col"
    ];

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LatexTables", name + ".md"));
}
