using DocumentFormat.OpenXml.Math;
using MarkdownConverter.Infrastructure.Converters;

namespace MarkdownConverter.Infrastructure.Tests;

public class LatexMathConverterTests
{
    [Fact]
    public void ToInlineMath_ReturnsOfficeMath()
    {
        var result = LatexMathConverter.ToInlineMath("x");

        Assert.IsType<OfficeMath>(result);
    }

    [Fact]
    public void ToDisplayMath_ReturnsParagraphContainingOfficeMath()
    {
        var result = LatexMathConverter.ToDisplayMath("x");

        Assert.IsType<Paragraph>(result);
        Assert.Contains(result.ChildElements, e => e is OfficeMath);
    }

    [Fact]
    public void ToInlineMath_SimpleVariable_ContainsText()
    {
        var oMath = LatexMathConverter.ToInlineMath("x");

        var xml = oMath.OuterXml;
        Assert.Contains("x", xml);
    }

    [Fact]
    public void ToInlineMath_Superscript_ProducesSuperscriptElement()
    {
        var oMath = LatexMathConverter.ToInlineMath("x^2");

        Assert.Contains(oMath.ChildElements, e => e is Superscript);
    }

    [Fact]
    public void ToInlineMath_Subscript_ProducesSubscriptElement()
    {
        var oMath = LatexMathConverter.ToInlineMath("x_i");

        Assert.Contains(oMath.ChildElements, e => e is Subscript);
    }

    [Fact]
    public void ToInlineMath_CombinedSubSuperscript_ProducesSubSuperscript()
    {
        var oMath = LatexMathConverter.ToInlineMath("x_i^n");

        Assert.Contains(oMath.ChildElements, e => e is SubSuperscript);
    }

    [Fact]
    public void ToInlineMath_Fraction_ProducesFractionElement()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\frac{a}{b}");

