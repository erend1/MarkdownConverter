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

    private static void RenderBlock(Block block, StringBuilder sb)
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
                                RenderBlock(subBlock, sb);
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
                RenderAlertBlock(alert, sb);
                break;

            case QuoteBlock quote:
                RenderQuoteBlock(quote, sb);
                break;

            case Table table:
                RenderTable(table, sb);
                break;

            default:
                if (block is ContainerBlock container)
                {
                    foreach (var child in container)
                    {
                        RenderBlock(child, sb);
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

    private static void RenderAlertBlock(AlertBlock alert, StringBuilder sb)
    {
        var kind = alert.Kind.ToString().Trim();
        var envName = AlertKindToEnv.TryGetValue(kind, out var mapped) ? mapped : "callout-note";

        sb.AppendLine($@"\begin{{{envName}}}");

        foreach (var subBlock in alert)
        {
            RenderBlock(subBlock, sb);
        }

        sb.AppendLine($@"\end{{{envName}}}");
        sb.AppendLine();
    }

    private static void RenderQuoteBlock(QuoteBlock quote, StringBuilder sb)
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

            RenderBlock(subBlock, sb);
        }

        sb.AppendLine(@"\end{mdquote}");
        sb.AppendLine();
    }

    private static void RenderTable(Table table, StringBuilder sb)
    {
        var columnCount = 0;
        foreach (var row in table)
        {
            if (row is TableRow tr)
            {
                columnCount = Math.Max(columnCount, tr.Count);
            }
        }

        if (columnCount == 0) return;

        // p{width} columns sized from the cell contents. The widths are shares of what the
        // line has left after cell padding and rules, so the table cannot exceed the line.
        var plan = PlanTable(table, columnCount);

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

        if (plan.CellPadding != null)
        {
            // Too many columns for the default cell padding: tighten it for this table only
            sb.AppendLine(@"\begingroup");
            sb.AppendLine($@"\setlength{{\tabcolsep}}{{{plan.CellPadding}}}");
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
                        var context = new CellContext(plan.ColumnWidthEm(cells.Count), Bold: isHeader);
                        var cellSb = new StringBuilder();
                        foreach (var subBlock in tableCell)
                        {
                            if (subBlock is ParagraphBlock p)
                            {
                                RenderInlines(p.Inline!, cellSb, context);
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
        if (plan.CellPadding != null)
        {
            sb.AppendLine(@"\endgroup");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// A table cell being rendered: the estimated width of its column, in em of the body
    /// font, and the font its text is set in.
    /// </summary>
    private readonly record struct CellContext(double WidthEm, bool Bold, bool Monospace = false);

    private const string BreakOpportunity = @"\allowbreak ";

    // An unbreakable run is cut into chunks of which this many fit on a line
    private const int ChunksPerLine = 4;

    private static LatexTablePlan PlanTable(Table table, int columnCount)
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
                foreach (var cell in tableRow)
                {
                    if (cell is TableCell tableCell)
                    {
                        var measure = new LatexTableLayout.CellMeasure();
                        foreach (var subBlock in tableCell)
                        {
                            if (subBlock is ParagraphBlock p)
                            {
                                MeasureInlines(p.Inline!, measure, bold: isHeader);
                            }
                        }

                        natural[column] = Math.Max(natural[column], measure.NaturalEm);
                        widestWord[column] = Math.Max(widestWord[column], measure.WidestWordEm);
                        unbreakable[column] = Math.Max(unbreakable[column], measure.UnbreakableEm);
                        column++;
                    }
                }
                isHeader = false;
            }
        }

        return LatexTableLayout.Plan(natural, widestWord, unbreakable);
    }

    // Mirrors RenderInline: estimates the width of what it would emit for a table cell.
    private static void MeasureInlines(ContainerInline container, LatexTableLayout.CellMeasure measure, bool bold)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    MeasureText(literal.Content.ToString(), measure, bold, monospace: false);
                    break;

                case EmphasisInline emphasis:
                    MeasureInlines(emphasis, measure, bold || emphasis.DelimiterCount == 2);
                    break;

                case CodeInline code:
                    MeasureText(code.Content, measure, bold, monospace: true);
                    break;

                case MathInline math:
                    // Math is emitted as written; TeX breaks an inline formula only after its operators
                    var (mathWidth, mathPart) = LatexTableLayout.MathWidthEm(math.Content.ToString());
                    measure.AddBox(mathWidth, math.DelimiterCount == 2 ? mathWidth : mathPart);
                    break;

                case CitationInline citation:
                    // The printed form is decided by the bibliography style; assume "Author, 2023"
                    foreach (var _ in citation.Citations)
                    {
                        MeasureText(" Author, 2023", measure, bold, monospace: false);
                    }
                    break;

                case LinkInline link:
                    if (link.IsImage)
                    {
                        MeasureText($"[Image: {link.Url}]", measure, bold, monospace: false);
                    }
                    else
                    {
                        MeasureInlines(link, measure, bold);
                    }
                    break;

                case LineBreakInline:
                    measure.AddSpace(LatexTableLayout.SpaceWidthEm(bold, monospace: false));
                    break;

                case ContainerInline containerInline:
                    MeasureInlines(containerInline, measure, bold);
                    break;

                default:
                    break;
            }
        }
    }

    private static void MeasureText(string text, LatexTableLayout.CellMeasure measure, bool bold, bool monospace)
    {
        foreach (var ch in text)
        {
            if (IsBreakingSpace(ch))
            {
                measure.AddSpace(LatexTableLayout.SpaceWidthEm(bold, monospace));
            }
            else if (!IsDropped(ch))
            {
                measure.AddGlyph(LatexTableLayout.CharWidthEm(ch, bold, monospace));
            }
        }
    }

    private static double CellTextWidthEm(ReadOnlySpan<char> text, CellContext cell)
    {
        double width = 0;
        foreach (var ch in text)
        {
            if (!IsDropped(ch))
            {
                width += LatexTableLayout.CharWidthEm(ch, cell.Bold, cell.Monospace);
            }
        }
        return width;
    }

    private static bool IsBreakingSpace(char ch) => ch is ' ' or '\t' or '\n' or '\r';

    // Characters the escaper leaves out take no room; it keeps everything up to Latin Extended-B
    private static bool IsDropped(char ch) => ch > '\u024F' && EscapeLatex(ch).Length == 0;

    /// <summary>
    /// Escapes table-cell text. A token that cannot fit its column on one line may break
    /// after a separator or before a camelCase capital; a piece that is still too wide may
    /// break between any two characters. Nothing is added to text that fits.
    /// </summary>
    private static void AppendCellText(StringBuilder sb, string text, CellContext cell)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (IsBreakingSpace(text[i]))
            {
                sb.Append(text[i++]);
                continue;
            }

            int start = i;
            while (i < text.Length && !IsBreakingSpace(text[i])) i++;
            AppendCellToken(sb, text.AsSpan(start, i - start), cell);
        }
    }

    private static void AppendCellToken(StringBuilder sb, ReadOnlySpan<char> token, CellContext cell)
    {
        if (LatexTableLayout.Fits(CellTextWidthEm(token, cell), cell.WidthEm))
        {
            AppendEscaped(sb, token);
            return;
        }

        int firstAlphanumeric = 0;
        while (firstAlphanumeric < token.Length && !char.IsLetterOrDigit(token[firstAlphanumeric]))
        {
            firstAlphanumeric++;
        }

        int pieceStart = 0;
        for (int i = 1; i <= token.Length; i++)
        {
            if (i < token.Length && !IsBreakOpportunity(token[i - 1], token[i], i - 1 > firstAlphanumeric))
            {
                continue;
            }

            if (pieceStart > 0) sb.Append(BreakOpportunity);
            AppendCellPiece(sb, token[pieceStart..i], cell);
            pieceStart = i;
        }
    }

    // "a/b", "a.b", "a_b" may break after the separator that follows a word,
    // "camelCase" before the capital.
    private static bool IsBreakOpportunity(char previous, char next, bool previousFollowsWord)
    {
        if (!char.IsLetterOrDigit(next)) return false;

        return char.IsLetterOrDigit(previous)
            ? char.IsUpper(next) && !char.IsUpper(previous)
            : previousFollowsWord;
    }

    private static void AppendCellPiece(StringBuilder sb, ReadOnlySpan<char> piece, CellContext cell)
    {
        var width = CellTextWidthEm(piece, cell);
        if (LatexTableLayout.Fits(width, cell.WidthEm))
        {
            AppendEscaped(sb, piece);
            return;
        }

        // A word breaks with a hyphen; a hash, number or code fragment breaks without one
        bool isWord = !cell.Monospace;
        var rendered = new List<string>(piece.Length);
        foreach (var ch in piece)
        {
            var escaped = EscapeLatex(ch);
            if (escaped.Length == 0) continue;

            isWord &= char.IsLetter(ch);
            rendered.Add(escaped);
        }

        var breakMark = isWord ? @"\-" : BreakOpportunity;

        // A line takes several chunks, so it fills well without a break mark after every character
        double perLine = LatexTableLayout.CharactersPerLine(width, rendered.Count, cell.WidthEm);
        int step = Math.Max(1, (int)(perLine / ChunksPerLine));

        // Like TeX's own hyphenation, keep two characters before a break and three after it,
        // unless the column is too narrow to hold that many
        bool keepEnds = rendered.Count >= 5 && perLine >= 3;
        int first = keepEnds ? 2 : 1;
        int last = rendered.Count - (keepEnds ? 3 : 1);

        for (int i = 0; i < rendered.Count; i++)
        {
            if (i % step == 0 && i >= first && i <= last) sb.Append(breakMark);
            sb.Append(rendered[i]);
        }
    }

    private static void AppendEscaped(StringBuilder sb, ReadOnlySpan<char> text)
    {
        foreach (var ch in text)
        {
            sb.Append(EscapeLatex(ch));
        }
    }

    private static void RenderInlines(ContainerInline container, StringBuilder sb, CellContext? cell = null)
    {
        foreach (var inline in container)
        {
            RenderInline(inline, sb, cell);
        }
    }

    private static void RenderInline(Inline inline, StringBuilder sb, CellContext? cell = null)
    {
        switch (inline)
        {
            case LiteralInline literal:
                if (cell is { } literalCell)
                    AppendCellText(sb, literal.Content.ToString(), literalCell);
                else
                    sb.Append(EscapeLatex(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                var tag = emphasis.DelimiterCount == 2 ? "textbf" : "textit";
                sb.Append($@"\{tag}{{");
                RenderInlines(emphasis, sb,
                    cell is { } emphasisCell && emphasis.DelimiterCount == 2 ? emphasisCell with { Bold = true } : cell);
                sb.Append("}");
                break;

            case CodeInline code:
                if (cell is { } codeCell)
                {
                    sb.Append(@"\texttt{");
                    AppendCellText(sb, code.Content, codeCell with { Monospace = true });
                    sb.Append('}');
                }
                else
                {
                    sb.Append($@"\texttt{{{EscapeLatex(code.Content)}}}");
                }
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
                    if (cell is { } imageCell)
                    {
                        sb.Append(@"\textit{");
                        AppendCellText(sb, $"[Image: {link.Url}]", imageCell);
                        sb.Append('}');
                    }
                    else
                    {
                        sb.Append($@"\textit{{[Image: {EscapeLatex(link.Url ?? "")}]}}");
                    }
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
