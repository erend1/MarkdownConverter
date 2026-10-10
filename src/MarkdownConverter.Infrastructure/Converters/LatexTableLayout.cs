using System.Globalization;

namespace MarkdownConverter.Infrastructure.Converters;

/// <summary>
/// How tightly a table has to be set to keep its columns inside the line.
/// </summary>
internal enum LatexTableDensity
{
    /// <summary>Default cell padding; the columns can average a comfortable width.</summary>
    Normal,

    /// <summary>Halved cell padding for tables with too many columns for the default.</summary>
    Compact,

    /// <summary>No cell padding: more columns than the page can show legibly.</summary>
    Bare
}

/// <summary>
/// Plans <c>longtable</c> columns that cannot be wider than the line they are set in.
/// Each width is emitted as a share of a budget TeX computes itself (the line width
/// minus cell padding and column rules); the shares follow the estimated width of the
/// cell contents.
/// </summary>
internal static class LatexTableLayout
{
    // Geometry of the document MarkdownLatexRenderer emits: A4 with 1in margins, 11pt
    // Latin Modern and the article-class table defaults. It only steers proportions and
    // break hints; the emitted TeX measures the real line, so another line stays safe.
    private const double LineWidthPt = 452.97;
    private const double EmPt = 10.95;
    private const double RuleWidthPt = 0.4;
    private const double DefaultPaddingPt = 6;
    private const double CompactPaddingPt = 3;

    // No column is planned narrower than this while the table has room for it.
    private const double MinColumnWidthEm = 2;

    // A column is kept wide enough for its longest word up to this width; longer tokens
    // (URLs, paths, hashes) wrap instead of starving the other columns.
    private const double ProtectedWordCapEm = 12;

    // The default padding is kept while the columns can average this width. A formula
    // may not squeeze the other columns below it either.
    private const double ComfortableColumnWidthEm = 3;

    // Below one glyph per column the padding is dropped altogether.
    private const double LegibleColumnWidthEm = 1;

    // Estimates ignore kerning and the narrower line inside a quote, callout or list
    // item; the margin keeps a word that only just fits from being broken.
    private const double WidthSafetyFactor = 1.08;

    // Shares are rounded down, which costs a column up to this much of its planned width.
    private const double FitToleranceEm = 0.01;

    // Column widths are emitted as four-digit decimal shares of the budget.
    internal const int ShareScale = 10000;

    private const double MonospaceWidthEm = 0.525;
    private const double BoldWidthFactor = 1.2;
    private const double SymbolWidthEm = 1;

    // Advance widths of '!'..'~' as the renderer escapes them, set in Latin Modern Roman
    // (T1), in thousandths of an em.
    private static readonly short[] RomanWidths =
    [
        278, 374, 833, 500, 833, 778, 278, 389, 389, 500, 778, 278, 333, 278, 500, // ! " # $ % & ' ( ) * + , - . /
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500,                          // 0-9
        278, 278, 778, 778, 778, 472, 778,                                         // : ; < = > ? @
        750, 708, 722, 764, 681, 653, 785, 750, 361, 514, 778, 625, 917,           // A-M
        750, 778, 681, 778, 736, 556, 722, 750, 750, 1028, 750, 750, 611,          // N-Z
        278, 500, 278, 556, 750, 278,                                              // [ \ ] ^ _ `
        500, 556, 444, 556, 444, 306, 500, 556, 278, 306, 528, 278, 833,           // a-m
        556, 500, 556, 528, 392, 394, 389, 556, 528, 722, 528, 528, 444,           // n-z
        500, 278, 500, 556                                                         // { | } ~
    ];

    // Formulas are measured in glyphs of this width. A command prints one symbol unless
    // MathCommands says otherwise.
    private const double MathGlyphWidthEm = 0.55;
    private const double MathSymbolGlyphs = 1.5;
    private const double MathOperatorGlyphs = 2.4;
    private static readonly Dictionary<string, (double Glyphs, bool IsOperator)> MathCommands = CreateMathCommands();

