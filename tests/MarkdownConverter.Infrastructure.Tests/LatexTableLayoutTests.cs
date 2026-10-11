using MarkdownConverter.Infrastructure.Converters;

namespace MarkdownConverter.Infrastructure.Tests;

public class LatexTableLayoutTests
{
    // Content budgets of the generated A4 document, in em: (452.97pt - padding - rules) / 10.95pt.
    private const double ThreeColumnBudgetEm = 38.006;
    private const double FourColumnBudgetEm = 36.874;

    // --- Density ---

    [Theory]
    [InlineData(1, nameof(LatexTableDensity.Normal))]
    [InlineData(10, nameof(LatexTableDensity.Normal))]
    [InlineData(11, nameof(LatexTableDensity.Compact))]
    [InlineData(26, nameof(LatexTableDensity.Compact))]
    [InlineData(27, nameof(LatexTableDensity.Bare))]
    [InlineData(500, nameof(LatexTableDensity.Bare))]
    public void Plan_ChoosesTheDensityFromTheColumnCount(int columnCount, string expected)
    {
        var plan = Plan(new double[columnCount], new double[columnCount]);

        Assert.Equal(expected, plan.Density.ToString());
        Assert.Equal(columnCount, plan.ColumnCount);
    }

    [Fact]
    public void Plan_DescribesThePaddingAndSeparatorOfEachDensity()
    {
        var normal = Plan(new double[3], new double[3]);
        var compact = Plan(new double[12], new double[12]);
        var bare = Plan(new double[40], new double[40]);

        Assert.Null(normal.CellPadding);
        Assert.Equal(" | ", normal.ColumnSeparator);
        Assert.Equal("3pt", compact.CellPadding);
        Assert.Equal(" | ", compact.ColumnSeparator);
        Assert.Equal("0pt", bare.CellPadding);
        Assert.Equal(" | ", bare.ColumnSeparator);
    }

    [Fact]
    public void Plan_DropsTheRulesOnlyWhereTheyAloneWouldUseUpTheLine()
    {
        Assert.Equal(" | ", Plan(new double[566], new double[566]).ColumnSeparator);
        Assert.Equal(" ", Plan(new double[568], new double[568]).ColumnSeparator);
        Assert.Equal(@"0.0017\dimexpr\linewidth-1136\tabcolsep\relax", Plan(new double[568], new double[568]).ColumnWidth(0));
    }

    // --- Width expressions ---

    [Fact]
    public void ColumnWidth_IsAShareOfTheLineLeftAfterPaddingAndRules()
    {
        var plan = Plan(new double[3], new double[3]);

        Assert.Equal(@"0.3333\dimexpr\linewidth-6\tabcolsep-2\arrayrulewidth\relax", plan.ColumnWidth(0));
    }

    [Fact]
    public void ColumnWidth_SingleColumn_HasNoRuleToPayFor()
    {
        var plan = Plan(new double[1], new double[1]);

        Assert.Equal(@"0.9999\dimexpr\linewidth-2\tabcolsep\relax", plan.ColumnWidth(0));
    }

    [Fact]
    public void ColumnWidth_BareTable_UsesTheSameBudgetWithItsPaddingSetToZero()
    {
        var plan = Plan(new double[40], new double[40]);

        Assert.Equal(@"0.0249\dimexpr\linewidth-80\tabcolsep-39\arrayrulewidth\relax", plan.ColumnWidth(0));
    }

    // --- Allocation ---

    [Fact]
    public void Plan_WithRoomToSpare_WidensTheNarrowestColumnsToACommonWidth()
    {
        var plan = Plan([2, 20, 3], [2, 5, 3]);

        // The middle column keeps the width of its text; the other two share the rest.
        Assert.Equal(21.6, plan.ColumnWidthEm(1), 1);
        Assert.Equal(plan.Share(0), plan.Share(2));
        Assert.Equal((ThreeColumnBudgetEm - 21.6) / 2, plan.ColumnWidthEm(0), 1);
    }