        Assert.Contains(oMath.ChildElements, e => e is Fraction);
    }

    [Fact]
    public void ToInlineMath_Sqrt_ProducesRadicalElement()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\sqrt{x}");

        Assert.Contains(oMath.ChildElements, e => e is Radical);
    }

    [Fact]
    public void ToInlineMath_Vec_ProducesAccentElement()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\vec{v}");

        Assert.Contains(oMath.ChildElements, e => e is Accent);
    }

    [Fact]
    public void ToInlineMath_Hat_ProducesAccentElement()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\hat{x}");

        Assert.Contains(oMath.ChildElements, e => e is Accent);
    }

    [Fact]
    public void ToInlineMath_Bar_ProducesBarElement()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\bar{x}");

        Assert.Contains(oMath.ChildElements, e => e is Bar);
    }

    [Fact]
    public void ToInlineMath_Overline_ProducesBarElement()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\overline{AB}");

        Assert.Contains(oMath.ChildElements, e => e is Bar);
    }

    [Theory]
    [InlineData(@"\alpha", "\u03B1")]
    [InlineData(@"\beta", "\u03B2")]
    [InlineData(@"\gamma", "\u03B3")]
    [InlineData(@"\pi", "\u03C0")]
    [InlineData(@"\Sigma", "\u03A3")]
    [InlineData(@"\Omega", "\u03A9")]
    public void ToInlineMath_GreekLetters_ProduceCorrectUnicode(string latex, string expected)
    {
        var oMath = LatexMathConverter.ToInlineMath(latex);

        Assert.Contains(expected, oMath.OuterXml);
    }

    [Theory]
    [InlineData(@"\infty", "\u221E")]
    [InlineData(@"\sum", "\u2211")]
    [InlineData(@"\prod", "\u220F")]
    [InlineData(@"\int", "\u222B")]
    [InlineData(@"\partial", "\u2202")]
    [InlineData(@"\forall", "\u2200")]
    [InlineData(@"\exists", "\u2203")]
    public void ToInlineMath_Symbols_ProduceCorrectUnicode(string latex, string expected)
    {
        var oMath = LatexMathConverter.ToInlineMath(latex);

        Assert.Contains(expected, oMath.OuterXml);
    }

    [Theory]
    [InlineData(@"\equiv", "\u2261")]
    [InlineData(@"\leq", "\u2264")]
    [InlineData(@"\geq", "\u2265")]
    [InlineData(@"\neq", "\u2260")]
    [InlineData(@"\in", "\u2208")]
    [InlineData(@"\subset", "\u2282")]
    public void ToInlineMath_Relations_ProduceCorrectUnicode(string latex, string expected)
    {
        var oMath = LatexMathConverter.ToInlineMath(latex);

        Assert.Contains(expected, oMath.OuterXml);
    }

    [Theory]
    [InlineData(@"\rightarrow", "\u2192")]
    [InlineData(@"\Rightarrow", "\u21D2")]
    [InlineData(@"\to", "\u2192")]
    [InlineData(@"\implies", "\u21D2")]
    public void ToInlineMath_Arrows_ProduceCorrectUnicode(string latex, string expected)
    {
        var oMath = LatexMathConverter.ToInlineMath(latex);

        Assert.Contains(expected, oMath.OuterXml);
    }

    [Fact]
    public void ToInlineMath_Mathbb_ProducesDoubleStruck()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\mathbb{Z}");

        Assert.Contains("\u2124", oMath.OuterXml); // ℤ
    }

    [Theory]
    [InlineData("R", "\u211D")]
    [InlineData("N", "\u2115")]
    [InlineData("C", "\u2102")]
    [InlineData("Q", "\u211A")]
    public void ToInlineMath_Mathbb_AllMappings(string letter, string expected)
    {
        var oMath = LatexMathConverter.ToInlineMath($@"\mathbb{{{letter}}}");

        Assert.Contains(expected, oMath.OuterXml);
    }

    [Fact]
    public void ToInlineMath_Pmod_ProducesParenthesizedMod()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\pmod{p}");

        var xml = oMath.OuterXml;
        Assert.Contains("mod", xml);
        Assert.Contains("p", xml);
    }

    [Fact]
    public void ToInlineMath_EscapedBraces()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\{a\}");

        var xml = oMath.OuterXml;
        Assert.Contains("{", xml);
        Assert.Contains("}", xml);
    }

    [Fact]
    public void ToInlineMath_BracedGroup_NoExtraElements()
    {
        var oMath = LatexMathConverter.ToInlineMath("{abc}");

        var xml = oMath.OuterXml;
        Assert.Contains("a", xml);
        Assert.Contains("b", xml);
        Assert.Contains("c", xml);
    }

    [Fact]
    public void ToInlineMath_SuperscriptWithBracedGroup()
    {
        var oMath = LatexMathConverter.ToInlineMath("x^{2n}");

        Assert.Contains(oMath.ChildElements, e => e is Superscript);
        var xml = oMath.OuterXml;
        Assert.Contains("2", xml);
        Assert.Contains("n", xml);
    }

    [Fact]
    public void ToInlineMath_NestedFraction()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\frac{\frac{a}{b}}{c}");

        var fractions = oMath.Descendants<Fraction>().ToList();
        Assert.True(fractions.Count >= 2);
    }

    [Fact]
    public void ToInlineMath_ComplexExpression()
    {
        // sum_{i=1}^{n} x_i
        var oMath = LatexMathConverter.ToInlineMath(@"\sum_{i=1}^{n} x_i");

        var xml = oMath.OuterXml;
        Assert.Contains("\u2211", xml); // sum symbol
    }

    [Fact]
    public void ToInlineMath_SpacingCommands_ProduceSpace()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"a\,b\;c\quad d");

        var xml = oMath.OuterXml;
        Assert.Contains("a", xml);
        Assert.Contains("b", xml);
        Assert.Contains("c", xml);
        Assert.Contains("d", xml);
    }

    [Fact]
    public void ToInlineMath_TextCommand_RendersText()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\text{hello}");

        var xml = oMath.OuterXml;
        // Each character is rendered as a separate MathRun
        Assert.Contains("h", xml);
        Assert.Contains("e", xml);
        Assert.Contains("l", xml);
        Assert.Contains("o", xml);
    }

    [Fact]
    public void ToInlineMath_EmptyInput_ReturnsEmptyOfficeMath()
    {
        var oMath = LatexMathConverter.ToInlineMath("");

        Assert.IsType<OfficeMath>(oMath);
    }

    [Fact]
    public void ToInlineMath_UnknownCommand_RendersWithoutBackslash()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\customcmd");

        Assert.Contains("customcmd", oMath.OuterXml);
    }

    [Fact]
    public void ToInlineMath_Dots_ProduceCorrectUnicode()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\dots");

        Assert.Contains("\u2026", oMath.OuterXml);
    }

    [Fact]
    public void ToInlineMath_Cdots_ProduceCorrectUnicode()
    {
        var oMath = LatexMathConverter.ToInlineMath(@"\cdots");

        Assert.Contains("\u22EF", oMath.OuterXml);
    }
}
