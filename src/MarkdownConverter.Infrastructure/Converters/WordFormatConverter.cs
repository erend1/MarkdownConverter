using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.Bibliography;
using MarkdownConverter.Infrastructure.MarkdigExtensions;
using MParagraphBlock = Markdig.Syntax.ParagraphBlock;

using WTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WRunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using WBreak = DocumentFormat.OpenXml.Wordprocessing.Break;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;
using MParagraph = Markdig.Syntax.ParagraphBlock;
using MTable = Markdig.Extensions.Tables.Table;
using MTableRow = Markdig.Extensions.Tables.TableRow;
using MTableCell = Markdig.Extensions.Tables.TableCell;

namespace MarkdownConverter.Infrastructure.Converters;

public sealed class WordFormatConverter : IFormatConverter
{
    private readonly IFileSystem _fileSystem;

    public WordFormatConverter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public ExportFormat Format => ExportFormat.Word;

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Path.GetDirectoryName(request.OutputFilePath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMathematics()
            .UsePipeTables()
            .Use(new CitationExtension())
            .Build();

        var markdownDoc = Markdig.Markdown.Parse(request.Document.RawMarkdown, pipeline);

        // Load bibliography if available
        IReadOnlyDictionary<string, BibEntry>? bibEntries = null;
        request.Options.TryGetValue("bibliography", out var bibPath);
        if (!string.IsNullOrEmpty(bibPath) && _fileSystem.FileExists(bibPath))
        {
            var bibContent = await _fileSystem.ReadAllTextAsync(bibPath, cancellationToken);
            bibEntries = BibTexParser.Parse(bibContent);
        }

        using (var stream = _fileSystem.CreateFileStream(request.OutputFilePath, FileMode.Create))
        using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            AddStyleDefinitions(mainPart);

            // Add bibliography sources as CustomXmlPart
            if (bibEntries != null && bibEntries.Count > 0)
            {
                AddBibliographySources(mainPart, bibEntries);
            }

            foreach (var block in markdownDoc)
            {
                RenderBlock(block, body);
            }

            // Add bibliography field at end of document
            if (bibEntries != null && bibEntries.Count > 0)
            {
                AddBibliographyField(body);
            }

            mainPart.Document.Save();
        }

