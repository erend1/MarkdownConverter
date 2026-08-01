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

        // Use p{width} columns to bound table within page width
        // Subtract estimated border/padding overhead per column
        var colWidth = $"{1.0 / columnCount:F2}\\textwidth";

        var alignments = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            var align = i < table.ColumnDefinitions.Count
                ? table.ColumnDefinitions[i].Alignment
                : (TableColumnAlign?)null;

            // >{\raggedright\arraybackslash} etc. control text alignment within p{} columns
            alignments[i] = align switch
            {
                TableColumnAlign.Center => $@">{{\centering\arraybackslash}}p{{{colWidth}}}",
                TableColumnAlign.Right => $@">{{\raggedleft\arraybackslash}}p{{{colWidth}}}",
                _ => $@">{{\raggedright\arraybackslash}}p{{{colWidth}}}"
            };
        }

        sb.AppendLine(@"\begin{longtable}{" + string.Join(" | ", alignments) + "}");
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
                        var cellSb = new StringBuilder();
                        foreach (var subBlock in tableCell)
                        {
                            if (subBlock is ParagraphBlock p)
                            {
                                RenderInlines(p.Inline!, cellSb);
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
        sb.AppendLine();
    }

    private static void RenderInlines(ContainerInline container, StringBuilder sb)
    {
        foreach (var inline in container)
        {
            RenderInline(inline, sb);
        }
    }

    private static void RenderInline(Inline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case LiteralInline literal:
                sb.Append(EscapeLatex(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                var tag = emphasis.DelimiterCount == 2 ? "textbf" : "textit";
                sb.Append($@"\{tag}{{");
                RenderInlines(emphasis, sb);
                sb.Append("}");
                break;

            case CodeInline code:
                sb.Append($@"\texttt{{{EscapeLatex(code.Content)}}}");
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
                    sb.Append($@"\textit{{[Image: {EscapeLatex(link.Url ?? "")}]}}");
                }
                else
                {
                    sb.Append($@"\href{{{link.Url}}}{{");
                    RenderInlines(link, sb);
                    sb.Append("}");
                }
                break;

            case LineBreakInline:
                sb.Append(@"\\");
                sb.AppendLine();
                break;

            case ContainerInline containerInline:
                RenderInlines(containerInline, sb);
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
            var s = ch.ToString();
            if (UnicodeMap.TryGetValue(s, out var mapped))
            {
                result.Append(mapped);
                continue;
            }

            result.Append(ch switch
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
            });
        }
        return result.ToString();
    }
}
