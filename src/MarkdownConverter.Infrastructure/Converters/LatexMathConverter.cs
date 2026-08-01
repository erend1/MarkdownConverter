using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Math;

namespace MarkdownConverter.Infrastructure.Converters;

/// <summary>
/// Converts LaTeX math strings to OpenXml Math (OMML) elements
/// for native rendering in Word's formula engine.
/// </summary>
internal static class LatexMathConverter
{
    public static OfficeMath ToInlineMath(string latex)
    {
        var oMath = new OfficeMath();
        var tokens = Tokenize(latex.Trim());
        int pos = 0;
        var elements = ParseSequence(tokens, ref pos, untilCloseBrace: false);
        foreach (var el in elements)
            oMath.Append(el);
        return oMath;
    }

    public static Paragraph ToDisplayMath(string latex)
    {
        var oMathPara = new Paragraph();
        oMathPara.Append(ToInlineMath(latex));
        return oMathPara;
    }

    #region Tokenizer

    private enum TokenType { Text, Command, OpenBrace, CloseBrace, Super, Sub }
    private record Token(TokenType Type, string Value);

    private static List<Token> Tokenize(string latex)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < latex.Length)
        {
            char c = latex[i];
            switch (c)
            {
                case '{':
                    tokens.Add(new Token(TokenType.OpenBrace, "{"));
                    i++;
                    break;
                case '}':
                    tokens.Add(new Token(TokenType.CloseBrace, "}"));
                    i++;
                    break;
                case '^':
                    tokens.Add(new Token(TokenType.Super, "^"));
                    i++;
                    break;
                case '_':
                    tokens.Add(new Token(TokenType.Sub, "_"));
                    i++;
                    break;
                case '\\':
                    i++;
                    if (i < latex.Length && !char.IsLetter(latex[i]))
                    {
                        // Single-char command: \{, \}, \\, \,, \;, etc.
                        tokens.Add(new Token(TokenType.Command, $"\\{latex[i]}"));
                        i++;
                    }
                    else
                    {
                        int start = i;
                        while (i < latex.Length && char.IsLetter(latex[i])) i++;
                        tokens.Add(new Token(TokenType.Command, $"\\{latex[start..i]}"));
                    }
                    break;
                case ' ' or '\t' or '\r' or '\n':
                    while (i < latex.Length && char.IsWhiteSpace(latex[i])) i++;
                    tokens.Add(new Token(TokenType.Text, " "));
                    break;
                default:
                    tokens.Add(new Token(TokenType.Text, c.ToString()));
                    i++;
                    break;
            }
        }
        return tokens;
    }

    #endregion

    #region Parser

    private static List<OpenXmlElement> ParseSequence(
        List<Token> tokens, ref int pos, bool untilCloseBrace)
    {
        var elements = new List<OpenXmlElement>();

        while (pos < tokens.Count)
        {
            var token = tokens[pos];

            if (token.Type == TokenType.CloseBrace)
            {
                if (untilCloseBrace) { pos++; break; }
                pos++;
                continue;
            }

            if (token.Type == TokenType.Super || token.Type == TokenType.Sub)
            {
                // Attach to previous element as base
                OpenXmlElement baseEl = elements.Count > 0
                    ? elements[^1] : MathRun("");
                if (elements.Count > 0)
                    elements.RemoveAt(elements.Count - 1);

                bool isSuper = token.Type == TokenType.Super;
                pos++;
                var arg = ParseSingleItem(tokens, ref pos);

                // Check for combined sub+super (e.g. x_i^n or x^n_i)
                if (pos < tokens.Count
                    && (tokens[pos].Type == TokenType.Super || tokens[pos].Type == TokenType.Sub)
                    && tokens[pos].Type != token.Type)
                {
                    bool secondIsSuper = tokens[pos].Type == TokenType.Super;
                    pos++;
                    var arg2 = ParseSingleItem(tokens, ref pos);

                    var subSup = new SubSuperscript();
                    var baseArg = new Base();
                    baseArg.Append(baseEl);
                    subSup.Append(baseArg);

                    var subArg = new SubArgument();
                    var supArg = new SuperArgument();

                    if (isSuper)
                    {
                        foreach (var e in arg) supArg.Append(e);
                        foreach (var e in arg2) subArg.Append(e);
                    }
                    else
                    {
                        foreach (var e in arg) subArg.Append(e);
                        foreach (var e in arg2) supArg.Append(e);
                    }

                    subSup.Append(subArg);
                    subSup.Append(supArg);
                    elements.Add(subSup);
                }
                else if (isSuper)
                {
                    var sup = new Superscript();
                    var baseArg = new Base();
                    baseArg.Append(baseEl);
                    sup.Append(baseArg);
                    var supArg = new SuperArgument();
                    foreach (var e in arg) supArg.Append(e);
                    sup.Append(supArg);
                    elements.Add(sup);
                }
                else
                {
                    var sub = new Subscript();
                    var baseArg = new Base();
                    baseArg.Append(baseEl);
                    sub.Append(baseArg);
                    var subArg = new SubArgument();
                    foreach (var e in arg) subArg.Append(e);
                    sub.Append(subArg);
                    elements.Add(sub);
                }
                continue;
            }

            if (token.Type == TokenType.OpenBrace)
            {
                pos++;
                var group = ParseSequence(tokens, ref pos, untilCloseBrace: true);
                elements.AddRange(group);
                continue;
            }

            if (token.Type == TokenType.Command)
            {
                pos++;
                var el = HandleCommand(token.Value, tokens, ref pos);
                elements.AddRange(el);
                continue;
            }

            // Regular text character
            pos++;
            elements.Add(MathRun(token.Value));
        }

        return elements;
    }

    private static List<OpenXmlElement> ParseSingleItem(List<Token> tokens, ref int pos)
    {
        // Skip spaces
        while (pos < tokens.Count && tokens[pos].Type == TokenType.Text
               && tokens[pos].Value == " ")
            pos++;

        if (pos >= tokens.Count)
            return [MathRun("")];

        var token = tokens[pos];

        if (token.Type == TokenType.OpenBrace)
        {
            pos++;
            return ParseSequence(tokens, ref pos, untilCloseBrace: true);
        }

        if (token.Type == TokenType.Command)
        {
            pos++;
            return HandleCommand(token.Value, tokens, ref pos);
        }

        pos++;
        return [MathRun(token.Value)];
    }

    #endregion

    #region Command Handling

    private static List<OpenXmlElement> HandleCommand(
        string cmd, List<Token> tokens, ref int pos)
    {
        // Simple symbol replacement
        if (SymbolMap.TryGetValue(cmd, out var symbol))
            return [MathRun(symbol)];

        switch (cmd)
        {
            case @"\frac":
            {
                var num = ParseSingleItem(tokens, ref pos);
                var den = ParseSingleItem(tokens, ref pos);
                var frac = new Fraction();
                var numerator = new Numerator();
                foreach (var e in num) numerator.Append(e);
                var denominator = new Denominator();
                foreach (var e in den) denominator.Append(e);
                frac.Append(numerator);
                frac.Append(denominator);
                return [frac];
            }

            case @"\sqrt":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                var rad = new Radical();
                rad.Append(new Degree());
                var baseEl = new Base();
                foreach (var e in arg) baseEl.Append(e);
                rad.Append(baseEl);
                return [rad];
            }

            case @"\mathbb":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                var text = ExtractText(arg);
                if (DoubleStruckMap.TryGetValue(text, out var ds))
                    return [MathRun(ds)];
                return arg;
            }

            case @"\binom":
            {
                var top = ParseSingleItem(tokens, ref pos);
                var bot = ParseSingleItem(tokens, ref pos);
                // Render as (top / bot) using fraction with parentheses
                var result = new List<OpenXmlElement> { MathRun("(") };
                var frac = new Fraction();
                var fracPr = new FractionProperties(
                    new FractionType { Val = FractionTypeValues.NoBar });
                frac.Append(fracPr);
                var numerator = new Numerator();
                foreach (var e in top) numerator.Append(e);
                var denominator = new Denominator();
                foreach (var e in bot) denominator.Append(e);
                frac.Append(numerator);
                frac.Append(denominator);
                result.Add(frac);
                result.Add(MathRun(")"));
                return result;
            }

            case @"\pmod":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                var result = new List<OpenXmlElement> { MathRun("(mod ") };
                result.AddRange(arg);
                result.Add(MathRun(")"));
                return result;
            }

            case @"\bmod":
                return [MathRun(" mod ")];

            case @"\text" or @"\textbf" or @"\textit" or @"\mathrm"
                 or @"\mathbf" or @"\mathit" or @"\mathcal":
            {
                return ParseSingleItem(tokens, ref pos);
            }

            case @"\operatorname":
            {
                return ParseSingleItem(tokens, ref pos);
            }

            case @"\tilde" or @"\dot" or @"\ddot" or @"\acute"
                 or @"\grave" or @"\breve" or @"\check":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                var accentChar = SymbolMap.TryGetValue(cmd, out var ac) ? ac : "\u0303";
                var accent = new Accent();
                var accPr = new AccentProperties(
                    new AccentChar { Val = accentChar });
                accent.Append(accPr);
                var baseEl = new Base();
                foreach (var e in arg) baseEl.Append(e);
                accent.Append(baseEl);
                return [accent];
            }

            case @"\vec":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                // Use OMML Accent for vector arrow
                var accent = new Accent();
                var accPr = new AccentProperties(
                    new AccentChar { Val = "\u20D7" });
                accent.Append(accPr);
                var baseEl = new Base();
                foreach (var e in arg) baseEl.Append(e);
                accent.Append(baseEl);
                return [accent];
            }

            case @"\hat":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                var accent = new Accent();
                var accPr = new AccentProperties(
                    new AccentChar { Val = "\u0302" });
                accent.Append(accPr);
                var baseEl = new Base();
                foreach (var e in arg) baseEl.Append(e);
                accent.Append(baseEl);
                return [accent];
            }

            case @"\bar" or @"\overline":
            {
                var arg = ParseSingleItem(tokens, ref pos);
                var bar = new Bar();
                var baseEl = new Base();
                foreach (var e in arg) baseEl.Append(e);
                bar.Append(baseEl);
                return [bar];
            }

            case @"\left":
            {
                // Parse delimiter char
                if (pos < tokens.Count)
                {
                    var delimChar = ConsumeDelimiterChar(tokens, ref pos);
                    return [MathRun(delimChar)];
                }
                return [];
            }

            case @"\right":
            {
                if (pos < tokens.Count)
                {
                    var delimChar = ConsumeDelimiterChar(tokens, ref pos);
                    return [MathRun(delimChar)];
                }
                return [];
            }

            // Spacing commands
            case @"\," or @"\;" or @"\:" or @"\!" or @"\quad" or @"\qquad":
                return [MathRun(" ")];

            case @"\\":
                return [MathRun(" ")];

            default:
                // Unknown command: render name without backslash
                return [MathRun(cmd.TrimStart('\\'))];
        }
    }

    private static string ConsumeDelimiterChar(List<Token> tokens, ref int pos)
    {
        var token = tokens[pos];
        pos++;

        if (token.Type == TokenType.Command)
        {
            return token.Value switch
            {
                @"\{" => "{",
                @"\}" => "}",
                @"\|" => "\u2016",
                @"\langle" => "\u27E8",
                @"\rangle" => "\u27E9",
                _ => token.Value.TrimStart('\\')
            };
        }

        return token.Value switch
        {
            "." => "",  // invisible delimiter
            "|" => "|",
            _ => token.Value
        };
    }

    #endregion

    #region Symbol Maps

    private static readonly Dictionary<string, string> SymbolMap = new()
    {
        // Greek lowercase
        [@"\alpha"] = "\u03B1",
        [@"\beta"] = "\u03B2",
        [@"\gamma"] = "\u03B3",
        [@"\delta"] = "\u03B4",
        [@"\epsilon"] = "\u03F5",
        [@"\varepsilon"] = "\u03B5",
        [@"\zeta"] = "\u03B6",
        [@"\eta"] = "\u03B7",
        [@"\theta"] = "\u03B8",
        [@"\vartheta"] = "\u03D1",
        [@"\iota"] = "\u03B9",
        [@"\kappa"] = "\u03BA",
        [@"\lambda"] = "\u03BB",
        [@"\mu"] = "\u03BC",
        [@"\nu"] = "\u03BD",
        [@"\xi"] = "\u03BE",
        [@"\pi"] = "\u03C0",
        [@"\rho"] = "\u03C1",
        [@"\sigma"] = "\u03C3",
        [@"\tau"] = "\u03C4",
        [@"\upsilon"] = "\u03C5",
        [@"\phi"] = "\u03D5",
        [@"\varphi"] = "\u03C6",
        [@"\chi"] = "\u03C7",
        [@"\psi"] = "\u03C8",
        [@"\omega"] = "\u03C9",

        // Greek uppercase
        [@"\Gamma"] = "\u0393",
        [@"\Delta"] = "\u0394",
        [@"\Theta"] = "\u0398",
        [@"\Lambda"] = "\u039B",
        [@"\Xi"] = "\u039E",
        [@"\Pi"] = "\u03A0",
        [@"\Sigma"] = "\u03A3",
        [@"\Phi"] = "\u03A6",
        [@"\Psi"] = "\u03A8",
        [@"\Omega"] = "\u03A9",

        // Binary operators
        [@"\cdot"] = "\u22C5",
        [@"\times"] = "\u00D7",
        [@"\div"] = "\u00F7",
        [@"\pm"] = "\u00B1",
        [@"\mp"] = "\u2213",
        [@"\circ"] = "\u2218",
        [@"\oplus"] = "\u2295",
        [@"\otimes"] = "\u2297",

        // Relations
        [@"\equiv"] = "\u2261",
        [@"\approx"] = "\u2248",
        [@"\neq"] = "\u2260",
        [@"\ne"] = "\u2260",
        [@"\leq"] = "\u2264",
        [@"\le"] = "\u2264",
        [@"\geq"] = "\u2265",
        [@"\ge"] = "\u2265",
        [@"\lt"] = "<",
        [@"\gt"] = ">",
        [@"\ll"] = "\u226A",
        [@"\gg"] = "\u226B",
        [@"\sim"] = "\u223C",
        [@"\simeq"] = "\u2243",
        [@"\propto"] = "\u221D",
        [@"\in"] = "\u2208",
        [@"\notin"] = "\u2209",
        [@"\subset"] = "\u2282",
        [@"\supset"] = "\u2283",
        [@"\subseteq"] = "\u2286",
        [@"\supseteq"] = "\u2287",

        // Arrows
        [@"\rightarrow"] = "\u2192",
        [@"\leftarrow"] = "\u2190",
        [@"\Rightarrow"] = "\u21D2",
        [@"\Leftarrow"] = "\u21D0",
        [@"\leftrightarrow"] = "\u2194",
        [@"\Leftrightarrow"] = "\u21D4",
        [@"\mapsto"] = "\u21A6",
        [@"\to"] = "\u2192",
        [@"\implies"] = "\u21D2",
        [@"\iff"] = "\u21D4",

        // Dots
        [@"\dots"] = "\u2026",
        [@"\ldots"] = "\u2026",
        [@"\cdots"] = "\u22EF",
        [@"\vdots"] = "\u22EE",
        [@"\ddots"] = "\u22F1",

        // Big operators (Unicode characters — sub/superscripts handled by parser)
        [@"\sum"] = "\u2211",
        [@"\prod"] = "\u220F",
        [@"\coprod"] = "\u2210",
        [@"\int"] = "\u222B",
        [@"\iint"] = "\u222C",
        [@"\iiint"] = "\u222D",
        [@"\oint"] = "\u222E",
        [@"\bigcup"] = "\u22C3",
        [@"\bigcap"] = "\u22C2",
        [@"\bigoplus"] = "\u2A01",
        [@"\bigotimes"] = "\u2A02",

        // Misc symbols
        [@"\infty"] = "\u221E",
        [@"\partial"] = "\u2202",
        [@"\nabla"] = "\u2207",
        [@"\forall"] = "\u2200",
        [@"\exists"] = "\u2203",
        [@"\neg"] = "\u00AC",
        [@"\land"] = "\u2227",
        [@"\lor"] = "\u2228",
        [@"\cup"] = "\u222A",
        [@"\cap"] = "\u2229",
        [@"\emptyset"] = "\u2205",
        [@"\varnothing"] = "\u2205",
        [@"\star"] = "\u22C6",
        [@"\ast"] = "*",
        [@"\ell"] = "\u2113",
        [@"\hbar"] = "\u210F",
        [@"\dagger"] = "\u2020",

        // Delimiters
        [@"\langle"] = "\u27E8",
        [@"\rangle"] = "\u27E9",
        [@"\lfloor"] = "\u230A",
        [@"\rfloor"] = "\u230B",
        [@"\lceil"] = "\u2308",
        [@"\rceil"] = "\u2309",
        [@"\|"] = "\u2016",

        // Function operators (rendered as upright text in math)
        [@"\sin"] = "sin",
        [@"\cos"] = "cos",
        [@"\tan"] = "tan",
        [@"\sec"] = "sec",
        [@"\csc"] = "csc",
        [@"\cot"] = "cot",
        [@"\arcsin"] = "arcsin",
        [@"\arccos"] = "arccos",
        [@"\arctan"] = "arctan",
        [@"\sinh"] = "sinh",
        [@"\cosh"] = "cosh",
        [@"\tanh"] = "tanh",
        [@"\log"] = "log",
        [@"\ln"] = "ln",
        [@"\exp"] = "exp",
        [@"\lim"] = "lim",
        [@"\limsup"] = "lim sup",
        [@"\liminf"] = "lim inf",
        [@"\min"] = "min",
        [@"\max"] = "max",
        [@"\sup"] = "sup",
        [@"\inf"] = "inf",
        [@"\det"] = "det",
        [@"\gcd"] = "gcd",
        [@"\dim"] = "dim",
        [@"\ker"] = "ker",
        [@"\hom"] = "hom",
        [@"\arg"] = "arg",
        [@"\deg"] = "deg",
        [@"\Pr"] = "Pr",
        [@"\mod"] = "mod",

        // Accents (additional)
        [@"\tilde"] = "\u0303",
        [@"\dot"] = "\u0307",
        [@"\ddot"] = "\u0308",
        [@"\acute"] = "\u0301",
        [@"\grave"] = "\u0300",
        [@"\breve"] = "\u0306",
        [@"\check"] = "\u030C",

        // Additional misc
        [@"\perp"] = "\u22A5",
        [@"\parallel"] = "\u2225",
        [@"\angle"] = "\u2220",
        [@"\triangle"] = "\u25B3",
        [@"\square"] = "\u25A1",
        [@"\diamond"] = "\u22C4",
        [@"\bullet"] = "\u2022",
        [@"\setminus"] = "\u2216",
        [@"\wp"] = "\u2118",
        [@"\Re"] = "\u211C",
        [@"\Im"] = "\u2111",
        [@"\aleph"] = "\u2135",
        [@"\beth"] = "\u2136",

        // Escaped chars
        [@"\{"] = "{",
        [@"\}"] = "}",
        [@"\%"] = "%",
        [@"\#"] = "#",
        [@"\&"] = "&",
        [@"\_"] = "_",
    };

    private static readonly Dictionary<string, string> DoubleStruckMap = new()
    {
        ["C"] = "\u2102",
        ["H"] = "\u210D",
        ["N"] = "\u2115",
        ["P"] = "\u2119",
        ["Q"] = "\u211A",
        ["R"] = "\u211D",
        ["Z"] = "\u2124",
    };

    #endregion

    #region Helpers

    private static Run MathRun(string text)
    {
        var run = new Run();
        var mathText = new Text { Text = text };
        mathText.SetAttribute(new OpenXmlAttribute(
            "xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"));
        run.Append(mathText);
        return run;
    }

    private static string ExtractText(List<OpenXmlElement> elements)
    {
        var result = new System.Text.StringBuilder();
        foreach (var el in elements)
        {
            if (el is Run run)
            {
                foreach (var child in run.ChildElements)
                {
                    if (child is Text t)
                        result.Append(t.Text);
                }
            }
        }
        return result.ToString();
    }

    #endregion
}
