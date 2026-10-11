# Table that spans several pages

Seventy-two rows of wrapped text, enough to cross at least two page breaks.

| Case | Area | Scenario | Expected outcome |
|-----:|------|----------|------------------|
| 001 | Parser | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 002 | Word export | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 003 | LaTeX export | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 004 | PDF export | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 005 | Preview | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 006 | Desktop shell | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 007 | Citations | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 008 | Parser | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 009 | Word export | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 010 | LaTeX export | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 011 | PDF export | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 012 | Preview | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 013 | Desktop shell | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 014 | Citations | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 015 | Parser | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 016 | Word export | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 017 | LaTeX export | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 018 | PDF export | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 019 | Preview | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 020 | Desktop shell | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 021 | Citations | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 022 | Parser | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 023 | Word export | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 024 | LaTeX export | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 025 | PDF export | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 026 | Preview | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 027 | Desktop shell | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 028 | Citations | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 029 | Parser | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 030 | Word export | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 031 | LaTeX export | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 032 | PDF export | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 033 | Preview | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 034 | Desktop shell | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 035 | Citations | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 036 | Parser | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 037 | Word export | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 038 | LaTeX export | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 039 | PDF export | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 040 | Preview | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 041 | Desktop shell | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 042 | Citations | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 043 | Parser | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 044 | Word export | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 045 | LaTeX export | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 046 | PDF export | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 047 | Preview | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 048 | Desktop shell | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 049 | Citations | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 050 | Parser | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 051 | Word export | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 052 | LaTeX export | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 053 | PDF export | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 054 | Preview | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 055 | Desktop shell | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 056 | Citations | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 057 | Parser | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 058 | Word export | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 059 | LaTeX export | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 060 | PDF export | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 061 | Preview | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 062 | Desktop shell | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 063 | Citations | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 064 | Parser | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
| 065 | Word export | Convert a document that contains only headings and short paragraphs and compare the exported structure with the source outline. | The export completes and the output matches the source. |
| 066 | LaTeX export | Convert a document with a table that is longer than one page and confirm that the rows continue on the following page. | The export completes without an overfull box in the compiler log. |
| 067 | PDF export | Convert a document that mixes inline mathematics, inline code and emphasised text inside the same table cell. | The export completes and the characters are rendered with the expected glyphs. |
| 068 | Preview | Convert a document whose table contains a very long address and confirm that the address wraps inside its own column. | The export completes; the table stays inside the page margins on every page. |
| 069 | Desktop shell | Convert a document that cites several sources and confirm that every citation resolves against the bibliography file. | The export completes after the bibliography pass and lists every cited source. |
| 070 | Citations | Convert a document with nested block quotes and callouts and confirm that the nesting depth is preserved in the output. | The export completes and reports the number of bytes written. |
| 071 | Parser | Convert a document that contains non-ASCII letters, typographic quotes and symbols and confirm that nothing is dropped. | The export completes and every inline element keeps its formatting. |
| 072 | Word export | Convert an empty document and confirm that the export still produces a valid file without any warnings. | The export completes and the visual hierarchy matches the preview. |