    [Fact]
    public void Plan_WithoutRoom_WrapsOnlyTheWidestColumns()
    {
        var plan = Plan([3, 100, 60, 4], [3, 5, 5, 4]);

        // The short columns keep their text on one line; the two long ones are treated alike.
        Assert.Equal(3 * 1.08, plan.ColumnWidthEm(0), 1);
        Assert.Equal(4 * 1.08, plan.ColumnWidthEm(3), 1);
        Assert.Equal(plan.Share(1), plan.Share(2));
        Assert.Equal((FourColumnBudgetEm - 7 * 1.08) / 2, plan.ColumnWidthEm(1), 1);
    }

    [Fact]
    public void Plan_NeverPlansAColumnBelowTheMinimumWidth_WhileTheTableHasRoom()
    {
        var plan = Plan([0, 500, 0.5], [0, 6, 0.5]);

        Assert.Equal(2, plan.ColumnWidthEm(0), 1);
        Assert.Equal(2, plan.ColumnWidthEm(2), 1);
    }

    [Fact]
    public void Plan_KeepsEveryColumnAsWideAsItsLongestWord()
    {
        var plan = Plan([200, 200, 200, 200], [9, 4, 6, 3]);

        for (int column = 0; column < 4; column++)
        {
            Assert.True(LatexTableLayout.Fits(new[] { 9.0, 4, 6, 3 }[column], plan.ColumnWidthEm(column)));
        }
    }

    [Fact]
    public void Plan_VeryLongToken_DoesNotStarveTheOtherColumns()
    {
        // A 60em URL next to prose: the URL column is protected up to 12em and then shares.
        var plan = Plan([60, 80, 3], [60, 5, 3]);

        Assert.True(plan.ColumnWidthEm(0) >= 12 - 0.1);
        Assert.Equal(plan.Share(0), plan.Share(1));
        Assert.Equal(3 * 1.08, plan.ColumnWidthEm(2), 1);
    }

    [Fact]
    public void Plan_UnbreakableContent_IsProtectedBeyondTheWordCap()
    {
        // A formula part of 20em cannot be given break opportunities the way a long word can.
        var plan = LatexTableLayout.Plan([25, 80, 3], [20, 5, 3], [20, 0, 0]);

        Assert.True(LatexTableLayout.Fits(20, plan.ColumnWidthEm(0)));
        Assert.True(LatexTableLayout.Fits(5, plan.ColumnWidthEm(1)));
        Assert.True(LatexTableLayout.Fits(3, plan.ColumnWidthEm(2)));
    }

    [Fact]
    public void Plan_UnbreakableContentWiderThanTheTable_LeavesTheOtherColumnsAComfortableWidth()
    {
        var plan = LatexTableLayout.Plan([90, 80, 30], [90, 5, 5], [90, 0, 0]);

        // The formula may claim what is left when each other column keeps 3em, no more.
        Assert.False(LatexTableLayout.Fits(90, plan.ColumnWidthEm(0)));
        Assert.Equal(ThreeColumnBudgetEm - 6, plan.ColumnWidthEm(0), 1);
        Assert.Equal(3, plan.ColumnWidthEm(1), 1);
        Assert.Equal(3, plan.ColumnWidthEm(2), 1);
    }

    [Fact]
    public void Plan_WhenWordsCannotAllFit_ShortensTheLongestWordsFirst()
    {
        // Ten columns have about 30em between them; these words ask for more.
        var words = new double[] { 9, 9, 2, 2, 2, 2, 2, 2, 2, 2 };
        var plan = Plan(words, words);

        for (int column = 2; column < 10; column++)
        {
            Assert.True(LatexTableLayout.Fits(2, plan.ColumnWidthEm(column)));
        }
        Assert.False(LatexTableLayout.Fits(9, plan.ColumnWidthEm(0)));
        Assert.Equal(plan.Share(0), plan.Share(1));
        Assert.True(plan.ColumnWidthEm(0) > 5);
    }