        var size = _fileSystem.GetFileSize(request.OutputFilePath);
        return await Task.FromResult(ConversionResult.Ok(request.OutputFilePath, size));
    }

    private static void AddBibliographySources(
        MainDocumentPart mainPart,
        IReadOnlyDictionary<string, BibEntry> entries)
    {
        var sourcesXml = BibEntryToOpenXmlConverter.ToSourcesXml(entries);
        var customXmlPart = mainPart.AddCustomXmlPart(CustomXmlPartType.CustomXml);
        using var stream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(sourcesXml));
        customXmlPart.FeedData(stream);
    }

    private static void AddBibliographyField(Body body)
    {
        // Add "References" heading
        var heading = new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "Heading1" }
            ),
            new WRun(
                new WRunProperties(new Bold()),
                new WText("References")
            )
        );
        body.Append(heading);

        // Add BIBLIOGRAPHY field code
        var bibPara = new Paragraph();
        bibPara.Append(new WRun(new FieldChar { FieldCharType = FieldCharValues.Begin }));
        bibPara.Append(new WRun(
            new FieldCode(" BIBLIOGRAPHY ") { Space = SpaceProcessingModeValues.Preserve }));
        bibPara.Append(new WRun(new FieldChar { FieldCharType = FieldCharValues.Separate }));
        bibPara.Append(new WRun(
            new WText("[Update fields to show bibliography]") { Space = SpaceProcessingModeValues.Preserve }));
        bibPara.Append(new WRun(new FieldChar { FieldCharType = FieldCharValues.End }));
        body.Append(bibPara);
    }

    private static void AddStyleDefinitions(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Heading styles 1-5
        for (int i = 1; i <= 5; i++)
        {
            var fontSize = i switch
            {
                1 => "36",  // 18pt
                2 => "32",  // 16pt
                3 => "28",  // 14pt
                4 => "26",  // 13pt
                _ => "24"   // 12pt
            };

            styles.Append(new Style(
                new StyleName { Val = $"heading {i}" },
                new StyleRunProperties(
                    new Bold(),
                    new FontSize { Val = fontSize }
                )
            )
            { Type = StyleValues.Paragraph, StyleId = $"Heading{i}" });
        }

        // Code style
        styles.Append(new Style(
            new StyleName { Val = "Code" },
            new StyleRunProperties(
                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                new FontSize { Val = "20" }  // 10pt
            )
        )
        { Type = StyleValues.Character, StyleId = "CodeChar" });

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
    }

    private static void RenderBlock(Block block, OpenXmlCompositeElement parent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(heading, parent);
                break;

            case MParagraph paragraph:
                RenderParagraph(paragraph, parent);
                break;

            case MathBlock mathBlock:
                RenderMathBlock(mathBlock, parent);
                break;

            case FencedCodeBlock codeBlock:
                RenderCodeBlock(codeBlock, parent);
                break;

            case ListBlock list:
                RenderList(list, parent);
                break;

            case ThematicBreakBlock:
                RenderHorizontalRule(parent);
                break;

            case AlertBlock alert:
                RenderAlert(alert, parent);
                break;

            case QuoteBlock quote:
                RenderQuote(quote, parent, 0);
                break;

            case MTable table:
                RenderTable(table, parent);
                break;

            default:
                if (block is ContainerBlock container)
                {
                    foreach (var child in container)
                    {
                        RenderBlock(child, parent);
                    }
                }
                break;
        }
    }

    private static void RenderHeading(HeadingBlock heading, OpenXmlCompositeElement parent)
    {
        var para = new Paragraph();
        para.ParagraphProperties = new ParagraphProperties(
            new ParagraphStyleId { Val = $"Heading{heading.Level}" }
        );

        if (heading.Inline != null)
        {
            RenderInlines(heading.Inline, para);
        }

        parent.Append(para);
    }

    private static void RenderParagraph(MParagraph paragraph, OpenXmlCompositeElement parent)
    {
        var para = new Paragraph();

        if (paragraph.Inline != null)
        {
            RenderInlines(paragraph.Inline, para);
        }

        parent.Append(para);
    }

    private static void RenderMathBlock(MathBlock mathBlock, OpenXmlCompositeElement parent)
    {
        var mathContent = mathBlock.Lines.ToString().Trim();
        try
        {
            var oMathPara = LatexMathConverter.ToDisplayMath(mathContent);
            parent.Append(oMathPara);
        }
        catch
        {
            // Fallback: render as code paragraph if OMML conversion fails
            var para = new Paragraph(
                new WRun(
                    new WRunProperties(
                        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                        new Italic()
                    ),
                    new WText($"$${mathContent}$$") { Space = SpaceProcessingModeValues.Preserve }
                )
            );
            parent.Append(para);
        }
    }

    private static void RenderCodeBlock(FencedCodeBlock codeBlock, OpenXmlCompositeElement parent)
    {
        var table = new WTable();

        var tableProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }
            ),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        table.Append(tableProps);

        var row = new WTableRow();
        var cell = new WTableCell();

        var cellProps = new TableCellProperties(
            new Shading
            {
                Val = ShadingPatternValues.Clear,
                Color = "auto",
                Fill = "F5F5F5"
            },
            new TableCellMargin(
                new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new StartMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new EndMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
            )
        );
        cell.Append(cellProps);

        var lines = codeBlock.Lines.ToString().TrimEnd().Split('\n');
        foreach (var line in lines)
        {
            var para = new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
                ),
                new WRun(
                    new WRunProperties(
                        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                        new FontSize { Val = "20" },
                        new Color { Val = "333333" }
                    ),
                    new WText(line) { Space = SpaceProcessingModeValues.Preserve }
                )
            );
            cell.Append(para);
        }

        row.Append(cell);
        table.Append(row);
        parent.Append(table);

        // Spacing after code block
        parent.Append(new Paragraph());
    }

    private static void RenderList(ListBlock list, OpenXmlCompositeElement parent)
    {
        int index = 1;
        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                var para = new Paragraph();

                // Add bullet or number prefix
                var prefix = list.IsOrdered ? $"{index}. " : "\u2022 "; // bullet char
                var prefixRun = new WRun(
                    new WText(prefix) { Space = SpaceProcessingModeValues.Preserve }
                );
                para.Append(prefixRun);

                // Render inline content from child paragraphs
                foreach (var subBlock in listItem)
                {
                    if (subBlock is MParagraph p && p.Inline != null)
                    {
                        RenderInlines(p.Inline, para);
                    }
                }

                parent.Append(para);
                index++;
            }
        }
    }

    private static void RenderHorizontalRule(OpenXmlCompositeElement parent)
    {
        var para = new Paragraph(
            new ParagraphProperties(
                new ParagraphBorders(
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 6,
                        Space = 1,
                        Color = "999999"
                    }
                )
            )
        );
        parent.Append(para);
    }

    // Alert color definitions: (border, background, icon, label)
    private static readonly Dictionary<string, (string Border, string Background, string Icon, string Label)> AlertColors =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["NOTE"]      = ("4493F8", "DBE9FE", "\u2139\uFE0F", "Note"),
        ["WARNING"]   = ("D29922", "FFF4CC", "\u26A0\uFE0F", "Warning"),
        ["TIP"]       = ("3FB950", "D4EDDA", "\u2714",       "Tip"),
        ["IMPORTANT"] = ("A371F7", "E8D5FF", "\u2757",       "Important"),
        ["CAUTION"]   = ("F85149", "FDDEDE", "\u26D4",       "Caution"),
    };

    private static readonly string[] NestingBorderColors = ["CCCCCC", "AAAAAA", "888888", "666666"];

    private static void RenderQuote(QuoteBlock quote, OpenXmlCompositeElement parent, int nestingLevel)
    {
        // Check for attribution
        var (hasAttribution, author) = AttributionDetector.Detect(quote);

        // Render styled blockquote with nesting support
        var borderColor = NestingBorderColors[Math.Min(nestingLevel, NestingBorderColors.Length - 1)];
        var indent = (720 * (nestingLevel + 1)).ToString();

        int blockCount = quote.Count;
        int index = 0;
        foreach (var subBlock in quote)
        {
            index++;

            // Attribution: render last paragraph right-aligned italic
            if (hasAttribution && index == blockCount && subBlock is MParagraph attrPara)
            {
                var para = new Paragraph();
                para.ParagraphProperties = new ParagraphProperties(
                    new Indentation { Left = indent },
                    new Justification { Val = JustificationValues.Right },
                    new ParagraphBorders(
                        new LeftBorder { Val = BorderValues.Single, Size = 12, Space = 4, Color = borderColor }
                    )
                );
                if (attrPara.Inline != null)
                {
                    RenderInlines(attrPara.Inline, para, italic: true);
                }
                parent.Append(para);
                continue;
            }

            // Nested blockquote
            if (subBlock is QuoteBlock nestedQuote)
            {
                RenderQuote(nestedQuote, parent, nestingLevel + 1);
                continue;
            }

            if (subBlock is MParagraph p)
            {
                var para = new Paragraph();
                para.ParagraphProperties = new ParagraphProperties(
                    new Indentation { Left = indent },
                    new Shading
                    {
                        Val = ShadingPatternValues.Clear,
                        Color = "auto",
                        Fill = "F5F5F5"
                    },
                    new ParagraphBorders(
                        new LeftBorder { Val = BorderValues.Single, Size = 12, Space = 4, Color = borderColor }
                    )
                );

                if (p.Inline != null)
                {
                    RenderInlines(p.Inline, para, italic: true);
                }
                parent.Append(para);
            }
            else
            {
                RenderBlock(subBlock, parent);
            }
        }
    }

    private static void RenderAlert(AlertBlock alert, OpenXmlCompositeElement parent)
    {
        var kind = alert.Kind.ToString().Trim();
        var (borderColor, bgColor, icon, label) = AlertColors.TryGetValue(kind, out var colors)
            ? colors
            : ("4493F8", "DBE9FE", "\u2139\uFE0F", kind);

        var table = new WTable();
        var tableProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = borderColor },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = borderColor },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = borderColor },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = borderColor }
            ),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        table.Append(tableProps);

        var row = new WTableRow();
        var cell = new WTableCell();
        cell.Append(new TableCellProperties(
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = bgColor },
            new TableCellMargin(
                new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new StartMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new EndMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
            )
        ));

        // Title paragraph with icon
        var titlePara = new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { After = "60" }
            ),
            new WRun(
                new WRunProperties(new Bold(), new Color { Val = borderColor }),
                new WText($"{icon} {label}") { Space = SpaceProcessingModeValues.Preserve }
            )
        );
        cell.Append(titlePara);

        // Content paragraphs (AlertBlock already strips the [!TYPE] marker)
        foreach (var subBlock in alert)
        {
            if (subBlock is MParagraph p)
            {
                var para = new Paragraph();
                if (p.Inline != null)
                {
                    RenderInlines(p.Inline, para);
                }
                cell.Append(para);
            }
            else if (subBlock is QuoteBlock nestedQuote)
            {
                RenderQuote(nestedQuote, cell, 0);
            }
            else
            {
                RenderBlock(subBlock, cell);
            }
        }

        row.Append(cell);
        table.Append(row);
        parent.Append(table);
        parent.Append(new Paragraph()); // spacing after
    }

    private static void RenderTable(MTable table, OpenXmlCompositeElement parent)
    {
        var wordTable = new WTable();

        // Table properties with borders
        var tblProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" }
            ),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        wordTable.Append(tblProps);

        bool isHeader = true;
        foreach (var row in table)
        {
            if (row is MTableRow tableRow)
            {
                var wordRow = new WTableRow();

                foreach (var cell in tableRow)
                {
                    if (cell is MTableCell tableCell)
                    {
                        var wordCell = new WTableCell();
                        var para = new Paragraph();

                        foreach (var subBlock in tableCell)
                        {
                            if (subBlock is MParagraph p && p.Inline != null)
                            {
                                if (isHeader)
                                {
                                    // Wrap all inlines in bold for header
                                    foreach (var inline in p.Inline)
                                    {
                                        RenderInline(inline, para, forceBold: true);
                                    }
                                }
                                else
                                {
                                    RenderInlines(p.Inline, para);
                                }
                            }
                        }

                        wordCell.Append(para);
                        wordRow.Append(wordCell);
                    }
                }

                wordTable.Append(wordRow);
                isHeader = false;
            }
        }

        parent.Append(wordTable);
        // Add spacing after table
        parent.Append(new Paragraph());
    }

    private static void RenderInlines(ContainerInline container, Paragraph para,
        bool bold = false, bool italic = false)
    {
        foreach (var inline in container)
        {
            RenderInline(inline, para, bold, italic);
        }
    }

    private static void RenderInline(Inline inline, Paragraph para,
        bool bold = false, bool italic = false, bool forceBold = false)
    {
        bool isBold = bold || forceBold;

        switch (inline)
        {
            case LiteralInline literal:
            {
                var runProps = new WRunProperties();
                if (isBold) runProps.Append(new Bold());
                if (italic) runProps.Append(new Italic());
                var run = new WRun(runProps, new WText(literal.Content.ToString())
                    { Space = SpaceProcessingModeValues.Preserve });
                para.Append(run);
                break;
            }

            case EmphasisInline emphasis:
            {
                bool emBold = emphasis.DelimiterCount == 2;
                bool emItalic = emphasis.DelimiterCount == 1;
                foreach (var child in emphasis)
                {
                    RenderInline(child, para,
                        bold: isBold || emBold,
                        italic: italic || emItalic);
                }
                break;
            }

            case CodeInline code:
            {
                var run = new WRun(
                    new WRunProperties(
                        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                        new FontSize { Val = "20" },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Color = "auto",
                            Fill = "F0F0F0"
                        }
                    ),
                    new WText(code.Content) { Space = SpaceProcessingModeValues.Preserve }
                );
                para.Append(run);
                break;
            }

            case MathInline math:
            {
                try
                {
                    var oMath = LatexMathConverter.ToInlineMath(math.Content.ToString());
                    para.Append(oMath);
                }
                catch
                {
                    // Fallback: render math as plain text if OMML conversion fails
                    var run = new WRun(
                        new WRunProperties(
                            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                            new Italic()
                        ),
                        new WText($"${math.Content}$") { Space = SpaceProcessingModeValues.Preserve }
                    );
                    para.Append(run);
                }
                break;
            }

            case CitationInline citation:
            {
                foreach (var cite in citation.Citations)
                {
                    // CITATION field code
                    para.Append(new WRun(new FieldChar { FieldCharType = FieldCharValues.Begin }));
                    para.Append(new WRun(
                        new FieldCode($" CITATION {cite.Key} \\l 1033 ")
                            { Space = SpaceProcessingModeValues.Preserve }));
                    para.Append(new WRun(new FieldChar { FieldCharType = FieldCharValues.Separate }));
                    para.Append(new WRun(
                        new WRunProperties(),
                        new WText($"({cite.Key})")
                            { Space = SpaceProcessingModeValues.Preserve }));
                    para.Append(new WRun(new FieldChar { FieldCharType = FieldCharValues.End }));
                }
                break;
            }

            case LinkInline link:
            {
                if (link.IsImage)
                {
                    var run = new WRun(
                        new WRunProperties(new Italic()),
                        new WText($"[Image: {link.Url}]") { Space = SpaceProcessingModeValues.Preserve }
                    );
                    para.Append(run);
                }
                else
                {
                    foreach (var child in link)
                    {
                        RenderInline(child, para, isBold, italic);
                    }
                }
                break;
            }

            case LineBreakInline:
            {
                para.Append(new WRun(new WBreak()));
                break;
            }

            case ContainerInline containerInline:
            {
                foreach (var child in containerInline)
                {
                    RenderInline(child, para, isBold, italic);
                }
                break;
            }
        }
    }
}
