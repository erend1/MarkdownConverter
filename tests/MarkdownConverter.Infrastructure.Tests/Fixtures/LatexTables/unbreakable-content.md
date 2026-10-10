# Content without natural break points

URLs, identifiers, inline code, hashes and inline math inside table cells.

| Kind | Example | Notes |
|------|---------|-------|
| URL | https://example.org/very/long/unbreakable/path/with/identifiers/and/detailed/names?query=value&other=another-long-value#fragment | A bare address that is longer than the whole table. |
| Link | [https://docs.example.org/reference/api/v2/conversion/latex/tables](https://docs.example.org/reference/api/v2/conversion/latex/tables) | A link whose visible text is the address itself. |
| Identifier | MarkdownConverter.Infrastructure.Converters.MarkdownLatexRenderer.RenderTable | A fully qualified member name. |
| Snake case | very_long_configuration_option_name_with_many_separate_segments_and_no_spaces | Underscores are escaped in the generated source. |
| Inline code | `src/MarkdownConverter.Infrastructure/Converters/MarkdownLatexRenderer.cs` | Typewriter text is never hyphenated. |
| Hash | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | Sixty-four characters with no separator at all. |
| Code hash | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | The same value as inline code. |
| Math | $\hat{\beta} = (X^{\top}X)^{-1}X^{\top}y + \lambda \sum_{i=1}^{n} \lvert w_i \rvert$ | Inline math is emitted unchanged. |
| Long word | Donaudampfschifffahrtsgesellschaftskapitän | A natural-language word that should be hyphenated. |