    [Fact]
    public void Plan_DenseTable_LetsNarrowColumnsGiveUpTheMinimumWidth()
    {
        // Twenty columns have under 30em between them: one label column, nineteen of two digits.
        var words = Enumerable.Repeat(1.0, 20).ToArray();
        words[0] = 5;
        var plan = Plan(words, words);

        Assert.True(LatexTableLayout.Fits(5, plan.ColumnWidthEm(0)));
        for (int column = 1; column < 20; column++)
        {
            Assert.True(LatexTableLayout.Fits(1, plan.ColumnWidthEm(column)));
            Assert.True(plan.ColumnWidthEm(column) < 2);
            Assert.Equal(plan.Share(1), plan.Share(column));
        }
    }

    [Fact]
    public void Plan_WhenNoWordFits_SharesTheLineBetweenTheColumnsThatNeedIt()
    {
        var words = Enumerable.Range(0, 20).Select(i => i < 2 ? 0.5 : 6.0).ToArray();
        var plan = Plan(words, words);

        // The two narrow columns keep their content; the other eighteen split the rest evenly.
        Assert.True(LatexTableLayout.Fits(0.5, plan.ColumnWidthEm(0)));
        Assert.True(plan.ColumnWidthEm(0) < plan.ColumnWidthEm(2));
        Assert.Single(Enumerable.Range(2, 18).Select(plan.Share).Distinct());
        Assert.False(LatexTableLayout.Fits(6, plan.ColumnWidthEm(2)));
    }

    [Fact]
    public void Plan_SharesAreNeverNegativeAndLeaveRoomForTexRounding()
    {
        for (int columnCount = 1; columnCount <= 200; columnCount++)
        {
            foreach (var natural in ContentShapes(columnCount))
            {
                var plan = Plan(natural, natural.Select(width => Math.Min(width, 7)).ToArray());
                var shares = Enumerable.Range(0, columnCount).Select(plan.Share).ToList();

                Assert.All(shares, share => Assert.InRange(share, 0, LatexTableLayout.ShareScale));
                // TeX may round every decimal factor up by half of 2^-16.
                Assert.True(
                    shares.Sum() / (double)LatexTableLayout.ShareScale + columnCount / 131072.0 <= 1,
                    $"{columnCount} columns claim {shares.Sum()} of {LatexTableLayout.ShareScale}");
            }
        }
    }

    [Fact]
    public void Plan_IsDeterministic()
    {
        double[] natural = [3.1, 75.4, 12.25, 0, 40];
        double[] words = [3.1, 5.2, 6.75, 0, 8];

        var first = Plan(natural, words);
        var second = Plan(natural, words);

        Assert.Equal(
            Enumerable.Range(0, 5).Select(first.ColumnWidth),
            Enumerable.Range(0, 5).Select(second.ColumnWidth));
    }

    [Fact]
    public void Plan_EmptyTable_HasNoColumns()
    {
        var plan = Plan([], []);

        Assert.Equal(0, plan.ColumnCount);
    }

    // --- Width estimates ---

    [Theory]
    [InlineData('0', false, false, 0.5)]
    [InlineData('i', false, false, 0.278)]
    [InlineData('W', false, false, 1.028)]
    [InlineData('_', false, false, 0.75)]
    [InlineData('W', true, false, 1.2336)]
    [InlineData('i', false, true, 0.525)]
    [InlineData('W', true, true, 0.525)]
    [InlineData('ş', false, false, 0.6)]
    [InlineData('Ç', false, false, 0.8)]
    [InlineData('ş', false, true, 0.525)]
    [InlineData('→', false, false, 1)]
    [InlineData('→', true, false, 1.2)]
    [InlineData('…', false, false, 1.34)]               // three periods, wider than the other symbols
    [InlineData('…', true, false, 1.608)]
    [InlineData('…', false, true, 1.6)]                 // and three characters in typewriter type
    [InlineData('×', false, true, 0.8)]                 // set in the math font even inside code
    public void CharWidthEm_FollowsTheFontTheCharacterIsSetIn(char ch, bool bold, bool monospace, double expected)
    {
        Assert.Equal(expected, LatexTableLayout.CharWidthEm(ch, bold, monospace), 4);
    }