    /// <summary>
    /// Sizes the columns of a table from estimated widths, in em of the body font: each
    /// column's widest cell set on one line, its widest word, and its widest content that
    /// cannot be broken at all (a formula between two operators).
    /// </summary>
    public static LatexTablePlan Plan(
        IReadOnlyList<double> naturalEm, IReadOnlyList<double> widestWordEm, IReadOnlyList<double> unbreakableEm)
    {
        int count = naturalEm.Count;
        var density = ChooseDensity(count);
        // Rules are only given up where they alone would use up the line.
        bool hasRules = (count - 1) * RuleWidthPt < LineWidthPt / 2;
        if (count == 0) return new LatexTablePlan(density, hasRules, [], []);

        double budget = BudgetEm(count, density, hasRules);

        // What a column asks for, most pressing first. Each array includes the one before.
        var rigid = new double[count];          // its widest formula part, which cannot break
        var word = new double[count];           // its longest word, which should not have to
        var comfortable = new double[count];    // at least the minimum width
        var natural = new double[count];        // its widest cell on a single line
        var full = new double[count];
        double rigidCap = Math.Max(0, budget - (count - 1) * ComfortableColumnWidthEm);
        for (int i = 0; i < count; i++)
        {
            rigid[i] = Math.Min(unbreakableEm[i] * WidthSafetyFactor, rigidCap);
            word[i] = Math.Max(rigid[i], Math.Min(widestWordEm[i] * WidthSafetyFactor, ProtectedWordCapEm));
            comfortable[i] = Math.Max(word[i], MinColumnWidthEm);
            natural[i] = Math.Max(comfortable[i], Math.Min(naturalEm[i] * WidthSafetyFactor, budget));
            full[i] = Math.Max(natural[i], budget);
        }

        double[] widths;
        if (natural.Sum() <= budget)
            widths = Level(natural, full, budget);              // room to spare: widen the narrowest columns
        else if (comfortable.Sum() <= budget)
            widths = Level(comfortable, natural, budget);       // wrap the widest columns
        else if (word.Sum() <= budget)
            widths = Level(word, comfortable, budget);          // dense: give up the minimum width
        else if (rigid.Sum() <= budget)
            widths = Level(rigid, word, budget);                // denser: break the longest words
        else
            widths = Enumerable.Repeat(budget / count, count).ToArray();

        // TeX stores a decimal factor in units of 2^-16 and may round it up by half a
        // unit, so the shares leave that much of the budget unused.
        int usable = Math.Max(0, ShareScale - (int)Math.Ceiling(count * ShareScale / 131072.0));
        double total = widths.Sum();

        var shares = new int[count];
        var widthsEm = new double[count];
        for (int i = 0; i < count; i++)
        {
            shares[i] = (int)Math.Floor(widths[i] / total * usable + 1e-9);
            widthsEm[i] = budget * shares[i] / ShareScale;
        }

        return new LatexTablePlan(density, hasRules, shares, widthsEm);
    }

    /// <summary>
    /// Whether text of the estimated width can be set on one line of a column.
    /// </summary>
    public static bool Fits(double estimatedEm, double columnEm) =>
        estimatedEm * WidthSafetyFactor <= columnEm + FitToleranceEm;

    /// <summary>
    /// How many characters of a run of the estimated width fit on one line of a column.
    /// </summary>
    public static double CharactersPerLine(double estimatedEm, int characterCount, double columnEm) =>
        estimatedEm <= 0 ? characterCount : columnEm * characterCount / (estimatedEm * WidthSafetyFactor);

    /// <summary>
    /// Estimated advance width of one rendered character, in em of the body font.
    /// </summary>
    public static double CharWidthEm(char ch, bool bold, bool monospace)
    {
        if (ch > '\u024F') return SymbolWidthEm;    // mapped symbols such as arrows and dashes
        if (monospace) return MonospaceWidthEm;

        double width = ch is >= '!' and <= '~' ? RomanWidths[ch - '!'] / 1000.0
            : char.IsLower(ch) ? 0.6                // accented lower-case letters
            : 0.8;                                  // accented capitals and Latin-1 symbols
        return bold ? width * BoldWidthFactor : width;
    }

    /// <summary>
    /// Estimated width of the space between two words, in em of the body font.
    /// </summary>
    public static double SpaceWidthEm(bool bold, bool monospace) =>
        monospace ? MonospaceWidthEm : bold ? 0.4 : 1.0 / 3;

