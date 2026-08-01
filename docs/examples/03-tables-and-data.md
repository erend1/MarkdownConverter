# Tables and Structured Data

MarkdownConverter renders tables with full border support and bold headers across all output formats.

## Project Architecture

| Layer | Project | Dependencies | Purpose |
|-------|---------|-------------|---------|
| Domain | MarkdownConverter.Core | None | Interfaces, models, enums |
| Infrastructure | MarkdownConverter.Infrastructure | Core, Markdig, OpenXml | Converters, parsers, file I/O |
| Presentation | MarkdownConverter.ConsoleApp | Core, Infrastructure | CLI frontend |
| Tests | *.Tests | Core, Infrastructure, xUnit | Unit test suites |

## Supported Export Formats

| Format | Extension | Engine | Math Support |
|--------|-----------|--------|-------------|
| Word | .docx | DocumentFormat.OpenXml | OMML (native formulas) |
| PDF | .pdf | pdflatex (MiKTeX / TeX Live) | LaTeX math |
| LaTeX | .tex | Direct file output | LaTeX math |

## Design Patterns Used

| Pattern | Interface | Implementation |
|---------|-----------|---------------|
| Strategy | `IFormatConverter` | One class per format |
| Factory | `IConverterFactory` | Keyed service resolution |
| Dependency Inversion | `IFileSystem` | `PhysicalFileSystem` |
| Dependency Inversion | `IProcessRunner` | `SystemProcessRunner` |
| Dependency Inversion | `IMarkdownParser` | `MarkdigMarkdownParser` |

## Performance Benchmarks

Results from converting a 500-line Markdown document with 20 math equations:

| Format | Time | Output Size |
|--------|------|-------------|
| LaTeX | ~50ms | 12 KB |
| Word | ~120ms | 45 KB |
| PDF | ~3.5s | 180 KB |

> **Note:** PDF conversion is slower because it invokes `pdflatex` twice (for cross-reference resolution).

## CLI Options Reference

| Option | Short | Required | Default | Description |
|--------|-------|----------|---------|-------------|
| `--input` | `-i` | Yes | — | Input Markdown file path |
| `--output` | `-o` | No | Auto | Output file path |
| `--format` | `-f` | No | `pdf` | Target format: `pdf`, `word`, `latex` |

### Example Commands

```bash
# List available formats
dotnet run --project src/MarkdownConverter.ConsoleApp -- formats

# Convert to PDF (default)
dotnet run --project src/MarkdownConverter.ConsoleApp -- convert -i document.md

# Convert to Word with custom output
dotnet run --project src/MarkdownConverter.ConsoleApp -- convert -i document.md -o report.docx -f word
```

---

*This example demonstrates table rendering and structured content.*