    // --- Chunks ---

    [Theory]
    [InlineData(4, 100, null)]      // 400 cells: the package default is fine
    [InlineData(4, 101, "20")]
    [InlineData(2, 1000, "20")]     // never more than twenty rows
    [InlineData(40, 200, "10")]     // about 400 cells
    [InlineData(100, 50, "4")]
    [InlineData(600, 3, "1")]
    public void ChunkRows_BoundsTheCellsTypesetAtOnce(int columnCount, int rowCount, string? expected)
    {
        Assert.Equal(expected, LatexTableLayout.ChunkRows(columnCount, rowCount));
    }

    // --- Line width of the container ---

    [Fact]
    public void Plan_NarrowerLine_LeavesLessRoomForTheSameContent()
    {
        double[] natural = [3, 100, 60, 4];
        double[] words = [3, 5, 5, 4];

        var page = LatexTableLayout.Plan(natural, words, new double[4]);
        var nestedList = LatexTableLayout.Plan(natural, words, new double[4], lineWidthPt: 401.5);

        // The short columns keep their text; the wrapped ones pay for the narrower line.
        Assert.Equal(page.ColumnWidthEm(0), nestedList.ColumnWidthEm(0), 1);
        Assert.Equal((401.5 - 49.2) / 10.95, Enumerable.Range(0, 4).Sum(nestedList.ColumnWidthEm), 1);
        Assert.True(nestedList.ColumnWidthEm(1) < page.ColumnWidthEm(1) - 2);
    }

