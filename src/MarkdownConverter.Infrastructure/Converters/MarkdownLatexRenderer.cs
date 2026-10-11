using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.MarkdigExtensions;

namespace MarkdownConverter.Infrastructure.Converters;

internal static class MarkdownLatexRenderer
{
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = "[Sharp]C", ["cs"] = "[Sharp]C", ["c#"] = "[Sharp]C",
        ["c"] = "C", ["cpp"] = "C++", ["c++"] = "C++",
        ["java"] = "Java", ["javascript"] = "JavaScript", ["js"] = "JavaScript",
        ["typescript"] = "JavaScript", ["ts"] = "JavaScript",
        ["python"] = "Python", ["py"] = "Python",
        ["ruby"] = "Ruby", ["html"] = "HTML", ["xml"] = "XML",
        ["sql"] = "SQL", ["bash"] = "bash", ["sh"] = "bash", ["shell"] = "bash",
        ["php"] = "PHP", ["perl"] = "Perl", ["r"] = "R",
        ["matlab"] = "Matlab", ["fortran"] = "Fortran", ["pascal"] = "Pascal",
        ["tex"] = "TeX", ["latex"] = "TeX", ["make"] = "make", ["makefile"] = "make",
    };

    private static readonly Dictionary<string, string> UnicodeMap = new()
    {
        // Common emoji and symbols → LaTeX replacements
        ["\u274C"] = "{\\texttimes}",   // ❌
        ["\u2705"] = "{\\checkmark}",   // ✅
        ["\u2714"] = "{\\checkmark}",   // ✔
        ["\u2718"] = "{\\texttimes}",   // ✘
        ["\u2713"] = "{\\checkmark}",   // ✓
        ["\u2717"] = "{\\texttimes}",   // ✗
        ["\u2022"] = "{\\textbullet}",  // •
        ["\u2026"] = "{\\ldots}",       // …
        ["\u2192"] = "{$\\rightarrow$}",// →
        ["\u2190"] = "{$\\leftarrow$}", // ←
        ["\u2194"] = "{$\\leftrightarrow$}", // ↔
        ["\u21D2"] = "{$\\Rightarrow$}",// ⇒
        ["\u2014"] = "---",             // —
        ["\u2013"] = "--",              // –
        ["\u2018"] = "`",              // '
        ["\u2019"] = "'",             // '
        ["\u201C"] = "``",            // "
        ["\u201D"] = "''",            // "
        ["\u00A9"] = "{\\copyright}",   // ©
        ["\u00AE"] = "{\\textregistered}", // ®
        ["\u2122"] = "{\\texttrademark}", // ™
        ["\u00B0"] = "{\\textdegree}",  // °
        ["\u00D7"] = "{$\\times$}",     // ×
        ["\u00F7"] = "{$\\div$}",       // ÷
        ["\u2248"] = "{$\\approx$}",    // ≈
        ["\u2260"] = "{$\\neq$}",       // ≠
        ["\u2264"] = "{$\\leq$}",       // ≤
        ["\u2265"] = "{$\\geq$}",       // ≥
        ["\u221E"] = "{$\\infty$}",     // ∞
    };

    private static string? GetListingsLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info)) return null;
        var lang = info.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return LanguageMap.TryGetValue(lang, out var mapped) ? mapped : null;
    }

    public static string Render(string rawMarkdown, string? bibResourceName = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"\documentclass[11pt,a4paper]{article}");
        sb.AppendLine(@"\usepackage[utf8]{inputenc}");
        sb.AppendLine(@"\usepackage[T1]{fontenc}");
        sb.AppendLine(@"\usepackage{lmodern}");
        sb.AppendLine(@"\usepackage{amsmath,amssymb,amsfonts}");
        sb.AppendLine(@"\usepackage{geometry}");
        sb.AppendLine(@"\geometry{margin=1in}");
        sb.AppendLine(@"\usepackage[hidelinks]{hyperref}");
        sb.AppendLine(@"\usepackage{longtable}");
        sb.AppendLine(@"\usepackage{booktabs}");
        sb.AppendLine(@"\usepackage{array}");
        sb.AppendLine(@"\usepackage{parskip}");
        sb.AppendLine(@"\usepackage{xcolor}");
        sb.AppendLine(@"\usepackage[most]{tcolorbox}");
        sb.AppendLine(@"\usepackage{fontawesome5}");
        sb.AppendLine();
        // Blockquote style: grey left bar, light background, italic
        sb.AppendLine(@"\newtcolorbox{mdquote}{");
        sb.AppendLine(@"  blanker, left=12pt, borderline west={3pt}{0pt}{gray!50},");
        sb.AppendLine(@"  before skip=6pt, after skip=6pt, colback=gray!5,");
        sb.AppendLine(@"  boxrule=0pt, arc=0pt, outer arc=0pt, top=4pt, bottom=4pt, right=8pt, left skip=4pt");
        sb.AppendLine(@"}");
        // Callout environments
        sb.AppendLine(@"\newtcolorbox{callout-note}{");
        sb.AppendLine(@"  colback=blue!5, colframe=blue!50!black, coltitle=blue!50!black,");
        sb.AppendLine(@"  title={\faInfoCircle\ Note}, fonttitle=\bfseries,");
        sb.AppendLine(@"  left=8pt, right=8pt, top=4pt, bottom=4pt, arc=2pt, boxrule=0.8pt,");
        sb.AppendLine(@"  before skip=6pt, after skip=6pt");
        sb.AppendLine(@"}");
        sb.AppendLine(@"\newtcolorbox{callout-warning}{");
        sb.AppendLine(@"  colback=yellow!5, colframe=yellow!70!black, coltitle=yellow!70!black,");
        sb.AppendLine(@"  title={\faExclamationTriangle\ Warning}, fonttitle=\bfseries,");
        sb.AppendLine(@"  left=8pt, right=8pt, top=4pt, bottom=4pt, arc=2pt, boxrule=0.8pt,");
        sb.AppendLine(@"  before skip=6pt, after skip=6pt");
        sb.AppendLine(@"}");
        sb.AppendLine(@"\newtcolorbox{callout-tip}{");
        sb.AppendLine(@"  colback=green!5, colframe=green!50!black, coltitle=green!50!black,");
        sb.AppendLine(@"  title={\faCheck\ Tip}, fonttitle=\bfseries,");
        sb.AppendLine(@"  left=8pt, right=8pt, top=4pt, bottom=4pt, arc=2pt, boxrule=0.8pt,");
        sb.AppendLine(@"  before skip=6pt, after skip=6pt");
        sb.AppendLine(@"}");
        sb.AppendLine(@"\newtcolorbox{callout-important}{");
        sb.AppendLine(@"  colback=violet!5, colframe=violet!70!black, coltitle=violet!70!black,");
        sb.AppendLine(@"  title={\faExclamation\ Important}, fonttitle=\bfseries,");
        sb.AppendLine(@"  left=8pt, right=8pt, top=4pt, bottom=4pt, arc=2pt, boxrule=0.8pt,");
        sb.AppendLine(@"  before skip=6pt, after skip=6pt");
        sb.AppendLine(@"}");
        sb.AppendLine(@"\newtcolorbox{callout-caution}{");
        sb.AppendLine(@"  colback=red!5, colframe=red!70!black, coltitle=red!70!black,");
        sb.AppendLine(@"  title={\faBan\ Caution}, fonttitle=\bfseries,");
        sb.AppendLine(@"  left=8pt, right=8pt, top=4pt, bottom=4pt, arc=2pt, boxrule=0.8pt,");
        sb.AppendLine(@"  before skip=6pt, after skip=6pt");
        sb.AppendLine(@"}");
        sb.AppendLine(@"\usepackage{listings}");
        sb.AppendLine(@"\lstset{");
        sb.AppendLine(@"  backgroundcolor=\color{gray!10},");
        sb.AppendLine(@"  basicstyle=\ttfamily\small,");
        sb.AppendLine(@"  breaklines=true,");
        sb.AppendLine(@"  frame=single,");
        sb.AppendLine(@"  rulecolor=\color{gray!50},");
        sb.AppendLine(@"  numbers=left,");
        sb.AppendLine(@"  numberstyle=\tiny\color{gray},");
        sb.AppendLine(@"  numbersep=8pt,");
        sb.AppendLine(@"  tabsize=4,");
        sb.AppendLine(@"  showstringspaces=false,");
        sb.AppendLine(@"  xleftmargin=1.5em,");
        sb.AppendLine(@"  framexleftmargin=1.5em,");
        sb.AppendLine(@"  extendedchars=true,");
        sb.AppendLine(@"  literate=");
        sb.AppendLine(@"    {á}{{\'a}}1 {é}{{\'e}}1 {í}{{\'\i}}1 {ó}{{\'o}}1 {ú}{{\'u}}1");
        sb.AppendLine(@"    {Á}{{\'A}}1 {É}{{\'E}}1 {Í}{{\'I}}1 {Ó}{{\'O}}1 {Ú}{{\'U}}1");
        sb.AppendLine(@"    {à}{{\`a}}1 {è}{{\`e}}1 {ì}{{\`\i}}1 {ò}{{\`o}}1 {ù}{{\`u}}1");
        sb.AppendLine(@"    {ä}{{\""{a}}}1 {ë}{{\""{e}}}1 {ï}{{\""\i}}1 {ö}{{\""{o}}}1 {ü}{{\""{u}}}1");
        sb.AppendLine(@"    {Ä}{{\""{A}}}1 {Ë}{{\""{E}}}1 {Ö}{{\""{O}}}1 {Ü}{{\""{U}}}1");
        sb.AppendLine(@"    {ğ}{{\u{g}}}1 {Ğ}{{\u{G}}}1");
        sb.AppendLine(@"    {ş}{{\c{s}}}1 {Ş}{{\c{S}}}1");
        sb.AppendLine(@"    {ç}{{\c{c}}}1 {Ç}{{\c{C}}}1");
        sb.AppendLine(@"    {ı}{{\i}}1 {İ}{{\.{I}}}1");
        sb.AppendLine(@"    {ñ}{{\~{n}}}1 {Ñ}{{\~{N}}}1");
        sb.AppendLine(@"    {â}{{\^{a}}}1 {ê}{{\^{e}}}1 {î}{{\^{\i}}}1 {ô}{{\^{o}}}1 {û}{{\^{u}}}1");
        sb.AppendLine(@"}");

        if (!string.IsNullOrEmpty(bibResourceName))
        {
            sb.AppendLine(@"\usepackage[style=apa,backend=biber]{biblatex}");
            sb.AppendLine($@"\addbibresource{{{bibResourceName}}}");
        }

        sb.AppendLine();
        sb.AppendLine(@"\begin{document}");
        sb.AppendLine();

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMathematics()
            .UsePipeTables()
            .Use(new CitationExtension())
            .Build();

        var document = Markdig.Markdown.Parse(rawMarkdown, pipeline);

        foreach (var block in document)
        {
            RenderBlock(block, sb);
        }

        if (!string.IsNullOrEmpty(bibResourceName))
        {
            sb.AppendLine();
            sb.AppendLine(@"\printbibliography");
        }

        sb.AppendLine();
        sb.AppendLine(@"\end{document}");

        return sb.ToString();
    }

    private static void RenderBlock(Block block, StringBuilder sb, double lineWidthPt = LatexTableLayout.PageLineWidthPt)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var sectionCmd = heading.Level switch
                {
                    1 => "section",
                    2 => "subsection",
                    3 => "subsubsection",
                    4 => "paragraph",
                    _ => "subparagraph"
                };
                sb.Append($@"\{sectionCmd}*{{");
                RenderInlines(heading.Inline!, sb);
                sb.AppendLine("}");
                sb.AppendLine();
                break;

            case ParagraphBlock paragraph:
                RenderInlines(paragraph.Inline!, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case MathBlock mathBlock:
                var mathContent = mathBlock.Lines.ToString().Trim();
                sb.AppendLine(@"\[");
                sb.AppendLine(mathContent);
                sb.AppendLine(@"\]");
                sb.AppendLine();
                break;

            case FencedCodeBlock codeBlock:
                // Ensure vertical space before code blocks so headings don't overlap
                sb.AppendLine(@"\leavevmode");
                var lang = GetListingsLanguage(codeBlock.Info);
                if (lang != null)
                    sb.AppendLine($@"\begin{{lstlisting}}[language={{{lang}}}]");
                else
                    sb.AppendLine(@"\begin{lstlisting}");
                sb.AppendLine(SanitizeCodeContent(codeBlock.Lines.ToString().TrimEnd()));
                sb.AppendLine(@"\end{lstlisting}");
                sb.AppendLine();
                break;

            case ListBlock list:
                var env = list.IsOrdered ? "enumerate" : "itemize";
                sb.AppendLine($@"\begin{{{env}}}");
                foreach (var item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
                        sb.Append(@"\item ");
                        foreach (var subBlock in listItem)
                        {
                            if (subBlock is ParagraphBlock p)
                            {
                                RenderInlines(p.Inline!, sb);
                            }
                            else
                            {
                                RenderBlock(subBlock, sb, lineWidthPt - ListIndentPt);
                            }
                        }
                        sb.AppendLine();
                    }
                }
                sb.AppendLine($@"\end{{{env}}}");
                sb.AppendLine();
                break;

            case ThematicBreakBlock:
                sb.AppendLine(@"\noindent\rule{\textwidth}{0.4pt}");
                sb.AppendLine();
                break;

            case AlertBlock alert:
                RenderAlertBlock(alert, sb, lineWidthPt);
                break;

            case QuoteBlock quote:
                RenderQuoteBlock(quote, sb, lineWidthPt);
                break;

            case Table table:
                RenderTable(table, sb, lineWidthPt);
                break;

            default:
                if (block is ContainerBlock container)
                {
                    foreach (var child in container)
                    {
                        RenderBlock(child, sb, lineWidthPt);
                    }
                }
                break;
        }
    }

    private static readonly Dictionary<string, string> AlertKindToEnv =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NOTE"] = "callout-note",
            ["WARNING"] = "callout-warning",
            ["TIP"] = "callout-tip",
            ["IMPORTANT"] = "callout-important",
            ["CAUTION"] = "callout-caution",
        };

    private static void RenderAlertBlock(AlertBlock alert, StringBuilder sb, double lineWidthPt)
    {
        var kind = alert.Kind.ToString().Trim();
        var envName = AlertKindToEnv.TryGetValue(kind, out var mapped) ? mapped : "callout-note";

        sb.AppendLine($@"\begin{{{envName}}}");

        foreach (var subBlock in alert)
        {
            RenderBlock(subBlock, sb, lineWidthPt - CalloutInsetPt);
        }

        sb.AppendLine($@"\end{{{envName}}}");
        sb.AppendLine();
    }

    private static void RenderQuoteBlock(QuoteBlock quote, StringBuilder sb, double lineWidthPt)
    {
        // Check for attribution (— Author) in last paragraph
        var (hasAttribution, author) = AttributionDetector.Detect(quote);

        // Render as styled blockquote with tcolorbox
        sb.AppendLine(@"\begin{mdquote}");
        sb.AppendLine(@"\itshape");

        int blockCount = quote.Count;
        int index = 0;
        foreach (var subBlock in quote)
        {
            index++;
            // Skip the attribution paragraph — we render it separately
            if (hasAttribution && index == blockCount)
            {
                sb.AppendLine(@"\normalfont");
                sb.AppendLine($@"\hfill\textit{{--- {EscapeLatex(author!)}}}");
                continue;
            }

            RenderBlock(subBlock, sb, lineWidthPt - QuoteInsetPt);
        }

        sb.AppendLine(@"\end{mdquote}");
        sb.AppendLine();
    }

    // What the containers defined in the preamble take away from the line of their content:
    // mdquote (left skip 4pt + left 12pt + right 8pt), a callout (left 8pt + right 8pt + two
    // 0.8pt rules + tcolorbox's 1mm box separation on either side) and a list level
    // (\leftmargini, 2.5em of the 10.95pt body font).
    internal const double QuoteInsetPt = 24;
    internal const double CalloutInsetPt = 23.3;
    internal const double ListIndentPt = 27.4;

    private static void RenderTable(Table table, StringBuilder sb, double lineWidthPt)
    {
        var columnCount = 0;
        var rowCount = 0;
        foreach (var row in table)
        {
            if (row is TableRow tr)
            {
                columnCount = Math.Max(columnCount, tr.Count);
                rowCount++;
            }
        }

        if (columnCount == 0) return;

        // p{width} columns sized from the cell contents. The widths are shares of what the
        // line has left after cell padding and rules, so the table cannot exceed the line.
        var rowCharacters = new List<long>(rowCount);
        var plan = PlanTable(table, columnCount, lineWidthPt, rowCharacters);

        var alignments = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            var align = i < table.ColumnDefinitions.Count
                ? table.ColumnDefinitions[i].Alignment
                : (TableColumnAlign?)null;

            var colWidth = plan.ColumnWidth(i);

            // >{\raggedright\arraybackslash} etc. control text alignment within p{} columns;
            // \hspace{0pt} lets TeX hyphenate the first word of a cell
            alignments[i] = align switch
            {
                TableColumnAlign.Center => $@">{{\centering\arraybackslash\hspace{{0pt}}}}p{{{colWidth}}}",
                TableColumnAlign.Right => $@">{{\raggedleft\arraybackslash\hspace{{0pt}}}}p{{{colWidth}}}",
                _ => $@">{{\raggedright\arraybackslash\hspace{{0pt}}}}p{{{colWidth}}}"
            };
        }

        // Settings that hold for this table only
        var chunkRows = LatexTableLayout.ChunkRows(columnCount, rowCharacters);
        bool hasSettings = plan.CellPadding != null || chunkRows != null;
        if (hasSettings)
        {
            sb.AppendLine(@"\begingroup");
            if (plan.CellPadding != null)
            {
                // Too many columns for the default cell padding
                sb.AppendLine($@"\setlength{{\tabcolsep}}{{{plan.CellPadding}}}");
            }
            if (chunkRows != null)
            {
                // A large table is typeset a few rows at a time, which bounds TeX's memory use
                sb.AppendLine($@"\LTchunksize={chunkRows}\relax");
            }
        }

        sb.AppendLine(@"\begin{longtable}{" + string.Join(plan.ColumnSeparator, alignments) + "}");
        sb.AppendLine(@"\hline");

        bool isHeader = true;
        foreach (var row in table)
        {
            if (row is TableRow tableRow)
            {
                var cells = new List<string>();
                foreach (var cell in tableRow)
                {
                    if (cell is TableCell tableCell)
                    {
                        var breaks = PlanCellBreaks(
                            CollectCellPieces(tableCell, bold: isHeader), plan.ColumnWidthEm(cells.Count));
                        var cellSb = new StringBuilder();
                        foreach (var subBlock in tableCell)
                        {
                            if (subBlock is ParagraphBlock p)
                            {
                                RenderInlines(p.Inline!, cellSb, breaks);
                            }
                        }
                        cells.Add(cellSb.ToString());
                    }
                }

                if (isHeader)
                {
                    sb.AppendLine(string.Join(" & ", cells.Select(c => $@"\textbf{{{c}}}")) + @" \\");
                    sb.AppendLine(@"\hline");
                    isHeader = false;
                }
                else
                {
                    sb.AppendLine(string.Join(" & ", cells) + @" \\");
                    sb.AppendLine(@"\hline");
                }
            }
        }

        sb.AppendLine(@"\end{longtable}");
        if (hasSettings)
        {
            sb.AppendLine(@"\endgroup");
        }
        sb.AppendLine();
    }

    // Plans the columns of a table and counts the characters of each of its rows.
    private static LatexTablePlan PlanTable(Table table, int columnCount, double lineWidthPt, List<long> rowCharacters)
    {
        var natural = new double[columnCount];
        var widestWord = new double[columnCount];
        var unbreakable = new double[columnCount];

        bool isHeader = true;
        foreach (var row in table)
        {
            if (row is TableRow tableRow)
            {
                int column = 0;
                long characters = 0;
                foreach (var cell in tableRow)
                {
                    if (cell is TableCell tableCell)
                    {
                        var pieces = CollectCellPieces(tableCell, bold: isHeader);
                        characters += pieces.Sum(piece => piece.Text.Length);
                        var measure = MeasureCell(pieces);
                        natural[column] = Math.Max(natural[column], measure.NaturalEm);
                        widestWord[column] = Math.Max(widestWord[column], measure.WidestWordEm);
                        unbreakable[column] = Math.Max(unbreakable[column], measure.UnbreakableEm);
                        column++;
                    }
                }
                rowCharacters.Add(characters);
                isHeader = false;
            }
        }

        return LatexTableLayout.Plan(natural, widestWord, unbreakable, lineWidthPt);
    }

    /// <summary>
    /// A stretch of a table cell as it is emitted: the text of one inline, or a box that
    /// takes no break opportunities, such as a formula.
    /// </summary>
    private readonly record struct CellPiece(
        Inline? Source, string Text, bool Bold, bool Monospace,
        bool IsBox = false, bool IsRigid = false, double WidthEm = 0, double PartEm = 0);

    /// <summary>
    /// One rendered character of a cell, or a box, with the place it comes from.
    /// </summary>
    private readonly record struct CellUnit(Inline? Source, int Index, char Char, double WidthEm, bool Monospace)
    {
        public bool IsBox => Source is null;

        public bool IsWord => IsBox || char.IsLetterOrDigit(Char);
    }

    /// <summary>
    /// Where the text of a table cell may break: for each inline, the marks to insert and
    /// the index of the character each one goes in front of, in ascending order.
    /// </summary>
    private sealed class CellBreaks
    {
        private readonly Dictionary<Inline, List<(int Index, string Mark)>> _marks = new();

        public void Add(Inline source, int index, string mark)
        {
            if (!_marks.TryGetValue(source, out var marks))
            {
                _marks[source] = marks = new List<(int, string)>();
            }
            marks.Add((index, mark));
        }

        public List<(int Index, string Mark)>? For(Inline source) => _marks.GetValueOrDefault(source);
    }

    // The braces keep TeX from swallowing a space that follows the mark
    private const string BreakOpportunity = @"\allowbreak{}";

    // An unbreakable run is cut into chunks of which this many fit on a line
    private const int ChunksPerLine = 4;

    private static List<CellPiece> CollectCellPieces(TableCell cell, bool bold)
    {
        var pieces = new List<CellPiece>();
        foreach (var subBlock in cell)
        {
            if (subBlock is ParagraphBlock p)
            {
                CollectCellPieces(p.Inline!, pieces, bold);
            }
        }
        return pieces;
    }

    // Mirrors RenderInline: lists what it emits for a table cell, in order.
    private static void CollectCellPieces(ContainerInline container, List<CellPiece> pieces, bool bold)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    pieces.Add(new CellPiece(literal, literal.Content.ToString(), bold, Monospace: false));
                    break;

                case EmphasisInline emphasis:
                    CollectCellPieces(emphasis, pieces, bold || emphasis.DelimiterCount == 2);
                    break;

                case CodeInline code:
                    pieces.Add(new CellPiece(code, code.Content, bold, Monospace: true));
                    break;

                case MathInline math:
                    // Math is emitted as written; TeX breaks an inline formula only after its operators
                    var tex = math.Content.ToString();
                    var (mathWidth, mathPart) = LatexTableLayout.MathWidthEm(tex);
                    pieces.Add(Box(tex, rigid: true, mathWidth, math.DelimiterCount == 2 ? mathWidth : mathPart));
                    break;

                case CitationInline citation:
                    // The printed form is decided by the bibliography style; assume "Author, 2023",
                    // which TeX breaks at its spaces like any other text
                    pieces.Add(Box("", rigid: false, 6.2 * citation.Citations.Count, 3.2));
                    break;

                case LinkInline link:
                    if (link.IsImage)
                    {
                        pieces.Add(new CellPiece(link, ImagePlaceholder(link), bold, Monospace: false));
                    }
                    else
                    {
                        CollectCellPieces(link, pieces, bold);
                    }
                    break;

                case LineBreakInline:
                    pieces.Add(new CellPiece(null, " ", bold, Monospace: false));
                    break;

                case ContainerInline containerInline:
                    CollectCellPieces(containerInline, pieces, bold);
                    break;

                default:
                    break;
            }
        }

        // The source text of a box only counts towards the size of the table
        static CellPiece Box(string text, bool rigid, double widthEm, double partEm) =>
            new(null, text, Bold: false, Monospace: false, IsBox: true, rigid, widthEm, partEm);
    }

    private static string ImagePlaceholder(LinkInline image) => $"[Image: {image.Url}]";

    private static LatexTableLayout.CellMeasure MeasureCell(List<CellPiece> pieces)
    {
        var measure = new LatexTableLayout.CellMeasure();
        foreach (var piece in pieces)
        {
            if (piece.IsBox)
            {
                measure.AddBox(piece.WidthEm, piece.PartEm, piece.IsRigid);
                continue;
            }

            foreach (var ch in piece.Text)
            {
                if (IsBreakingSpace(ch))
                {
                    measure.AddSpace(LatexTableLayout.SpaceWidthEm(piece.Bold, piece.Monospace));
                }
                else if (!IsDropped(ch))
                {
                    measure.AddGlyph(LatexTableLayout.CharWidthEm(ch, piece.Bold, piece.Monospace));
                }
            }
        }
        return measure;
    }

    private static bool IsBreakingSpace(char ch) => ch is ' ' or '\t' or '\n' or '\r';

    // EscapeLatex keeps every character up to the end of Latin Extended-B
    private const char LastKeptCharacter = (char)0x024F;

    // Characters the escaper leaves out take no room
    private static bool IsDropped(char ch) => ch > LastKeptCharacter && EscapeLatex(ch).Length == 0;

    /// <summary>
    /// Plans the break opportunities of one table cell. A token is everything between two
    /// spaces, whatever inlines it is made of. Only a token that cannot fit its column on
    /// one line gets any, so text that fits is emitted as it always was.
    /// </summary>
    private static CellBreaks? PlanCellBreaks(List<CellPiece> pieces, double columnEm)
    {
        CellBreaks? breaks = null;
        var token = new List<CellUnit>();
        foreach (var piece in pieces)
        {
            if (piece.IsBox)
            {
                token.Add(new CellUnit(null, 0, '\0', piece.PartEm, Monospace: false));
                continue;
            }

            for (int i = 0; i < piece.Text.Length; i++)
            {
                char ch = piece.Text[i];
                if (IsBreakingSpace(ch))
                {
                    BreakToken(token, columnEm, ref breaks);
                    token.Clear();
                }
                else if (piece.Source != null && !IsDropped(ch))
                {
                    var width = LatexTableLayout.CharWidthEm(ch, piece.Bold, piece.Monospace);
                    token.Add(new CellUnit(piece.Source, i, ch, width, piece.Monospace));
                }
            }
        }

        BreakToken(token, columnEm, ref breaks);
        return breaks;
    }

    // A token that is too wide may break after a separator that follows a word ("a/b",
    // "a.b", "a_b"), before a camelCase capital and next to a formula. A piece between two
    // such places that is still too wide may break between its characters.
    private static void BreakToken(List<CellUnit> token, double columnEm, ref CellBreaks? breaks)
    {
        if (token.Count < 2 || LatexTableLayout.Fits(WidthEm(token, 0, token.Count), columnEm)) return;

        breaks ??= new CellBreaks();
        int pieceStart = 0;
        bool wordBefore = false;    // a letter, digit or formula earlier than the previous unit
        for (int i = 1; i <= token.Count; i++)
        {
            if (i < token.Count)
            {
                // "a,-1", "x=-1" and "f(-1)" break in front of the sign, which then belongs
                // to what follows, like the punctuation that opens a token
                bool newItem = wordBefore && OpensItem(token[i - 1]) && LeadsItem(token, i);
                bool mayBreak = newItem || MayBreakBefore(token, i, wordBefore);
                wordBefore = !newItem && (wordBefore || token[i - 1].IsWord);
                if (!mayBreak) continue;
            }

            BreakPiece(token, pieceStart, i, columnEm, breaks);
            pieceStart = i;
            if (i == token.Count) break;

            // The mark goes in front of the next character, or behind the last one before a formula
            if (token[i].Source is { } next)
            {
                breaks.Add(next, token[i].Index, BreakOpportunity);
            }
            else
            {
                breaks.Add(token[i - 1].Source!, token[i - 1].Index + 1, BreakOpportunity);
            }
        }
    }

    private static bool OpensItem(CellUnit unit) => !unit.IsBox && unit.Char is ',' or ';' or '=' or '(' or '[';

    // A sign, bracket, quote or sigil that starts something, or the point of ".5" and
    // ".NET". "=>" and "==" stay together.
    private static bool LeadsItem(List<CellUnit> token, int index)
    {
        var unit = token[index];
        if (unit.IsBox) return false;
        if (unit.Char == '.') return index + 1 < token.Count && char.IsLetterOrDigit(token[index + 1].Char);
        return unit.Char is '-' or '+' or '(' or '[' or '{' or '"' or '\'' or '`' or '$' or '#' or '@' or '~' or '\\' or '/';
    }

    private static bool MayBreakBefore(List<CellUnit> token, int index, bool wordBeforePrevious)
    {
        var previous = token[index - 1];
        var next = token[index];
        if (next.IsBox) return !previous.IsBox && (wordBeforePrevious || previous.IsWord);
        if (!char.IsLetterOrDigit(next.Char)) return false;
        if (previous.IsBox) return true;

        if (char.IsLetterOrDigit(previous.Char))
        {
            return char.IsUpper(next.Char) && !char.IsUpper(previous.Char);
        }

        // "1,234.50" and "3,14" are numbers, not lists
        bool insideNumber = previous.Char is '.' or ',' && char.IsDigit(next.Char)
            && index >= 2 && char.IsDigit(token[index - 2].Char);
        return wordBeforePrevious && !insideNumber;
    }

    private static void BreakPiece(List<CellUnit> token, int start, int end, double columnEm, CellBreaks breaks)
    {
        int count = end - start;
        double width = WidthEm(token, start, end);
        if (count < 2 || LatexTableLayout.Fits(width, columnEm)) return;

        // A word breaks with a hyphen; a hash, number or code fragment breaks without one
        bool isWord = true;
        int boxCount = 0;
        double boxesEm = 0;
        double widestBoxEm = 0;
        for (int i = start; i < end; i++)
        {
            var unit = token[i];
            isWord &= !unit.IsBox && !unit.Monospace && char.IsLetter(unit.Char);
            if (unit.IsBox)
            {
                boxCount++;
                boxesEm += unit.WidthEm;
                widestBoxEm = Math.Max(widestBoxEm, unit.WidthEm);
            }
        }
        var mark = isWord ? @"\-" : BreakOpportunity;

        // A line takes several chunks, so it fills well without a break mark after every
        // character. Next to a formula the characters have the room it leaves.
        double perLine = LatexTableLayout.CharactersPerLine(
            width - boxesEm, count - boxCount, Math.Max(0, columnEm - widestBoxEm));
        int step = Math.Max(1, (int)(perLine / ChunksPerLine));

        // Like TeX's own hyphenation, keep two characters before a break and three after it,
        // unless the column is too narrow to hold that many
        bool keepEnds = count >= 5 && perLine >= 3;
        int first = keepEnds ? 2 : 1;
        int last = count - (keepEnds ? 3 : 1);

        for (int i = first; i <= last; i++)
        {
            // A formula breaks only where TeX allows it and keeps the character on either
            // side of it; the rest of the punctuation around it may break
            var unit = token[start + i];
            if (i % step == 0 && !unit.IsBox && !token[start + i - 1].IsBox && !SplitsLigature(token, start + i))
            {
                breaks.Add(unit.Source!, unit.Index, mark);
            }
        }
    }

    // "--", "---", "``", "''", ",,", "<<" and ">>" are drawn as one glyph where nothing is
    // emitted between the characters: inside one inline, or from one piece of plain text to
    // the next. A mark inside would change it. A longer run is a rule and may break.
    private static bool SplitsLigature(List<CellUnit> token, int index)
    {
        var unit = token[index];
        if (unit.Char is not ('-' or '`' or '\'' or ',' or '<' or '>')) return false;

        int before = 0;
        for (int i = index - 1; i >= 0 && before < 3 && Continues(token[i]); i--) before++;
        int after = 0;
        for (int i = index; i < token.Count && after < 3 && Continues(token[i]); i++) after++;
        return before > 0 && before + after <= 3;

        bool Continues(CellUnit other) =>
            other.Char == unit.Char
            && (other.Source == unit.Source || (other.Source is LiteralInline && unit.Source is LiteralInline));
    }

    private static double WidthEm(List<CellUnit> token, int start, int end)
    {
        double width = 0;
        for (int i = start; i < end; i++)
        {
            width += token[i].WidthEm;
        }
        return width;
    }

    // Escapes the text of an inline. Inside a table cell it also inserts the break marks planned for it.
    private static void AppendText(StringBuilder sb, Inline inline, string text, CellBreaks? cell)
    {
        var marks = cell?.For(inline);
        if (marks == null)
        {
            sb.Append(EscapeLatex(text));
            return;
        }

        int next = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            while (next < marks.Count && marks[next].Index == i)
            {
                sb.Append(marks[next++].Mark);
            }
            if (i < text.Length) sb.Append(EscapeLatex(text[i]));
        }
    }

    private static void RenderInlines(ContainerInline container, StringBuilder sb, CellBreaks? cell = null)
    {
        foreach (var inline in container)
        {
            RenderInline(inline, sb, cell);
        }
    }

    private static void RenderInline(Inline inline, StringBuilder sb, CellBreaks? cell = null)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AppendText(sb, literal, literal.Content.ToString(), cell);
                break;

            case EmphasisInline emphasis:
                var tag = emphasis.DelimiterCount == 2 ? "textbf" : "textit";
                sb.Append($@"\{tag}{{");
                RenderInlines(emphasis, sb, cell);
                sb.Append("}");
                break;

            case CodeInline code:
                sb.Append(@"\texttt{");
                AppendText(sb, code, code.Content, cell);
                sb.Append('}');
                break;

            case MathInline math:
                if (math.DelimiterCount == 2)
                {
                    // Display math: $$...$$ → \[...\]
                    sb.AppendLine();
                    sb.AppendLine($@"\[{math.Content}\]");
                    sb.AppendLine();
                }
                else
                {
                    // Inline math: $...$ → $...$
                    sb.Append($"${math.Content}$");
                }
                break;

            case CitationInline citation:
                if (citation.Citations.Count == 1 && citation.Citations[0].Locator != null)
                {
                    sb.Append($@"\cite[{EscapeLatex(citation.Citations[0].Locator!)}]{{{citation.Citations[0].Key}}}");
                }
                else
                {
                    var keys = string.Join(",", citation.Citations.Select(c => c.Key));
                    sb.Append($@"\cite{{{keys}}}");
                }
                break;

            case LinkInline link:
                if (link.IsImage)
                {
                    sb.Append(@"\textit{");
                    AppendText(sb, link, ImagePlaceholder(link), cell);
                    sb.Append('}');
                }
                else
                {
                    sb.Append($@"\href{{{link.Url}}}{{");
                    RenderInlines(link, sb, cell);
                    sb.Append("}");
                }
                break;

            case LineBreakInline:
                sb.Append(@"\\");
                sb.AppendLine();
                break;

            case ContainerInline containerInline:
                RenderInlines(containerInline, sb, cell);
                break;

            default:
                break;
        }
    }

    private static string SanitizeCodeContent(string content)
    {
        var sb = new StringBuilder(content.Length);
        foreach (var ch in content)
        {
            if (ch <= '\u007F') // ASCII — always safe in listings
            {
                sb.Append(ch);
                continue;
            }

            sb.Append(ch switch
            {
                // Box-drawing characters → ASCII equivalents
                '═' or '─' or '━' => "-",
                '║' or '│' or '┃' => "|",
                '╔' or '╗' or '╚' or '╝' or '┌' or '┐' or '└' or '┘' => "+",
                '╠' or '╣' or '╦' or '╩' or '╬' or '├' or '┤' or '┬' or '┴' or '┼' => "+",
                // Arrows
                '→' or '▸' or '▶' => ">",
                '←' or '◂' or '◀' => "<",
                '↔' => "<->",
                // Latin accented characters (U+00C0–U+024F) — covered by literate mappings
                _ when ch >= '\u00C0' && ch <= '\u024F' => ch.ToString(),
                // Everything else — drop to prevent listings crash
                _ => ""
            });
        }
        return sb.ToString();
    }

    private static string EscapeLatex(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            result.Append(EscapeLatex(ch));
        }
        return result.ToString();
    }

    private static string EscapeLatex(char ch)
    {
        var s = ch.ToString();
        if (UnicodeMap.TryGetValue(s, out var mapped))
        {
            return mapped;
        }

        return ch switch
        {
            '\\' => @"\textbackslash{}",
            '$' => @"\$",
            '&' => @"\&",
            '%' => @"\%",
            '#' => @"\#",
            '_' => @"\_",
            '{' => @"\{",
            '}' => @"\}",
            '~' => @"\textasciitilde{}",
            '^' => @"\textasciicircum{}",
            // Drop characters above Latin Extended-B (U+024F) that have no mapping
            // This prevents pdflatex from crashing on unsupported Unicode
            _ when ch > '\u024F' => "",
            _ => s
        };
    }
}