    /// <summary>
    /// Rough width of an inline formula and of its widest part that TeX cannot break. TeX
    /// breaks a formula only after a relation or binary operator outside braces. Stacked
    /// constructs such as fractions are counted side by side, so they come out too wide.
    /// </summary>
    public static (double TotalEm, double UnbreakableEm) MathWidthEm(string tex)
    {
        double total = 0, part = 0, widestPart = 0;
        int depth = 0;
        char previous = ' ';
        for (int i = 0; i < tex.Length; i++)
        {
            char ch = tex[i];
            double glyphs = 0;
            bool isOperator = false;
            if (ch == '\\')
            {
                int start = i + 1;
                int end = start;
                while (end < tex.Length && char.IsAsciiLetter(tex[end])) end++;

                if (end == start)
                {
                    glyphs = 0.5;                   // control symbol such as \, or \{
                    i = start;
                }
                else
                {
                    var command = tex[start..end];
                    if (command == "left") depth++;
                    else if (command == "right") depth--;

                    (glyphs, isOperator) = MathCommands.GetValueOrDefault(command, (MathSymbolGlyphs, false));
                    i = end - 1;
                }
            }
            else if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
            }
            else if (ch is '=' or '<' or '>' or '+' or '-')
            {
                glyphs = MathOperatorGlyphs;
                // Not a sign that opens the formula or a script such as x^-1.
                isOperator = part > 0 && previous is not ('^' or '_');
            }
            else if (!char.IsWhiteSpace(ch) && ch is not ('^' or '_' or '&'))
            {
                glyphs = char.IsUpper(ch) ? 1.4 : 1;
            }

            if (!char.IsWhiteSpace(ch)) previous = ch;
            total += glyphs;
            part += glyphs;
            if (isOperator && depth == 0)
            {
                widestPart = Math.Max(widestPart, part);
                part = 0;
            }
        }

        return (total * MathGlyphWidthEm, Math.Max(widestPart, part) * MathGlyphWidthEm);
    }

    // Math commands that are not one ordinary symbol wide: their width in glyphs, and
    // whether TeX may break an inline formula after them.
    private static Dictionary<string, (double Glyphs, bool IsOperator)> CreateMathCommands()
    {
        var commands = new Dictionary<string, (double, bool)>(StringComparer.Ordinal);
        // Arranges or restyles its argument without printing a glyph
        Add(0, false,
            "left right big Big bigg Bigg bigl bigr Bigl Bigr frac dfrac tfrac limits nolimits " +
            "displaystyle textstyle mathrm mathbf mathit mathsf mathtt mathbb mathcal mathfrak " +
            "text textrm textbf textit operatorname boldsymbol " +
            "hat bar vec tilde dot ddot widehat widetilde overline underline");
        // Upright function names
        Add(3, false,
            "log ln lg sin cos tan cot sec csc arcsin arccos arctan sinh cosh tanh exp " +
            "lim liminf limsup max min sup inf det gcd arg deg dim ker hom Pr pmod bmod");
        // Relations and binary operators, set with space around them
        Add(MathOperatorGlyphs, true,
            "leq geq neq le ge ne approx equiv sim simeq cong propto in notin ni subset subseteq " +
            "supset supseteq times cdot div pm mp cup cap setminus to rightarrow leftarrow Rightarrow " +
            "Leftarrow leftrightarrow mapsto mid parallel perp land lor wedge vee oplus otimes ll gg prec succ");
        // Long arrows
        Add(5, true,
            "implies iff impliedby Longrightarrow Longleftarrow Longleftrightarrow " +
            "longrightarrow longleftarrow longleftrightarrow");
        return commands;

        void Add(double glyphs, bool isOperator, string names)
        {
            foreach (var name in names.Split(' '))
            {
                commands[name] = (glyphs, isOperator);
            }
        }
    }

    private static LatexTableDensity ChooseDensity(int columnCount)
    {
        if (BudgetEm(columnCount, LatexTableDensity.Normal, hasRules: true) >= columnCount * ComfortableColumnWidthEm)
            return LatexTableDensity.Normal;

        if (BudgetEm(columnCount, LatexTableDensity.Compact, hasRules: true) >= columnCount * LegibleColumnWidthEm)
            return LatexTableDensity.Compact;

        return LatexTableDensity.Bare;
    }

    /// <summary>
    /// The cell padding of a density in points, or null for the document default.
    /// </summary>
    internal static double? PaddingPt(LatexTableDensity density) => density switch
    {
        LatexTableDensity.Compact => CompactPaddingPt,
        LatexTableDensity.Bare => 0,
        _ => null
    };

    // The width left for cell contents once padding and column rules are paid for.
    private static double BudgetEm(int columnCount, LatexTableDensity density, bool hasRules)
    {
        double padding = PaddingPt(density) ?? DefaultPaddingPt;
        double rules = hasRules ? (columnCount - 1) * RuleWidthPt : 0;
        return (LineWidthPt - 2 * columnCount * padding - rules) / EmPt;
    }

    // Raises one common level until the columns, each held between its lower and upper
    // bound, use the whole budget. Requires sum(lower) <= budget <= sum(upper).
    private static double[] Level(double[] lower, double[] upper, double budget)
    {
        double low = 0;
        double high = upper.Max();
        for (int step = 0; step < 64; step++)
        {
            double middle = (low + high) / 2;
            if (TotalAt(middle) < budget) low = middle;
            else high = middle;
        }

        var widths = new double[lower.Length];
        for (int i = 0; i < widths.Length; i++)
        {
            widths[i] = Math.Clamp(high, lower[i], upper[i]);
        }
        return widths;

        double TotalAt(double level)
        {
            double total = 0;
            for (int i = 0; i < lower.Length; i++)
            {
                total += Math.Clamp(level, lower[i], upper[i]);
            }
            return total;
        }
    }

    /// <summary>
    /// Accumulates the estimated width of one table cell while its inlines are walked.
    /// </summary>
    internal sealed class CellMeasure
    {
        private double _word;
        private double _widestWord;
        private double _pendingSpace;

        /// <summary>Width of the whole cell set on a single line.</summary>
        public double NaturalEm { get; private set; }

        /// <summary>Width of the widest run that contains no space.</summary>
        public double WidestWordEm => Math.Max(_widestWord, _word);

        /// <summary>Width of the widest content that offers no place to break at all.</summary>
        public double UnbreakableEm { get; private set; }

        public void AddGlyph(double widthEm)
        {
            // Leading and repeated spaces take no room in TeX.
            if (NaturalEm > 0) NaturalEm += _pendingSpace;
            _pendingSpace = 0;

            NaturalEm += widthEm;
            _word += widthEm;
        }

        public void AddSpace(double widthEm)
        {
            _widestWord = Math.Max(_widestWord, _word);
            _word = 0;
            _pendingSpace = widthEm;
        }

        /// <summary>Adds content such as a formula, of which only a part must stay on one line.</summary>
        public void AddBox(double widthEm, double unbreakableEm)
        {
            AddGlyph(unbreakableEm);
            NaturalEm += widthEm - unbreakableEm;
            UnbreakableEm = Math.Max(UnbreakableEm, unbreakableEm);
        }
    }
}

