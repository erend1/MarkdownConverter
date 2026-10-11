using System.Globalization;
using MarkdownConverter.Infrastructure.Converters;

namespace MarkdownConverter.Infrastructure.Tests;

/// <summary>
/// Table layout of the shared LaTeX renderer: the emitted column specification must keep
/// every table inside the line, size columns from their contents, and let long tokens wrap.
/// </summary>
public class MarkdownLatexRendererTableTests
{
    private const string IssueExample =
        "| ID | Description | Result |\n" +
        "|---:|-------------|-------:|\n" +
        "| 12 | This description contains a lengthy explanation with many words that should wrap inside the available column width rather than expanding the entire table past the page margin. | 42 |\n" +
        "| 13 | https://example.org/very/long/unbreakable/path/with/identifiers/and/detailed/names | 7 |\n";

    // Line widths a table can meet: the page, a block quote, a nested list item.
    public static TheoryData<double> LineWidthsPt => new() { 452.9679, 428.97, 401.5 };

    public static TheoryData<string> Fixtures => new(LatexTableFixtures.Names);

    // --- Width budget ---

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    public void Table_ColumnWidths_AreSharesOfTheLineLeftAfterPaddingAndRules(int columnCount)
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(ShortTable(columnCount)));

        Assert.Equal(columnCount, table.Columns.Count);
        Assert.Equal(columnCount - 1, table.RuleCount);
        Assert.Null(table.CellPadding);
        Assert.All(table.Columns, column =>
        {
            Assert.Equal(2 * columnCount, column.PaddingCount);
            Assert.Equal(columnCount - 1, column.BudgetRuleCount);
        });
        Assert.InRange(table.Shares.Sum(), 9990, 9999);
    }

    [Fact]
    public void Table_DoesNotEmitRoundedTextwidthFractions()
    {
        // Six columns used to be declared as 6 x 0.17\textwidth = 1.02\textwidth before padding.
        var result = MarkdownLatexRenderer.Render(ShortTable(6));

        Assert.DoesNotContain(@"\textwidth}", TableSpecification(result));
    }

    [Theory]
    [MemberData(nameof(LineWidthsPt))]
    public void Table_TotalWidth_NeverExceedsTheLine(double lineWidthPt)
    {
        long lineWidth = (long)Math.Round(lineWidthPt * LatexTableSource.PointSp);

        for (int columnCount = 1; columnCount <= 60; columnCount++)
        {
            foreach (var markdown in new[] { ShortTable(columnCount), AsymmetricTable(columnCount) })
            {
                var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(markdown));

                Assert.True(
                    table.TotalWidthSp(lineWidth) <= lineWidth,
                    $"{columnCount} columns are {table.TotalWidthSp(lineWidth) - lineWidth}sp wider than a {lineWidthPt}pt line");
            }
        }
    }

    [Fact]
    public void Table_ColumnWidths_StayPositiveForAnyColumnCount()
    {
        for (int columnCount = 1; columnCount <= 120; columnCount++)
        {
            var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(AsymmetricTable(columnCount)));

            Assert.All(
                table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp),
                width => Assert.True(width > 0, $"{columnCount} columns produced a column of {width}sp"));
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Table_Fixture_FitsTheLine(string fixture)
    {
        var tables = LatexTableSource.ParseAll(MarkdownLatexRenderer.Render(LatexTableFixtures.Read(fixture)));

        Assert.NotEmpty(tables);
        Assert.All(tables, table =>
        {
            Assert.True(table.TotalWidthSp(LatexTableSource.DocumentLineWidthSp) <= LatexTableSource.DocumentLineWidthSp);
            Assert.All(table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp), width => Assert.True(width > 0));
        });
    }

    // --- Content-aware widths ---

    [Fact]
    public void Table_ShortCells_ShareTheWidthEqually()
    {
        var md = "| Col1 | Col2 | Col3 |\n|------|------|------|\n| A | B | C |";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.Equal([3333, 3333, 3333], table.Shares);
    }

    [Fact]
    public void Table_LongDescriptionBetweenShortColumns_GetsMostOfTheWidth()
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(IssueExample));

        // ID keeps the 2em minimum, Result the width of its bold header, Description the rest.
        // The values are pinned on purpose: widths must not drift between runs or machines.
        Assert.Equal([526, 8519, 953], table.Shares);
    }

    [Fact]
    public void Table_ShortColumns_KeepTheirTextOnOneLine()
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(LatexTableFixtures.Read("wide-6col")));
        var widths = table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp);

        // ID, Component, Owner, Status, Risk: wide enough for the longest cell, set in 10.95pt type.
        double em = 10.95 * LatexTableSource.PointSp;
        Assert.True(widths[1] > 5.4 * em, "Component must hold 'Desktop shell'");
        Assert.True(widths[2] > 6.2 * em, "Owner must hold 'Infrastructure'");
        Assert.True(widths[4] > 2.3 * em, "Risk must hold the bold header");
        // The descriptive column takes what is left and is the widest by far.
        Assert.True(widths[5] > 1.9 * widths[2]);
        Assert.True(widths[5] > 6 * widths[0]);
    }

    [Fact]
    public void Table_ProseColumns_ShareTheRemainingWidthEqually()
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(LatexTableFixtures.Read("prose-4col")));

        Assert.True(table.Shares[0] < table.Shares[1]);
        Assert.Equal(table.Shares[1], table.Shares[2]);
        Assert.Equal(table.Shares[2], table.Shares[3]);
    }

    [Fact]
    public void Table_EmptyColumn_StillGetsAReadableMinimumWidth()
    {
        var prose = string.Join(" ", Enumerable.Repeat("word", 80));
        var md = $"| A |   | Notes |\n|---|---|-------|\n| 1 |   | {prose} |";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));
        var widths = table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp);

        // Two em of 10.95pt type, less the rounding of the share.
        Assert.True(widths[1] >= 2 * 10.95 * LatexTableSource.PointSp * 0.99);
    }

    [Fact]
    public void Table_WordsThatFit_AreNeverGivenBreakOpportunities()
    {
        foreach (var fixture in new[] { "short-cells-3col", "prose-4col", "wide-6col", "multipage-4col" })
        {
            var result = MarkdownLatexRenderer.Render(LatexTableFixtures.Read(fixture));

            Assert.DoesNotContain(@"\allowbreak", result);
            Assert.DoesNotContain(@"\-", result);
        }
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void Table_Output_DoesNotDependOnTheCurrentCulture(string cultureName)
    {
        var expected = RenderWithCulture(CultureInfo.InvariantCulture, IssueExample);

        var actual = RenderWithCulture(CultureInfo.GetCultureInfo(cultureName), IssueExample);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain("0,", TableSpecification(actual));
    }

    [Fact]
    public void Table_Output_IsStableAcrossRenders()
    {
        var md = LatexTableFixtures.Read("unbreakable-content");

        Assert.Equal(MarkdownLatexRenderer.Render(md), MarkdownLatexRenderer.Render(md));
    }

    // --- Alignment and cell content ---

    [Fact]
    public void Table_Alignment_FollowsTheMarkdownColumnAlignment()
    {
        var md = "| L | C | R | D |\n|:--|:-:|--:|---|\n| a | b | c | d |";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.Equal(
            ["raggedright", "centering", "raggedleft", "raggedright"],
            table.Columns.Select(c => c.Alignment));
    }

    [Fact]
    public void Table_KeepsHeaderRowOrderAndCellText()
    {
        var md = "| Name | Value |\n|------|------:|\n| first | 1 |\n| second | 2 |\n| third | 3 |";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.Equal(
            [
                [@"\textbf{Name}", @"\textbf{Value}"],
                ["first", "1"],
                ["second", "2"],
                ["third", "3"]
            ],
            table.Rows);
    }

    [Fact]
    public void Table_InlineMarkupAndEscapes_ArePreserved()
    {
        var result = MarkdownLatexRenderer.Render(LatexTableFixtures.Read("alignment-and-markup"));
        var table = LatexTableSource.Single(result);

        Assert.Equal(["raggedright", "centering", "raggedleft", "raggedright"], table.Columns.Select(c => c.Alignment));
        Assert.Equal(
            [@"\textbf{Left}", @"\textbf{Centre}", @"\textbf{Right}", @"\textbf{Default}"],
            table.Rows[0]);
        Assert.Equal(
            [
                @"\textbf{Bold} and \textit{italic} text",
                @"\texttt{inline code}",
                "1,234.50",
                @"A \href{https://example.org/docs}{named link} in a sentence."
            ],
            table.Rows[1]);
        Assert.Equal(
            [
                @"Price is 10\% \& tax \#1",
                "$x^2 + y^2 = z^2$",
                "-42",
                @"Escapes: under\_score, \{braces\}, tilde\textasciitilde{} and caret\textasciicircum{}."
            ],
            table.Rows[2]);
        Assert.Equal(
            [
                "Çalışma günü özeti",
                @"± 0.5 {\textdegree}C",
                @"3 {$\times$} 4",
                @"Naïve café -- ``quoted'' --- done{\ldots}"
            ],
            table.Rows[3]);
        Assert.Equal(
            [
                @"See \cite{smith2023}",
                @"\textit{nested \textbf{strong} text}",
                @"$\alpha$",
                @"Costs \$5 today, path\textbackslash{}to\textbackslash{}file."
            ],
            table.Rows[4]);
    }

    [Fact]
    public void Table_RowsShorterThanTheHeader_KeepTheirCellsInPlace()
    {
        var md = "| A | B | C |\n|---|---|---|\n| 1 | 2 |\n| 3 | 4 | 5 |";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(["1", "2", ""], table.Rows[1]);
        Assert.Equal(["3", "4", "5"], table.Rows[2]);
    }

    [Fact]
    public void Render_TextOutsideTables_NeverGetsBreakOpportunities()
    {
        var md =
            "A paragraph with https://example.org/very/long/unbreakable/path/with/identifiers/and/detailed/names " +
            "and `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` and " +
            "MarkdownConverter.Infrastructure.Converters.MarkdownLatexRenderer.RenderTable.\n\n" +
            "- A list item with very_long_configuration_option_name_with_many_separate_segments_and_no_spaces\n\n" +
            "> A quote with Donaudampfschifffahrtsgesellschaftskapitänsmützenabzeichenhersteller\n";

        var result = MarkdownLatexRenderer.Render(md);

        Assert.DoesNotContain(@"\allowbreak", result);
        Assert.DoesNotContain(@"\-", result);
        Assert.Contains(
            @"\texttt{e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855}", result);
    }

    [Fact]
    public void Table_FormulaBesideACitation_GetsTheRoomItCannotDoWithout()
    {
        // Sixty letters without an operator are about 350pt wide and cannot break.
        const string formula = "$aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaA$";
        static LatexTableSource Table(string source) => LatexTableSource.Single(
            MarkdownLatexRenderer.Render($"| Formula | Source |\n|---------|--------|\n| {formula} | {source} |\n"));

        var withCitation = Table("[@smith2023]");

        // A citation is a word like any other, so it gives way as "Smith 2023" does.
        Assert.Equal(Table("Smith 2023").Shares, withCitation.Shares);
        Assert.True(withCitation.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp)[0] > 350 * LatexTableSource.PointSp);
        Assert.Equal(@"\cite{smith2023}", withCitation.Rows[1][1]);
    }

    [Fact]
    public void Table_CitationColumnsBesideAFormula_DoNotStarveTheTextColumn()
    {
        const string md =
            "| Method | Estimator | A | B | C | D | E |\n" +
            "|---|---|---|---|---|---|---|\n" +
            @"| Ordinary least squares | $\hat{\beta}=\frac{\sum_{i}(x_i-\bar{x})(y_i-\bar{y})}{\sum_{i}(x_i-\bar{x})^2}$ | [@a2001] | [@b2002] | [@c2003] | [@d2004] | [@e2005] |" + "\n";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));
        var widths = table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp);

        // The formula and the five citations used to leave the first column 0.7em.
        Assert.All(widths, width => Assert.True(width > 2.9 * 10.95 * LatexTableSource.PointSp));
        Assert.Equal(widths[2], widths[0]);
        Assert.Equal("Ordinary least squares", LatexTableSource.WithoutBreaks(table.Rows[1][0]));
        Assert.Equal(@"\cite{a2001}", table.Rows[1][2]);
    }

    // --- Content that cannot wrap by itself ---

    [Fact]
    public void Table_LongUrl_MayBreakAfterSeparators_AndKeepsItsTarget()
    {
        const string url = "https://example.org/very/long/unbreakable/path/with/identifiers/and/detailed/names";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(url)));
        var cell = table.Rows[1][1];

        Assert.StartsWith($@"\href{{{url}}}{{https://\allowbreak{{}}example.\allowbreak{{}}org/\allowbreak{{}}very/", cell);
        Assert.Equal($@"\href{{{url}}}{{{url}}}", LatexTableSource.WithoutBreaks(cell));
    }

    [Fact]
    public void Table_LongInlineCode_MayBreakAfterSeparatorsAndAtCamelCase()
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(
            NarrowColumnTable("`src/MarkdownConverter.Infrastructure/Converters/MarkdownLatexRenderer.cs`")));

        Assert.Equal(
            @"\texttt{src/\allowbreak{}Markdown\allowbreak{}Converter.\allowbreak{}Infrastructure/\allowbreak{}Converters/\allowbreak{}Markdown\allowbreak{}Latex\allowbreak{}Renderer.\allowbreak{}cs}",
            table.Rows[1][1]);
    }

    [Fact]
    public void Table_EscapedCharacters_AreNotSplitByBreakOpportunities()
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(
            NarrowColumnTable("very_long_configuration_option_name_with_many_separate_segments&more%stuff#here")));
        var cell = table.Rows[1][1];

        Assert.Contains(@"very\_\allowbreak{}long\_\allowbreak{}configuration\_", cell);
        Assert.Equal(
            @"very\_long\_configuration\_option\_name\_with\_many\_separate\_segments\&more\%stuff\#here",
            LatexTableSource.WithoutBreaks(cell));
    }

    [Theory]
    [InlineData(@"very\_long\_configuration\_option\_name\_with\_many\_separate\_segments")]
    [InlineData("response.data.items[0].attributes.relationships[12].links.related.href")]
    [InlineData(@"C:\\Users\\name\\AppData\\Local\\Temp\\mdconv\\output\\report.tex")]
    [InlineData("`--configuration`=`Release`;`--verbosity`=`minimal`;`--no-restore`;`--nologo`")]
    [InlineData(@"first\|second\|third\|fourth\|fifth\|sixth\|seventh\|eighth\|ninth")]
    [InlineData(@"SELECT\*FROM\*table\*WHERE\*identifier\*IS\*NOT\*NULL")]
    [InlineData("**Markdown**Converter.*Infrastructure*.`LatexRenderer`.RenderTable")]
    public void Table_TokenMadeOfSeveralInlines_BreaksLikeOneToken(string token)
    {
        // Markdown escapes, brackets, emphasis and code spans split a token into several
        // inline nodes; it is still one unbreakable run for TeX.
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(token))).Rows[1][1];

        Assert.Contains(@"\allowbreak{}", cell);
        Assert.Equal(PlainCell(token), LatexTableSource.WithoutBreaks(cell));
        Assert.All(
            LatexTableSource.WithoutBreaks(cell.Replace(@"\allowbreak{}", "\n")).Split('\n'),
            piece => Assert.True(piece.Length <= 40, $"No break opportunity inside '{piece}'"));
    }

    [Fact]
    public void Table_BreakBeforeACharacterTheRendererDrops_KeepsTheFollowingSpace()
    {
        // The Greek letter has no glyph in the document fonts and is dropped. A mark in its
        // place would be followed by the space, and TeX swallows a space after a command.
        const string text = "temperature_coefficient_of_resistance_\u03b1 measured at room temperature";
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render(
            $"| Quantity | Notes | More |\n|---|---|---|\n| {text} | {Prose} | {Prose} |\n")).Rows[1][0];

        Assert.Contains(@"resistance\_ measured at room temperature", cell);
        Assert.Equal(
            @"temperature\_coefficient\_of\_resistance\_ measured at room temperature",
            LatexTableSource.WithoutBreaks(cell));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Table_BreakMarks_AlwaysStandBeforeACharacter(string fixture)
    {
        var tables = LatexTableSource.ParseAll(MarkdownLatexRenderer.Render(LatexTableFixtures.Read(fixture)));

        Assert.All(tables.SelectMany(table => table.Rows).SelectMany(row => row), cell =>
        {
            Assert.DoesNotMatch(@"(\\allowbreak\{\}|\\-)(\s|\}*$)", cell);
            Assert.DoesNotContain(@"\allowbreak{}\allowbreak{}", cell);
        });
    }

    [Fact]
    public void Table_TextNextToAFormula_MayBreakBesideItButNotInsideIt()
    {
        const string math = @"$\frac{\partial^2 u}{\partial x^2}\frac{\partial^2 v}{\partial y^2}\frac{\partial^2 w}{\partial z^2}$";
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render(
            NarrowColumnTable($"coefficient={math};term"))).Rows[1][1];

        Assert.Equal($@"coefficient=\allowbreak{{}}{math};\allowbreak{{}}term", cell);
    }

    [Theory]
    [InlineData(
        "(-0.12,-0.34,-0.56,-0.78,-0.91,-1.23,-1.45,-1.67,-1.89,-2.01)",
        @"(-0.12,\allowbreak{}-0.34,\allowbreak{}-0.56,\allowbreak{}-0.78,\allowbreak{}-0.91,\allowbreak{}-1.23,\allowbreak{}-1.45,\allowbreak{}-1.67,\allowbreak{}-1.89,\allowbreak{}-2.01)")]
    [InlineData(
        "`--configuration=Release;--verbosity=minimal;--no-restore;--nologo`",
        @"\texttt{--configuration=\allowbreak{}Release;\allowbreak{}--verbosity=\allowbreak{}minimal;\allowbreak{}--no-\allowbreak{}restore;\allowbreak{}--nologo}")]
    [InlineData(
        "offset=-0.000000001;scale=+1.000000001;limit=(-1.5,+1.5)",
        @"offset=\allowbreak{}-0.000000001;\allowbreak{}scale=\allowbreak{}+1.000000001;\allowbreak{}limit=\allowbreak{}(-1.5,\allowbreak{}+1.5)")]
    [InlineData(
        "(.125,.250,.375,.500,.625,.750,.875,.999,.111,.222,.333,.444)",
        @"(.125,\allowbreak{}.250,\allowbreak{}.375,\allowbreak{}.500,\allowbreak{}.625,\allowbreak{}.750,\allowbreak{}.875,\allowbreak{}.999,\allowbreak{}.111,\allowbreak{}.222,\allowbreak{}.333,\allowbreak{}.444)")]
    [InlineData(
        "name=.gitignore;path=.github/workflows;framework=.NET;ratio=.75",
        @"name=\allowbreak{}.gitignore;\allowbreak{}path=\allowbreak{}.github/\allowbreak{}workflows;\allowbreak{}framework=\allowbreak{}.NET;\allowbreak{}ratio=\allowbreak{}.75")]
    [InlineData(
        "alpha=>beta;gamma=>delta;epsilon=>zeta;eta=>theta;iota=>kappa;lambda=>mu",
        @"alpha=>\allowbreak{}beta;\allowbreak{}gamma=>\allowbreak{}delta;\allowbreak{}epsilon=>\allowbreak{}zeta;\allowbreak{}eta=>\allowbreak{}theta;\allowbreak{}iota=>\allowbreak{}kappa;\allowbreak{}lambda=>\allowbreak{}mu")]
    public void Table_BreakAfterASeparator_KeepsWhatFollowsWhole(string token, string expected)
    {
        // A break after the comma, semicolon or equals sign: not between a sign or a leading
        // point and what it belongs to, and not inside "=>".
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(token))).Rows[1][1];

        Assert.Equal(expected, cell);
    }

    [Theory]
    [InlineData("$n$......................................................................42")]
    [InlineData("......................................................................$n$")]
    [InlineData("(((((((((((((((((((((((((((((($x$))))))))))))))))))))))))))))))")]
    public void Table_PunctuationGluedToAFormula_MayBreakBetweenItsCharacters(string token)
    {
        // The formula keeps the characters on either side of it; the rest of the run may break.
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(token))).Rows[1][1];

        Assert.Equal(token, LatexTableSource.WithoutBreaks(cell));
        Assert.DoesNotContain(@"$\allowbreak{}", cell);
        Assert.DoesNotContain(@"\allowbreak{}$", cell);
        Assert.All(cell.Split(@"\allowbreak{}"), piece => Assert.InRange(piece.Length, 2, 14));
    }

    [Fact]
    public void Table_BreakMarks_DoNotSplitADashLigature()
    {
        // Forty columns leave room for two characters a line, so every position is a
        // candidate. "--" is drawn as one en dash and has to stay together.
        var header = "|" + string.Concat(Enumerable.Repeat(" H |", 40));
        var divider = "|" + string.Concat(Enumerable.Repeat("---|", 40));
        var row = "| 1990--1995 |" + string.Concat(Enumerable.Repeat(" 1 |", 39));
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render($"{header}\n{divider}\n{row}\n")).Rows[1][0];

        Assert.Contains(@"\allowbreak{}", cell);
        Assert.Contains("--", cell);
        Assert.Equal("1990--1995", LatexTableSource.WithoutBreaks(cell));
    }

    [Theory]
    [InlineData("1990--1995", @"1\allowbreak{}9\allowbreak{}9\allowbreak{}0\allowbreak{}--\allowbreak{}1\allowbreak{}9\allowbreak{}9\allowbreak{}5")]
    [InlineData("a---b", @"a\allowbreak{}---\allowbreak{}b")]                                                           // an em dash
    [InlineData("x>>y", @"x\allowbreak{}>>\allowbreak{}y")]                                                             // a guillemet
    [InlineData(@"1990-\-1995", @"1\allowbreak{}9\allowbreak{}9\allowbreak{}0\allowbreak{}--\allowbreak{}1\allowbreak{}9\allowbreak{}9\allowbreak{}5")]
    [InlineData("a----b", @"a\allowbreak{}-\allowbreak{}-\allowbreak{}-\allowbreak{}-\allowbreak{}b")]                  // a rule
    [InlineData("a++==b", @"a\allowbreak{}+\allowbreak{}+\allowbreak{}=\allowbreak{}=\allowbreak{}b")]
    [InlineData("a//b..c", @"a\allowbreak{}/\allowbreak{}/\allowbreak{}b\allowbreak{}.\allowbreak{}.\allowbreak{}c")]
    [InlineData("a-`-b`", @"a\allowbreak{}-\texttt{\allowbreak{}-\allowbreak{}b}")]                                     // two fonts
    public void Table_BreakMarks_SplitOnlyWhatIsNotALigature(string text, string expected)
    {
        // Sixty columns leave less than a character a line, so a mark goes wherever it may.
        // Two or three dashes, quotes, commas or angle brackets in a row are one glyph;
        // a longer run, other doubled characters and characters in two fonts are not.
        var header = "|" + string.Concat(Enumerable.Repeat(" H |", 60));
        var divider = "|" + string.Concat(Enumerable.Repeat("---|", 60));
        var row = $"| {text} |" + string.Concat(Enumerable.Repeat(" 1 |", 59));
        var cell = LatexTableSource.Single(MarkdownLatexRenderer.Render($"{header}\n{divider}\n{row}\n")).Rows[1][0];

        Assert.Equal(expected, cell);
    }

    [Theory]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`")]
    [InlineData("00000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void Table_TokenWithoutSeparators_MayBreakBetweenCharacters(string token)
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(token)));
        var cell = table.Rows[1][1];
        var text = token.Trim('`');

        Assert.Contains(@"\allowbreak{}", cell);
        Assert.DoesNotContain(@"\-", cell);
        Assert.Contains(text, LatexTableSource.WithoutBreaks(cell));

        // No run between two break opportunities is longer than a quarter of the token,
        // and none strands fewer than two characters at the start or three at the end.
        var chunks = cell.Replace(@"\texttt{", "").TrimEnd('}').Split(@"\allowbreak{}");
        Assert.True(chunks.Length >= 4);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 2, text.Length / 4));
        Assert.True(chunks[^1].Length >= 3);
    }

    [Theory]
    [InlineData("Donaudampfschifffahrtsgesellschaftskapitänsmützenabzeichenhersteller")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW")]
    public void Table_WordWiderThanItsColumn_MayBreakWithAHyphen(string word)
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(word)));
        var cell = table.Rows[1][1];

        Assert.Contains(@"\-", cell);
        Assert.DoesNotContain(@"\allowbreak", cell);
        Assert.Equal(word, LatexTableSource.WithoutBreaks(cell));
    }

    [Fact]
    public void Table_Math_IsEmittedUnchanged()
    {
        const string math = @"$\hat{\beta} = (X^{\top}X)^{-1}X^{\top}y + \lambda \sum_{i=1}^{n} \lvert w_i \rvert + \frac{1}{2}\lVert w \rVert_2^2 + \gamma\,\mathrm{tr}(\Sigma^{-1}S)$";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable(math)));

        Assert.Equal(math, table.Rows[1][1]);
    }

    [Fact]
    public void Table_CitationAndLinkTarget_AreEmittedUnchanged()
    {
        const string url = "https://example.org/a/very/long/link/target/that/is/not/visible/in/the/document/body.html";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(
            NarrowColumnTable($"[docs]({url}) [@smith2023; @jones2024]")));

        Assert.Equal($@"\href{{{url}}}{{docs}} \cite{{smith2023,jones2024}}", table.Rows[1][1]);
    }

    [Fact]
    public void Table_ShortTokensWithSeparators_AreLeftUntouched()
    {
        var md = "| Key | Value |\n|-----|-------|\n| I/O | 1,234.50 |\n| e.g. | file.txt |\n| v1.2.3 | `--no-restore` |\n| 2026-10-11 | camelCase |";
        var result = MarkdownLatexRenderer.Render(md);

        Assert.DoesNotContain(@"\allowbreak", result);
        Assert.DoesNotContain(@"\-", result);
        Assert.Contains(@"I/O & 1,234.50 \\", result);
        Assert.Contains(@"v1.2.3 & \texttt{--no-restore} \\", result);
    }

    [Fact]
    public void Table_CharactersTheRendererDrops_LeaveNoBreakOpportunitiesBehind()
    {
        // CJK text has no glyphs in the document fonts and is dropped by the escaper.
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(
            NarrowColumnTable(new string('漢', 120))));

        Assert.Equal("", table.Rows[1][1]);
    }

    [Fact]
    public void Table_ImagePlaceholder_MayBreakInsideALongAddress()
    {
        const string url = "https://example.org/assets/images/diagrams/architecture/overview-of-the-whole-system.png";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(NarrowColumnTable($"![diagram]({url})")));
        var cell = table.Rows[1][1];

        Assert.Contains(@"\allowbreak{}", cell);
        Assert.Equal($@"\textit{{[Image: {url}]}}", LatexTableSource.WithoutBreaks(cell));
    }

    // --- Tables with many columns ---

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void Table_UpToTenColumns_KeepsTheDefaultCellPadding(int columnCount)
    {
        var result = MarkdownLatexRenderer.Render(ShortTable(columnCount));
        var table = LatexTableSource.Single(result);

        Assert.Null(table.CellPadding);
        Assert.False(table.IsGrouped);
        Assert.DoesNotContain(@"\tabcolsep}{", result);
        Assert.DoesNotContain(@"\begingroup", result);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(26)]
    public void Table_WithManyColumns_TightensThePaddingForThatTableOnly(int columnCount)
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(ShortTable(columnCount)));

        Assert.Equal("3pt", table.CellPadding);
        Assert.True(table.IsGrouped);
        Assert.Equal(columnCount - 1, table.RuleCount);
        Assert.True(table.TotalWidthSp(LatexTableSource.DocumentLineWidthSp) <= LatexTableSource.DocumentLineWidthSp);
    }

    [Fact]
    public void Table_DenseFixture_KeepsLabelsAndNumbersWhole()
    {
        var result = MarkdownLatexRenderer.Render(LatexTableFixtures.Read("dense-13col"));
        var table = LatexTableSource.Single(result);
        var widths = table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp);

        Assert.Equal(13, table.Columns.Count);
        Assert.Equal("3pt", table.CellPadding);
        Assert.Equal(
            ["Conversions", "1204", "1318", "1456", "1502", "1611", "1720", "1698", "1755", "1830", "1902", "2011", "2140"],
            table.Rows[1]);
        // The label column is wide enough for "Documentation"; a month column for four digits.
        Assert.True(widths[0] > 6.0 * 10.95 * LatexTableSource.PointSp);
        Assert.All(widths.Skip(1), width => Assert.True(width > 2.0 * 10.95 * LatexTableSource.PointSp));
    }

    [Theory]
    [InlineData(27)]
    [InlineData(40)]
    [InlineData(120)]
    public void Table_BeyondWhatThePageCanShow_DropsThePaddingButStaysInsideTheLine(int columnCount)
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(ShortTable(columnCount)));

        Assert.Equal("0pt", table.CellPadding);
        Assert.True(table.IsGrouped);
        Assert.Equal(columnCount - 1, table.RuleCount);
        Assert.True(table.TotalWidthSp(LatexTableSource.DocumentLineWidthSp) <= LatexTableSource.DocumentLineWidthSp);
        Assert.Equal(columnCount, table.Rows[1].Count);
    }

    [Fact]
    public void Table_WhoseRulesAloneWouldFillTheLine_DropsThemToo()
    {
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(ShortTable(600)));

        Assert.Equal("0pt", table.CellPadding);
        Assert.Equal(0, table.RuleCount);
        Assert.All(table.Columns, column => Assert.Equal(0, column.BudgetRuleCount));
        Assert.True(table.TotalWidthSp(LatexTableSource.DocumentLineWidthSp) <= LatexTableSource.DocumentLineWidthSp);
        Assert.All(table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp), width => Assert.True(width >= 0));
    }

    [Fact]
    public void Table_DenseWithOneLabelColumn_KeepsTheLabelsWhole()
    {
        // Twenty columns leave about 1.5em each; the numbers need less, the labels more.
        var header = "| Metric | " + string.Join(" | ", Enumerable.Range(1, 19).Select(i => $"{i}")) + " |";
        var divider = "|" + string.Concat(Enumerable.Repeat("---|", 20));
        var row = "| Conversions | " + string.Join(" | ", Enumerable.Range(1, 19).Select(i => $"{10 + i}")) + " |";
        var result = MarkdownLatexRenderer.Render($"{header}\n{divider}\n{row}\n");
        var table = LatexTableSource.Single(result);
        var widths = table.ColumnWidthsSp(LatexTableSource.DocumentLineWidthSp);

        Assert.DoesNotContain(@"\-", result);
        Assert.DoesNotContain(@"\allowbreak", result);
        Assert.True(widths[0] > 5.2 * 10.95 * LatexTableSource.PointSp, "Metric must hold 'Conversions'");
        Assert.All(widths.Skip(1), width => Assert.InRange(width, 1.0 * 10.95 * LatexTableSource.PointSp, 2.0 * 10.95 * LatexTableSource.PointSp));
    }

    // --- Large tables ---

    [Fact]
    public void Table_OfOrdinarySize_KeepsThePackageChunkSize()
    {
        var result = MarkdownLatexRenderer.Render(LatexTableFixtures.Read("multipage-4col"));
        var table = LatexTableSource.Single(result);

        Assert.Equal(73, table.Rows.Count);
        Assert.Null(table.ChunkRows);
        Assert.False(table.IsGrouped);
        Assert.DoesNotContain("LTchunksize", result);
    }

    [Theory]
    [InlineData(4, 150, 20)]
    [InlineData(40, 20, 10)]
    [InlineData(100, 10, 4)]
    public void Table_WithManyCells_IsTypesetAFewRowsAtATime(int columnCount, int rowCount, int expectedChunk)
    {
        // longtable holds a chunk of rows in memory twice; 200 rows of a wide table exhaust TeX.
        var divider = "|" + string.Concat(Enumerable.Repeat("---|", columnCount));
        var row = "|" + string.Concat(Enumerable.Repeat(" 1234 |", columnCount));
        var md = string.Join("\n", Enumerable.Repeat(row, rowCount).Prepend(divider).Prepend(row)) + "\n";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.Equal(expectedChunk, table.ChunkRows);
        Assert.True(table.IsGrouped);
        Assert.Equal(rowCount + 1, table.Rows.Count);
    }

    [Theory]
    [InlineData(4, 20, 10, null)]       // 4,116 characters
    [InlineData(5, 30, 100, 8)]         // 2,495 characters a row
    [InlineData(20, 19, 80, 2)]         // 400 cells, 7,980 characters a row
    [InlineData(2, 3, 3000, 1)]         // 29,998 characters a row
    public void Table_WithMuchText_IsTypesetAboutTwentyThousandCharactersAtATime(
        int columnCount, int rowCount, int wordsPerCell, int? expectedChunk)
    {
        // Four hundred cells are few, but not when each of them holds a paragraph.
        var text = string.Join(" ", Enumerable.Repeat("word", wordsPerCell));
        var divider = "|" + string.Concat(Enumerable.Repeat("---|", columnCount));
        var row = "|" + string.Concat(Enumerable.Repeat($" {text} |", columnCount));
        var md = string.Join("\n", Enumerable.Repeat(row, rowCount).Prepend(divider).Prepend(row)) + "\n";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.True(columnCount * (rowCount + 1) <= 400);
        Assert.Equal(expectedChunk, table.ChunkRows);
        Assert.Equal(rowCount + 1, table.Rows.Count);
    }

    [Fact]
    public void Table_WithAFewLongRows_IsChunkedForThem()
    {
        // The average row is short; the three long ones in a row decide.
        var longText = string.Join(" ", Enumerable.Repeat("word", 400));
        string Row(string text) => $"| {text} | {text} | {text} | {text} |";
        var rows = Enumerable.Repeat(Row("short"), 40)
            .Concat(Enumerable.Repeat(Row(longText), 3))
            .Concat(Enumerable.Repeat(Row("short"), 40));
        var md = string.Join("\n", rows.Prepend("|---|---|---|---|").Prepend(Row("Header"))) + "\n";
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        // 7,996 characters a long row: two of them are a chunk, three are not.
        Assert.Equal(2, table.ChunkRows);
        Assert.Equal(84, table.Rows.Count);
    }

    // --- Tables inside quotes, callouts and lists ---

    [Fact]
    public void Table_InsideANestedList_IsPlannedForTheNarrowerLine()
    {
        const string table =
            "| Key | Value |\n" +
            "|-----|-------|\n" +
            "| type | `ConfigurationOptionsValidatorFactoryProviderBuilderBase` |\n" +
            "| other | short |\n";
        var nested = "- Outer item\n  - Inner item\n\n" + string.Concat(table.Split('\n').Select(line => "      " + line + "\n"));

        var onThePage = LatexTableSource.Single(MarkdownLatexRenderer.Render(table));
        var inTheList = LatexTableSource.Single(MarkdownLatexRenderer.Render(nested));

        // Two list levels leave a line of 401.5pt. Planned for the page, the code column would
        // get four fifths of it and the 55 typewriter characters (316.2pt) would not fit.
        long nestedLine = (long)Math.Round(401.5 * LatexTableSource.PointSp);
        Assert.True(inTheList.Shares[1] > onThePage.Shares[1]);
        Assert.True(inTheList.ColumnWidthsSp(nestedLine)[1] > 316.2 * LatexTableSource.PointSp);
        Assert.True(inTheList.TotalWidthSp(nestedLine) <= nestedLine);
        Assert.Equal(onThePage.Rows[1][1], LatexTableSource.WithoutBreaks(inTheList.Rows[1][1]));
    }

    [Theory]
    [InlineData("quote", "3pt")]        // a block quote takes 24pt of the line
    [InlineData("callout", "3pt")]      // a callout 23.3pt
    [InlineData("list", "3pt")]         // a list level 27.4pt
    [InlineData("page", null)]
    public void Table_InsideAContainer_TightensThePaddingSooner(string container, string? expectedPadding)
    {
        // Ten columns are the most the page holds at the default padding.
        var lines = ShortTable(10).TrimEnd('\n').Split('\n');
        var md = container switch
        {
            "quote" => string.Concat(lines.Select(line => "> " + line + "\n")),
            "callout" => "> [!NOTE]\n> A note.\n>\n" + string.Concat(lines.Select(line => "> " + line + "\n")),
            "list" => "- item\n\n" + string.Concat(lines.Select(line => "  " + line + "\n")),
            _ => string.Concat(lines.Select(line => line + "\n"))
        };

        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));

        Assert.Equal(10, table.Columns.Count);
        Assert.Equal(expectedPadding, table.CellPadding);
    }

    [Fact]
    public void Table_InsideManyNestedQuotes_StillHasAPositiveBudget()
    {
        // Fourteen levels leave a line of 117pt, less than the padding of ten ordinary columns.
        var prefix = string.Concat(Enumerable.Repeat("> ", 14));
        var md = string.Concat(ShortTable(10).TrimEnd('\n').Split('\n').Select(line => prefix + line + "\n"));
        var table = LatexTableSource.Single(MarkdownLatexRenderer.Render(md));
        long lineWidth = (long)Math.Round((452.9679 - 14 * 24) * LatexTableSource.PointSp);

        Assert.Equal("0pt", table.CellPadding);
        Assert.True(table.TotalWidthSp(lineWidth) <= lineWidth);
        Assert.All(table.ColumnWidthsSp(lineWidth), width => Assert.True(width > 0));
    }

    private static string RenderWithCulture(CultureInfo culture, string markdown)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            return MarkdownLatexRenderer.Render(markdown);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static string TableSpecification(string tex) =>
        tex.ReplaceLineEndings("\n").Split('\n').Single(line => line.Contains(@"\begin{longtable}"));

    // A table whose every cell fits on one line.
    private static string ShortTable(int columnCount)
    {
        var header = string.Join(" | ", Enumerable.Range(1, columnCount).Select(i => $"H{i}"));
        var divider = string.Join(" | ", Enumerable.Repeat("---", columnCount));
        var row = string.Join(" | ", Enumerable.Range(1, columnCount).Select(i => $"{i}"));
        return $"| {header} |\n| {divider} |\n| {row} |\n";
    }

    // A table with one wordy column, one long unbreakable token and otherwise short cells.
    private static string AsymmetricTable(int columnCount)
    {
        var header = string.Join(" | ", Enumerable.Range(1, columnCount).Select(i => $"Heading{i}"));
        var divider = string.Join(" | ", Enumerable.Repeat("---", columnCount));
        var row = string.Join(" | ", Enumerable.Range(1, columnCount).Select(i => i switch
        {
            1 => "This cell contains a lengthy explanation with many words that has to wrap inside its column.",
            2 => "https://example.org/very/long/unbreakable/path/with/identifiers/and/detailed/names",
            _ => $"{i * 1000}"
        }));
        return $"| {header} |\n| {divider} |\n| {row} |\n";
    }

    private const string Prose =
        "several ordinary words that wrap several ordinary words that wrap several ordinary words that wrap " +
        "several ordinary words that wrap several ordinary words that wrap several ordinary words that wrap";

    // Two wordy columns keep the middle one too narrow for a long token.
    private static string NarrowColumnTable(string cell) =>
        $"| Notes | Value | More |\n|-------|-------|------|\n| {Prose} | {cell} | {Prose} |\n";

    // The cell as it is rendered when nothing has to break: alone in a table as wide as the page.
    private static string PlainCell(string cell)
    {
        var rendered = LatexTableSource.Single(MarkdownLatexRenderer.Render($"| Value |\n|---|\n| {cell} |\n")).Rows[1][0];

        Assert.DoesNotContain(@"\allowbreak", rendered);
        Assert.DoesNotContain(@"\-", rendered);
        return rendered;
    }
}