    [Fact]
    public void Plan_NarrowerLine_TightensThePaddingSooner()
    {
        Assert.Equal(LatexTableDensity.Normal, Plan(new double[10], new double[10]).Density);
        Assert.Equal(
            LatexTableDensity.Compact,
            LatexTableLayout.Plan(new double[10], new double[10], new double[10], lineWidthPt: 428.97).Density);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-200)]
    [InlineData(5)]
    public void Plan_LineTooNarrowForAnything_StillYieldsPositiveShares(double lineWidthPt)
    {
        var plan = LatexTableLayout.Plan([4, 30, 4], [4, 5, 4], new double[3], lineWidthPt);

        Assert.All(Enumerable.Range(0, 3), column => Assert.True(plan.Share(column) > 0));
        Assert.True(Enumerable.Range(0, 3).Sum(plan.Share) < LatexTableLayout.ShareScale);
    }

    [Theory]
    [InlineData("x", 0.55)]
    [InlineData("X", 0.77)]                             // capitals are wider
    [InlineData(@"\alpha", 0.825)]                      // one symbol, however long its name
    [InlineData(@"\frac{a}{b}", 1.1)]                   // \frac only arranges its arguments
    [InlineData(@"\hat{\beta}", 0.825)]
    [InlineData("x^2 + y^2 = z^2", 5.94)]               // operators carry space around them
    [InlineData(@"a \leq b", 2.42)]
    [InlineData(@"P \implies Q", 4.29)]
    [InlineData(@"\log n", 2.2)]                        // a function name is set as a word
    [InlineData(@"a\,b", 1.375)]
    [InlineData("", 0)]
    [InlineData(@"\", 0.275)]
    public void MathWidthEm_CountsGlyphsNotSourceCharacters(string tex, double expected)
    {
        Assert.Equal(expected, LatexTableLayout.MathWidthEm(tex).TotalEm, 4);
    }

    [Theory]
    [InlineData("x", 0.55)]                             // nothing to break at
    [InlineData("a = b", 1.87)]                         // "a =" then "b"
    [InlineData("x^2 + y^2 = z^2", 2.42)]               // "x^2 +", "y^2 =", "z^2"
    [InlineData(@"a \leq b \times c", 1.87)]            // named operators break as well
    [InlineData("-x", 1.32)]                            // a leading sign is not an operator
    [InlineData("x = -1", 1.87)]                        // nor is a sign after a relation
    [InlineData("(-0.12, -0.34)", 7.59)]                // or after a bracket or comma: no break at all
    [InlineData("x^-1 y", 2.42)]                        // or the sign of an exponent
    [InlineData(@"\frac{a + b}{c + d}", 4.84)]          // no break inside braces
    [InlineData(@"\left(a + b\right)", 3.52)]           // nor between \left and \right
    [InlineData(@"\begin{pmatrix} a + b & c \end{pmatrix}", 2.97)]   // nor inside an environment
    public void MathWidthEm_ReportsTheWidestPartThatCannotBreak(string tex, double expected)
    {
        Assert.Equal(expected, LatexTableLayout.MathWidthEm(tex).UnbreakableEm, 4);
    }

    [Fact]
    public void MathWidthEm_DoesNotCountTheNameOfAnEnvironment()
    {
        var matrix = LatexTableLayout.MathWidthEm(@"\begin{pmatrix} a & b \end{pmatrix}");

        Assert.Equal(1.1, matrix.TotalEm, 4);
    }

    [Fact]
    public void Fits_LeavesASafetyMarginAroundTheEstimate()
    {
        Assert.True(LatexTableLayout.Fits(10, 10.8));
        Assert.False(LatexTableLayout.Fits(10, 10.7));
        Assert.True(LatexTableLayout.Fits(0, 0));
    }

    [Theory]
    [InlineData(32, 64, 16, 29.63)]     // a 64-character hash in a column half its width
    [InlineData(2, 4, 1.5, 2.78)]       // four digits in a column that holds fewer than three
    [InlineData(0, 3, 5, 3)]            // nothing rendered: nothing to divide
    public void CharactersPerLine_ScalesTheRunToTheColumn(
        double estimatedEm, int characters, double columnEm, double expected)
    {
        Assert.Equal(expected, LatexTableLayout.CharactersPerLine(estimatedEm, characters, columnEm), 2);
    }

    [Fact]
    public void CellMeasure_TracksTheLineAndItsWidestWord()
    {
        var measure = new LatexTableLayout.CellMeasure();

        measure.AddSpace(0.3);          // leading space takes no room
        measure.AddGlyph(1);
        measure.AddGlyph(1);
        measure.AddSpace(0.3);
        measure.AddSpace(0.3);          // repeated spaces collapse
        measure.AddGlyph(3);
        measure.AddSpace(0.3);          // trailing space takes no room

        Assert.Equal(5.3, measure.NaturalEm, 6);
        Assert.Equal(3, measure.WidestWordEm, 6);
        Assert.Equal(0, measure.UnbreakableEm, 6);
    }

    [Fact]
    public void CellMeasure_Box_CountsInFullOnTheLineButOnlyItsUnbreakablePartAsAWord()
    {
        var measure = new LatexTableLayout.CellMeasure();

        measure.AddGlyph(1);
        measure.AddBox(10, 4);          // a formula glued to the preceding text

        Assert.Equal(11, measure.NaturalEm, 6);
        Assert.Equal(5, measure.WidestWordEm, 6);
        Assert.Equal(4, measure.UnbreakableEm, 6);
    }

    private static LatexTablePlan Plan(double[] naturalEm, double[] widestWordEm) =>
        LatexTableLayout.Plan(naturalEm, widestWordEm, new double[naturalEm.Length]);

    // A few very different tables of the same column count.
    private static IEnumerable<double[]> ContentShapes(int columnCount)
    {
        yield return new double[columnCount];
        yield return Enumerable.Repeat(4.0, columnCount).ToArray();
        yield return Enumerable.Repeat(300.0, columnCount).ToArray();
        yield return Enumerable.Range(0, columnCount).Select(i => i % 3 == 0 ? 120.0 : 1.5).ToArray();
        yield return Enumerable.Range(0, columnCount).Select(i => (i * 37 % 11) * 3.3).ToArray();
    }
}
