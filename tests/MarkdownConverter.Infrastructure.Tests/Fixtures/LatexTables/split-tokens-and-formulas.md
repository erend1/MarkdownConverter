# Tokens the parser splits, and formulas

Escapes, brackets, emphasis and code spans cut one unbroken run of text into several inline nodes.

| Kind | Example | Notes |
|------|---------|-------|
| Escaped underscores | very\_long\_configuration\_option\_name\_with\_many\_separate\_segments | Each escape starts a new literal. |
| Indexers | response.data.items[0].attributes.relationships[12].links.related.href | Brackets are link syntax to the parser. |
| Escaped backslashes | C:\\Users\\name\\AppData\\Local\\Temp\\mdconv\\output\\report.tex | A Windows path written with escapes. |
| Code spans | `--configuration`=`Release`;`--verbosity`=`minimal`;`--no-restore`;`--nologo` | Several code spans glued together. |
| Escaped pipes | first\|second\|third\|fourth\|fifth\|sixth\|seventh\|eighth\|ninth | The only way to write a pipe in a cell. |
| Emphasis | **Markdown**Converter.*Infrastructure*.Converters.`MarkdownLatexRenderer`.RenderTable | Bold, italic and code inside one name. |
| Dropped letter | temperature_coefficient_of_resistance_α measured at room temperature | The Greek letter has no glyph and is left out; the space after it stays. |
| Ellipsis | `f(…)` and more… | An ellipsis is wider than the other symbols. |
| Signs | $(-0.12, -0.34, -0.56, -0.78, -0.91, -1.23)$ | A formula does not break after a sign. |
| Matrix | $\begin{pmatrix} a_{11}+b_{11} & a_{12}+b_{12} \\ a_{21}+b_{21} & a_{22}+b_{22} \end{pmatrix}$ | Nor inside an environment. |