/// <summary>
/// The column widths chosen for one table, as TeX source and as estimates.
/// </summary>
internal sealed class LatexTablePlan
{
    private readonly bool _hasRules;
    private readonly int[] _shares;
    private readonly double[] _widthsEm;

    internal LatexTablePlan(LatexTableDensity density, bool hasRules, int[] shares, double[] widthsEm)
    {
        Density = density;
        _hasRules = hasRules;
        _shares = shares;
        _widthsEm = widthsEm;
    }

    public LatexTableDensity Density { get; }

    public int ColumnCount => _shares.Length;

    /// <summary>Value for <c>\tabcolsep</c>, or null to keep the document default.</summary>
    public string? CellPadding =>
        LatexTableLayout.PaddingPt(Density) is { } points ? Invariant(points) + "pt" : null;

    /// <summary>Text placed between two column declarations.</summary>
    public string ColumnSeparator => _hasRules ? " | " : " ";

    /// <summary>A column's width in ten-thousandths of the content budget.</summary>
    public int Share(int column) => _shares[column];

    /// <summary>Estimated width of a column in em of the body font.</summary>
    public double ColumnWidthEm(int column) => _widthsEm[column];

    /// <summary>
    /// The TeX dimension for a column's <c>p{...}</c> argument: its share of what the line
    /// has left after both paddings of every cell and the rules between the columns.
    /// </summary>
    public string ColumnWidth(int column)
    {
        var share = "0." + _shares[column].ToString("D4", CultureInfo.InvariantCulture);
        var rules = _hasRules && ColumnCount > 1 ? $@"-{Invariant(ColumnCount - 1)}\arrayrulewidth" : "";
        return $@"{share}\dimexpr\linewidth-{Invariant(2 * ColumnCount)}\tabcolsep{rules}\relax";
    }

    private static string Invariant(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);
}
