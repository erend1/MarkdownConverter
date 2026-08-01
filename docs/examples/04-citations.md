# Academic Document with Citations

## Introduction

The field of document engineering has evolved rapidly over the past two decades. Lightweight markup languages such as Markdown [@markdown2004] have become the de facto standard for writing technical documentation, blog posts, and even academic papers.

The art of digital typesetting was pioneered by Donald Knuth with the creation of TeX [@knuth1984]. His work laid the foundation for modern document rendering pipelines, including the LaTeX system widely used in academia today.

## Background

Recent research has demonstrated significant progress in automated format conversion. [@smith2023] proposed a multi-stage pipeline that transforms Markdown to both PDF and Word formats while preserving mathematical notation and structural semantics.

Building on that foundation, [@jones2024] introduced interoperability techniques that enable lossless round-tripping between document formats — a long-standing challenge in the document engineering community.

## Methodology

Our approach combines several key ideas from the literature:

1. **Parsing**: We use the Markdig library to build an abstract syntax tree (AST) from the Markdown source, following the CommonMark specification [@markdown2004].
2. **Citation resolution**: Pandoc-style citations (`[@key]`) are parsed via a custom Markdig extension and resolved against a BibTeX bibliography file.
3. **Rendering**: The AST is walked by format-specific renderers that emit LaTeX `\cite{}` commands [@knuth1984] or Word CITATION field codes.

According to [@smith2023, p. 45], the most critical step in the pipeline is the mapping of inline elements to their target format equivalents.

## Results

Our converter successfully handles all common citation patterns:

- Single citations: [@knuth1984]
- Multiple citations in one bracket: [@smith2023; @jones2024]
- Citations with page locators: [@smith2023, p. 42]
- Citations mixed with regular text and **bold** or *italic* formatting

## Conclusion

This work demonstrates that a clean, extensible architecture can support proper academic citation handling across multiple output formats. The combination of BibTeX parsing, custom Markdig extensions, and format-specific rendering produces professional results in both LaTeX/PDF and Word outputs [@smith2023; @jones2024; @knuth1984; @markdown2004].
