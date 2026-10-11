using System.Globalization;
using System.Text.RegularExpressions;

namespace MarkdownConverter.Infrastructure.Tests;

/// <summary>
/// One <c>longtable</c> read back from rendered LaTeX, with the width TeX would give it.
/// Lets tests check the emitted column specification instead of trusting the planner.
/// </summary>
internal sealed partial class LatexTableSource
{
    // TeX works in scaled points: 65536sp = 1pt.
    public const long PointSp = 65536;

    /// <summary>\linewidth of the generated document: A4 with 1in margins.</summary>
    public static readonly long DocumentLineWidthSp = Sp(452.9679);

    private static readonly long DefaultPaddingSp = Sp(6);
    private static readonly long RuleWidthSp = TexDimension("0.4");

    private LatexTableSource(
        IReadOnlyList<Column> columns, string? cellPadding, int? chunkRows, int ruleCount, bool isGrouped,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Columns = columns;
        CellPadding = cellPadding;
        ChunkRows = chunkRows;
        RuleCount = ruleCount;
        IsGrouped = isGrouped;
        Rows = rows;
    }

    public IReadOnlyList<Column> Columns { get; }

    /// <summary>The <c>\tabcolsep</c> set for this table, or null for the document default.</summary>
    public string? CellPadding { get; }

    /// <summary>The <c>\LTchunksize</c> set for this table, or null for the package default.</summary>
    public int? ChunkRows { get; }

    /// <summary>Number of vertical rules between the columns.</summary>
    public int RuleCount { get; }

    /// <summary>Whether the table is wrapped in <c>\begingroup</c> ... <c>\endgroup</c>.</summary>
    public bool IsGrouped { get; }

    /// <summary>The TeX source of every cell, header row first.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    public IReadOnlyList<int> Shares => Columns.Select(c => c.Share).ToList();

    public static LatexTableSource Single(string tex) => Assert.Single(ParseAll(tex));

    public static IReadOnlyList<LatexTableSource> ParseAll(string tex)
    {
        var lines = tex.ReplaceLineEndings("\n").Split('\n');
        var tables = new List<LatexTableSource>();

        for (int i = 0; i < lines.Length; i++)
        {
            int begin = lines[i].IndexOf(@"\begin{longtable}{", StringComparison.Ordinal);
            if (begin < 0) continue;

            var spec = lines[i][(begin + @"\begin{longtable}".Length)..];
            var declarations = ColumnPattern().Matches(spec);
            Assert.True(declarations.Count > 0, $"Unrecognised longtable column specification: {spec}");

            // Every character of the specification must belong to a column or a separator.
            var rest = ColumnPattern().Replace(spec[1..^1], "");
            Assert.True(rest.Replace("|", "").Trim().Length == 0, $"Unexpected text in column specification: {rest}");

            // Settings for this table only sit between \begingroup and the table.
            string? padding = null;
            int? chunkRows = null;
            int settings = i - 1;
            if (settings >= 0 && ChunkPattern().Match(lines[settings]) is { Success: true } chunk)
            {
                chunkRows = int.Parse(chunk.Groups[1].Value, CultureInfo.InvariantCulture);
                settings--;
            }
            if (settings >= 0 && PaddingPattern().Match(lines[settings]) is { Success: true } match)
            {
                padding = match.Groups[1].Value;
                settings--;
            }
            bool grouped = settings >= 0 && lines[settings].EndsWith(@"\begingroup", StringComparison.Ordinal);
            Assert.Equal(grouped, padding is not null || chunkRows is not null);

            var rows = new List<IReadOnlyList<string>>();
            int end = i + 1;
            for (; end < lines.Length && lines[end] != @"\end{longtable}"; end++)
            {
                if (lines[end] == @"\hline") continue;
                Assert.EndsWith(@" \\", lines[end]);
                rows.Add(lines[end][..^3].Split(" & "));
            }

            Assert.True(end < lines.Length, "longtable is not closed");
            Assert.Equal(grouped, end + 1 < lines.Length && lines[end + 1] == @"\endgroup");

            tables.Add(new LatexTableSource(
                declarations.Select(Column.From).ToList(), padding, chunkRows, rest.Count(c => c == '|'), grouped, rows));
            i = end;
        }

        return tables;
    }

    /// <summary>
    /// The width of the whole table for a line width, computed the way TeX evaluates the
    /// emitted expressions: content columns plus the padding of every cell plus the rules.
    /// </summary>
    public long TotalWidthSp(long lineWidthSp) =>
        ColumnWidthsSp(lineWidthSp).Sum() + 2 * Columns.Count * PaddingSp + RuleCount * RuleWidthSp;

    public IReadOnlyList<long> ColumnWidthsSp(long lineWidthSp) =>
        Columns.Select(c => c.WidthSp(lineWidthSp, PaddingSp, RuleWidthSp)).ToList();

    /// <summary>The text of a cell with the break opportunities removed.</summary>
    public static string WithoutBreaks(string cell) =>
        cell.Replace(@"\allowbreak{}", "").Replace(@"\-", "");

    private long PaddingSp => CellPadding is null
        ? DefaultPaddingSp
        : TexDimension(CellPadding[..^2]);

    private static long Sp(double points) => (long)Math.Round(points * PointSp);

    // TeX's scan_dimen for "<decimal>pt".
    private static long TexDimension(string decimalPoints)
    {
        var parts = decimalPoints.Split('.');
        long integer = long.Parse(parts[0], CultureInfo.InvariantCulture);
        return integer * PointSp + (parts.Length > 1 ? RoundDecimals(parts[1]) : 0);
    }

    // TeX's round_decimals: the fraction digits as a multiple of 2^-16, rounded to nearest.
    private static long RoundDecimals(string digits)
    {
        long a = 0;
        for (int k = digits.Length - 1; k >= 0; k--)
        {
            a = (a + (digits[k] - '0') * 2 * PointSp) / 10;
        }
        return (a + 1) / 2;
    }

    [GeneratedRegex(@">\{\\(?<align>raggedright|centering|raggedleft)\\arraybackslash\\hspace\{0pt\}\}p\{0\.(?<share>\d{4})\\dimexpr\\linewidth-(?<padding>\d+)\\tabcolsep(?:-(?<rules>\d+)\\arrayrulewidth)?\\relax\}")]
    private static partial Regex ColumnPattern();

    [GeneratedRegex(@"^\\setlength\{\\tabcolsep\}\{(\d+(?:\.\d+)?pt)\}$")]
    private static partial Regex PaddingPattern();

    [GeneratedRegex(@"^\\LTchunksize=(\d+)\\relax$")]
    private static partial Regex ChunkPattern();

    /// <summary>One column declaration of the table.</summary>
    internal sealed record Column(string Alignment, int Share, int PaddingCount, int BudgetRuleCount)
    {
        public static Column From(Match match) => new(
            match.Groups["align"].Value,
            int.Parse(match.Groups["share"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["padding"].Value, CultureInfo.InvariantCulture),
            match.Groups["rules"].Success ? int.Parse(match.Groups["rules"].Value, CultureInfo.InvariantCulture) : 0);

        // "0.dddd<dimen>": TeX multiplies the dimension by the rounded factor and truncates.
        public long WidthSp(long lineWidthSp, long paddingSp, long ruleWidthSp)
        {
            long budget = lineWidthSp - PaddingCount * paddingSp - BudgetRuleCount * ruleWidthSp;
            long factor = RoundDecimals(Share.ToString("D4", CultureInfo.InvariantCulture));
            return budget >= 0 ? budget * factor / PointSp : -(-budget * factor / PointSp);
        }
    }
}
