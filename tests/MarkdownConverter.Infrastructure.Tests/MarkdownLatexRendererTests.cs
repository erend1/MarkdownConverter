using MarkdownConverter.Infrastructure.Converters;

namespace MarkdownConverter.Infrastructure.Tests;

public class MarkdownLatexRendererTests
{
    [Fact]
    public void Render_ProducesValidLatexDocument()
    {
        var result = MarkdownLatexRenderer.Render("Hello world");

        Assert.Contains(@"\documentclass", result);
        Assert.Contains(@"\begin{document}", result);
        Assert.Contains(@"\end{document}", result);
    }

    [Fact]
    public void Render_IncludesLmodernPackage()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains(@"\usepackage{lmodern}", result);
    }

    [Fact]
    public void Render_IncludesMathPackages()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains(@"\usepackage{amsmath,amssymb,amsfonts}", result);
    }

    [Theory]
    [InlineData(1, "section")]
    [InlineData(2, "subsection")]
    [InlineData(3, "subsubsection")]
    public void Render_HeadingsMapToCorrectCommands(int level, string expected)
    {
        var md = new string('#', level) + " Title";
        var result = MarkdownLatexRenderer.Render(md);

        Assert.Contains($@"\{expected}*{{Title}}", result);
    }

    [Fact]
    public void Render_BoldText()
    {
        var result = MarkdownLatexRenderer.Render("**bold**");

        Assert.Contains(@"\textbf{bold}", result);
    }

    [Fact]
    public void Render_ItalicText()
    {
        var result = MarkdownLatexRenderer.Render("*italic*");

        Assert.Contains(@"\textit{italic}", result);
    }

    [Fact]
    public void Render_InlineCode()
    {
        var result = MarkdownLatexRenderer.Render("`code`");

        Assert.Contains(@"\texttt{code}", result);
    }

    [Fact]
    public void Render_CodeBlock()
    {
        var result = MarkdownLatexRenderer.Render("```\ncode block\n```");

        Assert.Contains(@"\begin{lstlisting}", result);
        Assert.Contains("code block", result);
        Assert.Contains(@"\end{lstlisting}", result);
    }

    [Fact]
    public void Render_IncludesListingsPackage()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains(@"\usepackage{listings}", result);
        Assert.Contains(@"\usepackage{xcolor}", result);
    }

    [Fact]
    public void Render_LstsetConfigured()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains("backgroundcolor", result);
        Assert.Contains("frame=single", result);
    }

    [Fact]
    public void Render_CodeBlockWithLanguage_IncludesLanguageOption()
    {
        var result = MarkdownLatexRenderer.Render("```python\nprint('hi')\n```");

        Assert.Contains(@"\begin{lstlisting}[language={Python}]", result);
    }

    [Fact]
    public void Render_CodeBlockWithCSharp_MapsCorrectly()
    {
        var result = MarkdownLatexRenderer.Render("```csharp\nvar x = 1;\n```");

        Assert.Contains(@"\begin{lstlisting}[language={[Sharp]C}]", result);
    }

    [Fact]
    public void Render_CodeBlockWithUnknownLanguage_NoLanguageOption()
    {
        var result = MarkdownLatexRenderer.Render("```unknownlang\ncode\n```");

        Assert.Contains(@"\begin{lstlisting}", result);
        Assert.DoesNotContain("language=", result);
    }

    [Fact]
    public void Render_OrderedList()
    {
        var md = "1. first\n2. second";
        var result = MarkdownLatexRenderer.Render(md);

        Assert.Contains(@"\begin{enumerate}", result);
        Assert.Contains(@"\item first", result);
        Assert.Contains(@"\item second", result);
        Assert.Contains(@"\end{enumerate}", result);
    }

    [Fact]
    public void Render_UnorderedList()
    {
        var md = "- apple\n- banana";
        var result = MarkdownLatexRenderer.Render(md);

        Assert.Contains(@"\begin{itemize}", result);
        Assert.Contains(@"\item apple", result);
        Assert.Contains(@"\item banana", result);
        Assert.Contains(@"\end{itemize}", result);
    }

    [Fact]
    public void Render_HorizontalRule()
    {
        var result = MarkdownLatexRenderer.Render("---");

        Assert.Contains(@"\noindent\rule{\textwidth}{0.4pt}", result);
    }

    [Fact]
    public void Render_BlockQuote()
    {
        var result = MarkdownLatexRenderer.Render("> quoted text");

        Assert.Contains(@"\begin{mdquote}", result);
        Assert.Contains("quoted text", result);
        Assert.Contains(@"\end{mdquote}", result);
    }

    [Fact]
    public void Render_InlineMath_SingleDollar()
    {
        var result = MarkdownLatexRenderer.Render("Value $x^2$ here");

        Assert.Contains("$x^2$", result);
    }

    [Fact]
    public void Render_DisplayMath_DoubleDollar()
    {
        var result = MarkdownLatexRenderer.Render("$$E = mc^2$$");

        // Display math should use \[...\]
        Assert.Contains(@"\[", result);
        Assert.Contains(@"\]", result);
    }

    [Fact]
    public void Render_DisplayMath_FencedBlock()
    {
        var md = "$$\nx = y\n$$";
        var result = MarkdownLatexRenderer.Render(md);

        Assert.Contains(@"\[", result);
        Assert.Contains(@"\]", result);
    }

    [Fact]
    public void Render_Table()
    {
        var md = "| Col1 | Col2 |\n|------|------|\n| A    | B    |";
        var result = MarkdownLatexRenderer.Render(md);

        Assert.Contains(@"\begin{longtable}", result);
        Assert.Contains(@"\end{longtable}", result);
        Assert.Contains(@"\hline", result);
        Assert.Contains(@"\textbf{Col1}", result);
    }

    [Fact]
    public void Render_EscapesSpecialCharacters()
    {
        var result = MarkdownLatexRenderer.Render("Price is 10% & tax #1");

        Assert.Contains(@"\%", result);
        Assert.Contains(@"\&", result);
        Assert.Contains(@"\#", result);
    }

    [Fact]
    public void Render_EscapesUnderscoresInText()
    {
        var result = MarkdownLatexRenderer.Render("variable_name");

        Assert.Contains(@"\_", result);
    }

    [Fact]
    public void Render_EscapesTildeInText()
    {
        var result = MarkdownLatexRenderer.Render("value~here");

        Assert.Contains(@"\textasciitilde{}", result);
    }

    [Fact]
    public void Render_Link()
    {
        var result = MarkdownLatexRenderer.Render("[Google](https://google.com)");

        Assert.Contains(@"\href{https://google.com}{Google}", result);
    }

    [Fact]
    public void Render_Image()
    {
        var result = MarkdownLatexRenderer.Render("![alt](image.png)");

        Assert.Contains(@"\textit{[Image:", result);
    }

    [Fact]
    public void Render_EmptyInput()
    {
        var result = MarkdownLatexRenderer.Render("");

        Assert.Contains(@"\begin{document}", result);
        Assert.Contains(@"\end{document}", result);
    }

    [Fact]
    public void Render_WithBibliography_IncludesBiblatexPreamble()
    {
        var result = MarkdownLatexRenderer.Render("test", "references.bib");

        Assert.Contains(@"\usepackage[style=apa,backend=biber]{biblatex}", result);
        Assert.Contains(@"\addbibresource{references.bib}", result);
    }

    [Fact]
    public void Render_WithBibliography_IncludesPrintbibliography()
    {
        var result = MarkdownLatexRenderer.Render("test", "references.bib");

        Assert.Contains(@"\printbibliography", result);
    }

    [Fact]
    public void Render_WithoutBibliography_NoBiblatexPreamble()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.DoesNotContain("biblatex", result);
        Assert.DoesNotContain(@"\printbibliography", result);
    }

    [Fact]
    public void Render_WithCitation_EmitsCiteCommand()
    {
        var result = MarkdownLatexRenderer.Render("See [@smith2023] for details.");

        Assert.Contains(@"\cite{smith2023}", result);
    }

    [Fact]
    public void Render_WithMultipleCitations_EmitsMultipleKeys()
    {
        var result = MarkdownLatexRenderer.Render("See [@smith2023; @jones2024].");

        Assert.Contains(@"\cite{smith2023,jones2024}", result);
    }

    [Fact]
    public void Render_WithCitationLocator_EmitsCiteWithOption()
    {
        var result = MarkdownLatexRenderer.Render("See [@smith2023, p. 42].");

        Assert.Contains(@"\cite[p. 42]{smith2023}", result);
    }

    // --- Enhanced Blockquote Tests ---

    [Fact]
    public void Render_Preamble_IncludesTcolorbox()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains(@"\usepackage[most]{tcolorbox}", result);
        Assert.Contains(@"\usepackage{fontawesome5}", result);
    }

    [Fact]
    public void Render_Preamble_DefinesCalloutEnvironments()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains(@"\newtcolorbox{callout-note}", result);
        Assert.Contains(@"\newtcolorbox{callout-warning}", result);
        Assert.Contains(@"\newtcolorbox{callout-tip}", result);
        Assert.Contains(@"\newtcolorbox{callout-important}", result);
        Assert.Contains(@"\newtcolorbox{callout-caution}", result);
    }

    [Fact]
    public void Render_Preamble_DefinesBlockquoteStyle()
    {
        var result = MarkdownLatexRenderer.Render("test");

        Assert.Contains(@"\newtcolorbox{mdquote}", result);
    }

    [Fact]
    public void Render_BlockQuote_UsesItalic()
    {
        var result = MarkdownLatexRenderer.Render("> styled quote");

        Assert.Contains(@"\itshape", result);
    }

    [Fact]
    public void Render_NestedBlockQuote_ProducesNestedEnvironments()
    {
        var result = MarkdownLatexRenderer.Render("> outer\n> > inner");

        // Should contain two mdquote environments (one nested)
        var count = result.Split(@"\begin{mdquote}").Length - 1;
        Assert.True(count >= 2, $"Expected at least 2 mdquote begins, found {count}");
    }

    [Fact]
    public void Render_BlockQuoteWithAttribution_RendersAuthor()
    {
        var result = MarkdownLatexRenderer.Render("> Be the change.\n>\n> \u2014 Gandhi");

        Assert.Contains("Gandhi", result);
        Assert.Contains(@"\hfill", result);
    }

    [Theory]
    [InlineData("NOTE", "callout-note")]
    [InlineData("WARNING", "callout-warning")]
    [InlineData("TIP", "callout-tip")]
    [InlineData("IMPORTANT", "callout-important")]
    [InlineData("CAUTION", "callout-caution")]
    public void Render_Callout_ProducesCorrectEnvironment(string type, string envName)
    {
        var result = MarkdownLatexRenderer.Render($"> [!{type}]\n> Content here");

        Assert.Contains($@"\begin{{{envName}}}", result);
        Assert.Contains($@"\end{{{envName}}}", result);
    }

    [Fact]
    public void Render_CalloutNote_RendersContent()
    {
        var result = MarkdownLatexRenderer.Render("> [!NOTE]\n> Important information here");

        Assert.Contains(@"\begin{callout-note}", result);
        Assert.Contains("Important information here", result);
        Assert.Contains(@"\end{callout-note}", result);
    }

    [Fact]
    public void Render_CalloutDoesNotProduceBlockquote()
    {
        var result = MarkdownLatexRenderer.Render("> [!NOTE]\n> Content");

        // AlertBlock should NOT produce mdquote — it should use callout-note
        Assert.DoesNotContain(@"\begin{mdquote}", result);
        Assert.Contains(@"\begin{callout-note}", result);
    }

    [Fact]
    public void Render_PlainBlockQuote_StillWorks()
    {
        var result = MarkdownLatexRenderer.Render("> Just a simple quote");

        Assert.Contains(@"\begin{mdquote}", result);
        Assert.Contains("Just a simple quote", result);
        Assert.Contains(@"\end{mdquote}", result);
    }

    // --- Escaping: backslash and dollar sign ---

    [Fact]
    public void Render_EscapesBackslashInText()
    {
        var result = MarkdownLatexRenderer.Render(@"path\to\file");

        Assert.Contains(@"\textbackslash{}", result);
    }

    [Fact]
    public void Render_EscapesDollarSignInText()
    {
        var result = MarkdownLatexRenderer.Render("costs $5 today");

        // The $ should be escaped to \$ so it doesn't start math mode
        Assert.Contains(@"\$", result);
    }

    [Fact]
    public void Render_EscapesBackslashInInlineCode()
    {
        var result = MarkdownLatexRenderer.Render(@"`nonce\|\|ct`");

        // Backslash inside inline code must be escaped to prevent LaTeX commands
        Assert.Contains(@"\textbackslash{}", result);
        Assert.DoesNotContain(@"\|", result.Replace(@"\textbackslash{}", ""));
    }

    // --- Table column specification ---

    [Fact]
    public void Render_Table_ColumnSpecHasSingleBackslash()
    {
        var md = "| Col1 | Col2 |\n|------|------|\n| A    | B    |";
        var result = MarkdownLatexRenderer.Render(md);

        // Column spec must use single backslash (\raggedright) not double (\\raggedright)
        Assert.Contains(@"\raggedright", result);
        Assert.DoesNotContain(@"\\raggedright", result);
        Assert.Contains(@"\arraybackslash", result);
        Assert.DoesNotContain(@"\\arraybackslash", result);
    }

    // --- Code block sanitization ---

    [Fact]
    public void Render_CodeBlock_SanitizesBoxDrawingChars()
    {
        var md = "```\n═══════\n├─ item\n└─ end\n```";
        var result = MarkdownLatexRenderer.Render(md);

        // Box-drawing characters should be replaced with ASCII equivalents
        Assert.DoesNotContain("═", result);
        Assert.DoesNotContain("├", result);
        Assert.DoesNotContain("└", result);
        Assert.Contains("-------", result);
        Assert.Contains("+- item", result);
        Assert.Contains("+- end", result);
    }

    [Fact]
    public void Render_CodeBlock_PreservesAccentedCharsInListings()
    {
        var md = "```\n// Örnek girdi\n```";
        var result = MarkdownLatexRenderer.Render(md);

        // Accented Latin chars (covered by literate mappings) should be preserved
        Assert.Contains("Ö", result);
    }

    // --- Hyperref link styling ---

    [Fact]
    public void Render_Preamble_HidelinksOption()
    {
        var result = MarkdownLatexRenderer.Render("test");

        // Links should not have colored/bordered boxes in PDF
        Assert.Contains(@"\usepackage[hidelinks]{hyperref}", result);
    }

    // --- Code block spacing ---

    [Fact]
    public void Render_CodeBlock_HasLeavemodeBefore()
    {
        var md = "### Title\n\n```\ncode\n```";
        var result = MarkdownLatexRenderer.Render(md);

        // leavevmode before lstlisting prevents heading overlap
        Assert.Contains(@"\leavevmode", result);
    }
}
