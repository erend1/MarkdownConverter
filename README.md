# MarkdownConverter

A .NET 10 application that converts Markdown (`.md`) files to multiple output formats including **Word (.docx)**, **PDF**, and **LaTeX (.tex)**. Features a **Blazor WebAssembly** editor with live preview, dark/light themes, and browser-based export. Built with Clean Architecture principles, fully extensible, and designed to support multiple frontends.

Use the hosted WebApp at <https://erend1.github.io/MarkdownConverter/>.

## Features

- **Multi-format export** — Convert Markdown to Word, PDF, or LaTeX from a single codebase
- **Full math support** — Inline (`$...$`) and display (`$$...$$`) math expressions render natively:
  - **Word**: OMML (Office Math Markup Language) — formulas are editable in Word's equation editor
  - **PDF/LaTeX**: Standard LaTeX math with `amsmath`, `amssymb`, `amsfonts` packages
- **Rich Markdown support** — Headings, bold/italic, inline code, fenced code blocks, ordered/unordered lists, tables, blockquotes, horizontal rules, links, images
- **Citation support** — Pandoc-style `[@key]` citations with BibTeX bibliography files:
  - **LaTeX/PDF**: APA-style `biblatex` with `\cite{}` commands, automatic `biber` compilation
  - **Word**: Native CITATION field codes + Sources.xml bibliography data
  - Auto-discovery of `.bib` sidecar files or explicit `--bibliography` option
- **Enhanced blockquotes** — Styled blockquotes with full nesting, attribution, and GitHub-style callouts:
  - **LaTeX/PDF**: `tcolorbox` environments with grey left bar, colored callout boxes, FontAwesome icons
  - **Word**: Left-bordered paragraphs with F5F5F5 background, colored callout tables, progressive nesting
  - **5 callout types**: `[!NOTE]`, `[!TIP]`, `[!WARNING]`, `[!IMPORTANT]`, `[!CAUTION]`
  - **Attribution**: `— Author` lines rendered right-aligned italic
- **Code block styling** — Language-aware syntax highlighting (LaTeX `listings` package), bordered/shaded boxes (Word)
- **Unicode safety** — Common emoji and symbols mapped to LaTeX equivalents; unsupported chars gracefully dropped
- **Clean Architecture** — Core domain has zero external dependencies; all NuGet packages are isolated in the Infrastructure layer
- **Extensible** — Adding a new export format requires only 3 changes (enum member, converter class, DI registration line)
- **Web App** — Blazor WebAssembly PWA with split-pane Markdown editor + live HTML preview:
  - Real-time rendering via Markdig HTML output + KaTeX math (JS interop)
  - **Multi-tab editing** — Up to 10 tabs with independent file state, dirty indicators, hover-revealed rename pencil, drag-to-reorder
  - **Find & replace** — `Ctrl+F` / `Ctrl+H` with case-sensitive / whole-word / regex / find-in-selection toggles, `current / total` counter, all matches highlighted (VS-Code parity); current match in a brighter shade
  - **Keyboard shortcuts** — `Ctrl+B` bold, `Ctrl+I` italic, `Ctrl+K` link, `` Ctrl+` `` code, `Ctrl+S` save, `Ctrl+N` new tab, `Ctrl+W` close tab
  - **Drag-and-drop** — Drop `.md` files to open in new tab, `.bib` files to load bibliography
  - **Save dialog** — Save / export uses the File System Access API so the user picks the destination (Chromium / WebView2); legacy auto-download fallback for Firefox / Safari with an info toast
  - **Stats bar** — Live word count, character count, line count per tab
  - **Auto-save** — Debounced session persistence to `localStorage`; corrupted state is backed up rather than silently lost
  - **Error UX** — Per-type toast lifetime (success 3 s, info 5 s, error 10 s), close button, "Show details" modal for long `pdflatex` logs with copy-to-clipboard
  - Word (.docx) and LaTeX (.tex) export as browser downloads
  - Bibliography (.bib) upload, dark/light theme toggle
  - Installable offline PWA with service worker caching
- **Desktop app** — Windows `.exe` that wraps the Web UI in a WebView2 host, with extra Desktop-only features:
  - **Compile PDF** — local `pdflatex` invoked via a `POST /api/preview-pdf` endpoint, PDF streamed back to the WebView2
  - **Open `.md` from Explorer** — `MarkdownConverter.exe path\to\file.md`; once set as the default opener for `.md`, double-click opens the file as a new tab
  - **Explorer preview pane support** — optional per-user script registers `.md` / `.markdown` with Windows' built-in text previewer
  - **Stable session storage** — fixed loopback port + `%LOCALAPPDATA%` user-data folder so `localStorage` survives restarts and Disk Cleanup
- **Testable** — All external dependencies abstracted behind interfaces; **400+ unit tests** with Moq. The find / replace algorithms and shortcut handler are pure C# (`FindEngine`, `FindSession`, `EditorShortcutHandler`) — no browser or `IJSRuntime` needed to run them.

## Solution Structure

```
MarkdownConverter/
├── Directory.Build.props                          # Shared build config (net10.0, nullable)
│
├── src/
│   ├── MarkdownConverter.Core/                    # Domain layer — ZERO dependencies
│   │   ├── Enums/ExportFormat.cs                  # Word, Pdf, Latex
│   │   ├── Interfaces/                            # All abstractions
│   │   │   ├── IConversionService.cs              # High-level orchestration
│   │   │   ├── IConverterFactory.cs               # Resolves converter by format
│   │   │   ├── IFormatConverter.cs                # Strategy — one per format
│   │   │   ├── IMarkdownParser.cs                 # Markdown parsing seam
│   │   │   ├── IFileSystem.cs                     # File I/O seam
│   │   │   └── IProcessRunner.cs                  # External process seam
│   │   ├── Models/                                # DTOs and value objects
│   │   │   ├── MarkdownDocument.cs
│   │   │   ├── ConversionRequest.cs
│   │   │   ├── ConversionResult.cs
│   │   │   ├── ProcessRunResult.cs
│   │   │   ├── BibEntry.cs                      # Bibliography entry model
│   │   │   ├── CitationInfo.cs                  # Citation reference model
│   │   │   └── CalloutType.cs                   # Callout type enum (Note, Warning, etc.)
│   │   └── Services/
│   │       └── ConversionService.cs               # Orchestrator (only concrete class)
│   │
│   ├── MarkdownConverter.Infrastructure/          # Implementations + NuGet packages
│   │   ├── Converters/
│   │   │   ├── WordFormatConverter.cs              # Markdown → Word via OpenXml
│   │   │   ├── PdfLatexFormatConverter.cs          # Markdown → LaTeX → pdflatex → PDF
│   │   │   ├── LatexFormatConverter.cs             # Markdown → LaTeX file
│   │   │   ├── MarkdownLatexRenderer.cs            # Shared Markdown-to-LaTeX renderer
│   │   │   └── LatexMathConverter.cs               # LaTeX math → OMML (Word formulas)
│   │   ├── Bibliography/
│   │   │   ├── BibTexParser.cs                    # BibTeX file parser
│   │   │   └── BibEntryToOpenXmlConverter.cs      # BibEntry → Word Sources.xml
│   │   ├── MarkdigExtensions/
│   │   │   ├── CitationInline.cs                  # Custom AST node for [@key]
│   │   │   ├── CitationParser.cs                  # Markdig inline parser
│   │   │   ├── CitationExtension.cs               # Markdig pipeline extension
│   │   │   └── AttributionDetector.cs             # Detects "— Author" in blockquotes
│   │   ├── Factories/ConverterFactory.cs           # .NET keyed service resolution
│   │   ├── FileSystem/PhysicalFileSystem.cs
│   │   ├── Parsing/MarkdigMarkdownParser.cs
│   │   ├── Process/SystemProcessRunner.cs
│   │   └── Registration/
│   │       └── ServiceCollectionExtensions.cs      # Single AddMarkdownConverter() method
│   │
│   ├── MarkdownConverter.CLI/              # CLI frontend
│   │   └── Program.cs
│   │
│   ├── MarkdownConverter.WebApp.Core/             # PVM layer — pure C#, zero Blazor deps
│   │   ├── Views/                                 # View interfaces (IEditorView, etc.)
│   │   ├── ViewModels/                            # UI state (EditorViewModel, etc.)
│   │   ├── Presenters/                            # Testable business logic (incl. FindPresenter)
│   │   ├── Services/                              # FindEngine, FindSession, EditorShortcutHandler,
│   │   │                                          # IEditorBridge, IToastService, …
│   │   └── Models/ExportOption.cs                 # Export format display model
│   │
│   ├── MarkdownConverter.WebApp/                  # Blazor WebAssembly PWA
│   │   ├── Components/                            # Razor components (Editor, Preview, Toolbar, etc.)
│   │   ├── Layout/MainLayout.razor                # App shell with toolbar + theme
│   │   ├── Pages/EditorPage.razor                 # Split-pane editor + preview
│   │   ├── Services/                              # BrowserFileSystem, EditorBridge, …
│   │   ├── Interop/KaTeXInterop.cs                # JS interop for math rendering
│   │   └── wwwroot/                               # Static assets, CSS, PWA manifest
│   │       └── js/
│   │           ├── dom-bridge.js                  # DOM primitives + event forwarding (logic-free)
│   │           ├── dom-events.js                  # Local-only high-frequency handlers
│   │           ├── file-interop.js                # Browser-only APIs (save dialog, clipboard, …)
│   │           └── katex-interop.js               # KaTeX math rendering
│   │
│   └── MarkdownConverter.Desktop/                 # Windows desktop app (WebView2)
│       ├── Program.cs                             # WinForms + local HTTP server + WebView2
│       ├── PortStore.cs                           # Stable loopback port across launches
│       └── StartupArgs.cs                         # CLI .md path → "Open with" support
│
└── tests/
    ├── MarkdownConverter.Core.Tests/              # Domain logic tests
    ├── MarkdownConverter.Infrastructure.Tests/    # Converter, factory, renderer tests
    ├── MarkdownConverter.WebApp.Core.Tests/       # Presenter + service unit tests
    └── MarkdownConverter.Desktop.Tests/           # Desktop helper tests (links source files)
```

### Dependency Graph

```
Core ──> (nothing)                    Core has ZERO project/NuGet references
Infrastructure ──> Core
CLI ──> Core + Infrastructure
WebApp.Core ──> Core                  PVM layer — zero Blazor dependencies
WebApp ──> WebApp.Core + Core + Infrastructure
Desktop ──> Core + Infrastructure     Hosts WebApp WASM output via WebView2
                                      + local pdflatex / file-open endpoints
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **For PDF export**: A LaTeX distribution with `pdflatex` on your PATH
  - Windows: [MiKTeX](https://miktex.org/) or [TeX Live](https://tug.org/texlive/)
  - macOS: `brew install --cask mactex`
  - Linux: `sudo apt install texlive-full`
- **For PDF with citations**: `biber` (included with MiKTeX and TeX Live)
- **For PDF with callouts**: `tcolorbox` and `fontawesome5` LaTeX packages (included with MiKTeX and TeX Live)

## Installing

Desktop builds are published from version tags on this repository's GitHub Releases page. Open **Releases**, choose the latest release or prerelease channel, and download `MarkdownConverter-<version>-win-x64.zip`.

Extract the downloaded zip and run `MarkdownConverter.exe`.

The Desktop app requires the Microsoft Edge WebView2 Runtime. Most Windows 10/11 machines already have it; if not, install the Evergreen Runtime from:

```text
https://developer.microsoft.com/microsoft-edge/webview2/
```

To verify the downloaded zip:

```powershell
Get-FileHash .\MarkdownConverter-<version>-win-x64.zip -Algorithm SHA256
Get-Content .\MarkdownConverter-<version>-win-x64.zip.sha256
```

### Release Channels

| Channel | Tag pattern | Release type | Intended audience |
|---------|-------------|--------------|-------------------|
| Alpha | `v0.1.0-alpha.1` | Prerelease | Early testers validating rough builds |
| Beta | `v0.1.0-beta.1` | Prerelease | Testers validating feature-complete builds |
| Preview / RC | `v0.1.0-rc.1` | Prerelease | Final validation before stable |
| Stable | `v0.1.0` | Release | General users |

## Getting Started

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Usage

### Console Application

The console app provides two commands: `convert` and `formats`.

#### List supported formats

```bash
dotnet run --project src/MarkdownConverter.CLI -- formats
```

Output:
```
Supported formats:
  - Word
  - Pdf
  - Latex
```

#### Convert a file

```bash
dotnet run --project src/MarkdownConverter.CLI -- convert --input <file.md> [--output <file>] [--format <format>] [--bibliography <file.bib>]
```

**Options:**

| Option | Short | Required | Default | Description |
|--------|-------|----------|---------|-------------|
| `--input` | `-i` | Yes | — | Path to the input Markdown file |
| `--output` | `-o` | No | Auto-generated | Path to the output file |
| `--format` | `-f` | No | `pdf` | Export format: `pdf`, `word`, or `latex` |
| `--bibliography` | `-b` | No | Auto-discovered | Path to a `.bib` bibliography file |

If `--output` is omitted, the output file is placed alongside the input with the appropriate extension (`.pdf`, `.docx`, or `.latex`).

If `--bibliography` is omitted, the converter automatically looks for a `.bib` file with the same name as the input (e.g., `paper.md` → `paper.bib`).

**Examples:**

```bash
# Convert to PDF (default)
dotnet run --project src/MarkdownConverter.CLI -- convert -i docs/notes.md

# Convert to Word
dotnet run --project src/MarkdownConverter.CLI -- convert -i docs/notes.md -f word

# Convert to LaTeX with custom output path
dotnet run --project src/MarkdownConverter.CLI -- convert -i docs/notes.md -o output/notes.tex -f latex

# Convert with explicit bibliography file
dotnet run --project src/MarkdownConverter.CLI -- convert -i paper.md -b refs.bib -f pdf

# Convert to Word with custom output path
dotnet run --project src/MarkdownConverter.CLI -- convert -i docs/notes.md -o output/notes.docx -f word
```

### Using as a Library

Any .NET application can use the converter by referencing the Core and Infrastructure projects and calling `AddMarkdownConverter()`:

```csharp
using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Infrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

// Setup DI
var services = new ServiceCollection();
services.AddMarkdownConverter();
var provider = services.BuildServiceProvider();

// Convert
var service = provider.GetRequiredService<IConversionService>();
var markdown = File.ReadAllText("input.md");
var result = await service.ConvertAsync(markdown, ExportFormat.Word, "output.docx");

if (result.Success)
    Console.WriteLine($"Done! {result.BytesWritten} bytes written.");
else
    Console.WriteLine($"Error: {result.ErrorMessage}");
```

### Desktop App (Windows)

The Desktop app wraps the same Blazor WebAssembly UI inside a native Windows window using WebView2 (Edge). No browser needed — runs as a standalone `.exe`. The Desktop host adds three things on top of the shared Web UI:

- **Compile PDF** — a `POST /api/preview-pdf` endpoint runs `pdflatex` on the active tab and streams the resulting PDF back to the WebView2 (the in-browser WebApp greys this out).
- **Open `.md` from Explorer** — the `.exe` accepts a `.md` / `.markdown` / `.txt` path as its first argument. Right-click any `.md` → *Open with* → *Choose another app* → browse to `MarkdownConverter.exe` → check **Always use this app**, and double-click opens the file as a new tab.
- **Explorer preview pane support** — an optional per-user script registers `.md` and `.markdown` with Windows' built-in text preview handler so the Explorer preview pane can show the raw Markdown content without opening the app.
- **Stable session storage** — the loopback port is persisted under `%LOCALAPPDATA%\MarkdownConverter\WebView2\port.txt` (with a graceful fallback if it's busy) so the WebView2 origin stays the same across launches and `localStorage` survives restarts. WebView2 user data lives in the same folder rather than `%TEMP%`.

#### Run the Desktop App

```bash
# Build the WebApp first (Desktop copies its output)
dotnet build src/MarkdownConverter.WebApp
dotnet run --project src/MarkdownConverter.Desktop
```

The app starts a lightweight local HTTP server on the stored / picked port and opens the Blazor UI in a WebView2 window. All features from the Web App work identically.

To open a file directly on launch:

```bash
dotnet run --project src/MarkdownConverter.Desktop -- C:\path\to\notes.md
```

#### Enable Explorer Preview Pane

Windows Explorer preview integration is provided through Shell preview handlers. A fully rendered Markdown preview would require a separate COM `IPreviewHandler` shell extension, which is intentionally out of scope for the current Desktop app. The supported lightweight option is raw Markdown preview through Windows' built-in text previewer.

Enable it for the current user:

```powershell
.\scripts\windows\Register-MarkdownPreview.ps1
```

Disable it:

```powershell
.\scripts\windows\Register-MarkdownPreview.ps1 -Unregister
```

If Explorer does not update immediately, restart File Explorer or sign out and back in. Files downloaded from the internet may still be blocked from preview by Windows security policy until you unblock the file in Properties.

#### Publish Locally

```bash
dotnet publish src/MarkdownConverter.WebApp -c Release
dotnet publish src/MarkdownConverter.Desktop -c Release -r win-x64 --self-contained /p:PublishSingleFile=true
```

For distribution, prefer the tag-driven GitHub release workflow. It publishes the WebApp first, publishes the Desktop app as a self-contained `win-x64` build, zips the full publish directory, computes SHA256, and attaches both files to the release.

#### Releasing

Maintainer release steps are documented in [.github/RELEASING.md](.github/RELEASING.md). In short:

1. Add a changelog entry for the target version.
2. Commit and push to `main`.
3. Push a `v*` tag such as `v0.1.0-alpha.1`.
4. Let `.github/workflows/release.yml` create the GitHub Release.

### Web App (Blazor WebAssembly)

The Web App is a standalone Blazor WebAssembly PWA with a split-pane Markdown editor and live preview. It runs entirely in the browser — no server required.

#### Run the Web App

```bash
dotnet run --project src/MarkdownConverter.WebApp
```

Then open `https://localhost:5001` (or the URL shown in the console).

#### Features

- **Split-pane editor** — Write Markdown on the left, see live HTML preview on the right
- **Real-time preview** — Debounced (300ms) Markdig HTML rendering with KaTeX math via JS interop
- **Multi-tab editing** — Up to 10 tabs with per-tab file state, dirty indicators, close (`x`), switch, **hover-revealed rename pencil**, drag-to-reorder
- **Keyboard shortcuts** — `Ctrl+B` bold, `Ctrl+I` italic, `Ctrl+K` link, `` Ctrl+` `` code, `Ctrl+S` save to session, `Ctrl+Shift+S` save as, `Ctrl+N` new tab, `Ctrl+W` close tab, `Ctrl+F` find, `Ctrl+H` replace
- **Find & replace** — Whole-word and regex toggles, `current / total` counter, visible match highlight, Enter / Shift+Enter for next / prev, Escape to close
- **Drag-and-drop** — Drop `.md` files to open in new tab, `.bib` files to load bibliography
- **Save dialog** — Save / export uses the File System Access API so the user picks the destination on every save (Chromium / WebView2); falls back to legacy `<a download>` on Firefox / Safari with an info toast explaining why
- **Stats bar** — Live word count, character count, line count per tab
- **Auto-save** — Session persisted to `localStorage` on every keystroke (debounced). Corrupted state is backed up to `mdconverter_tabs.bak` rather than silently lost.
- **Error UX** — Per-type toast lifetime (success 3 s, info 5 s, error 10 s), close button on every toast, "Show details" modal for long `pdflatex` logs with copy-to-clipboard
- **Export** — Word (.docx) and LaTeX (.tex) export through the save dialog
- **Print to PDF** — Browser print dialog on styled preview window with KaTeX CSS
- **Compile PDF (Desktop only)** — Local `pdflatex` invoked via a `POST /api/preview-pdf` endpoint
- **Bibliography** — Upload a `.bib` file that attaches to exports
- **Dark/light theme** — Toggle persisted to `localStorage`
- **PWA** — Installable as a desktop app, works offline via service worker

#### Architecture (PVM)

The Web App uses the **Presenter-View-Model** pattern with Dependency Injection:

| Layer | Project | Depends On | Testable? |
|-------|---------|------------|-----------|
| **View** | `WebApp` (Razor components) | WebApp.Core | Browser coverage is required for browser-owned behavior |
| **Presenter** | `WebApp.Core` (pure C#) | Core only | Yes — Moq |
| **Model** | `Core` (ViewModels + domain) | Nothing | Yes |

Presenters hold ViewModels, receive user gestures from Views, orchestrate services, and call `View.RequestRender()`. Views are thin Razor components that implement a plain C# interface (e.g., `IEditorView`).

#### WASM Adaptations

| Interface | CLI Implementation | WASM Implementation |
|-----------|-------------------|---------------------|
| `IFileSystem` | `PhysicalFileSystem` | `BrowserFileSystem` (in-memory `ConcurrentDictionary`) |
| `IProcessRunner` | `SystemProcessRunner` | `NullProcessRunner` (returns error — no process exec in browser) |
| Format converters | Word + LaTeX + PDF | Word + LaTeX only (PDF not registered) |

## Architecture

### Design Patterns

| Pattern | Where | Purpose |
|---------|-------|---------|
| **Strategy** | `IFormatConverter` | One implementation per export format |
| **Factory** | `IConverterFactory` | Resolves the correct converter by `ExportFormat` enum |
| **Dependency Inversion** | `IFileSystem`, `IProcessRunner`, `IMarkdownParser` | Isolate external dependencies for testability |
| **Keyed Services** | `ServiceCollectionExtensions` | .NET keyed DI for strategy resolution |
| **Presenter-View-Model** | `WebApp.Core` Presenters | Testable UI logic decoupled from Blazor |

### Export Format Details

#### Word (.docx)

- Uses **DocumentFormat.OpenXml** (no Microsoft Office installation required)
- Heading styles (H1–H5) with proper font sizes
- Bold, italic, and inline code with Consolas font + grey background
- Code blocks in bordered, shaded boxes (single-cell table pattern)
- Math expressions rendered as native **OMML** (Office Math Markup Language) — fully editable in Word's equation editor
- Citations as native CITATION field codes with Sources.xml bibliography
- Tables with borders, bold headers
- **Blockquotes** with grey left border, F5F5F5 background, italic text, progressive nesting
- **Callouts** (`[!NOTE]`, `[!TIP]`, etc.) as colored table cells with icon + title
- **Attribution** (`— Author`) right-aligned italic in blockquotes
- Horizontal rules as bottom borders

#### PDF

- Pipeline: Markdown → LaTeX → `pdflatex` → PDF
- Runs `pdflatex` twice for cross-reference resolution (4 passes with `biber` when citations are present)
- Uses **lmodern** vector fonts (no pixelated text)
- Full LaTeX math support via `amsmath`
- APA-style citations via `biblatex` + `biber`
- Code blocks with `listings` package (background, frames, line numbers, language-aware)
- **Blockquotes** via `tcolorbox` `mdquote` environment with grey left bar, italic text
- **Callouts** via colored `tcolorbox` environments with FontAwesome icons (`\faInfoCircle`, `\faExclamationTriangle`, etc.)
- **Attribution** rendered as `\hfill\textit{--- Author}` right-aligned
- Temporary build directory with automatic cleanup

#### LaTeX (.tex)

- Outputs a standalone, compilable `.tex` document
- Same renderer used by the PDF pipeline
- Inline math preserved as `$...$`, display math as `\[...\]`
- Special characters properly escaped (`&`, `%`, `#`, `_`, `{`, `}`, `~`, `^`)
- Unicode emoji/symbols mapped to LaTeX equivalents (unsupported chars gracefully dropped)

### Math Rendering

The converter handles LaTeX math expressions end-to-end:

| Feature | LaTeX/PDF Output | Word Output |
|---------|-----------------|-------------|
| Inline math `$x^2$` | `$x^2$` | OMML Superscript |
| Display math `$$\sum_{i=1}^{n}$$` | `\[\sum_{i=1}^{n}\]` | OMML SubSuperscript |
| Fractions `\frac{a}{b}` | LaTeX native | OMML Fraction |
| Greek letters `\alpha`, `\Sigma` | LaTeX native | Unicode (α, Σ) |
| Square roots `\sqrt{x}` | LaTeX native | OMML Radical |
| Vectors `\vec{v}` | LaTeX native | OMML Accent |
| Bars `\bar{x}`, `\overline{AB}` | LaTeX native | OMML Bar |
| Blackboard bold `\mathbb{R}` | LaTeX native | Unicode (ℝ) |
| Operators `\sum`, `\prod`, `\int` | LaTeX native | Unicode (∑, ∏, ∫) |

### Citations

The converter supports Pandoc-style citations with BibTeX bibliography files:

#### Markdown Syntax

```markdown
Single citation: [@smith2023]
Multiple citations: [@smith2023; @jones2024]
With page locator: [@smith2023, p. 42]
```

#### Bibliography File (`.bib`)

Place a BibTeX file alongside your Markdown file with the same name (e.g., `paper.md` + `paper.bib`), or specify it explicitly with `--bibliography`.

```bibtex
@article{smith2023,
    author = {Smith, John and Doe, Jane},
    title = {A Study of Markdown Conversion},
    journal = {Journal of Document Engineering},
    year = {2023},
    volume = {15},
    pages = {42--58}
}
```

#### Output by Format

| Feature | LaTeX/PDF | Word |
|---------|-----------|------|
| In-text citation | `\cite{key}` (APA style via `biblatex`) | CITATION field code |
| Multiple keys | `\cite{key1,key2}` | Multiple CITATION fields |
| Page locator | `\cite[p.~42]{key}` | CITATION field with locator |
| Bibliography list | `\printbibliography` (auto-generated) | BIBLIOGRAPHY field + Sources.xml |
| Compilation | `pdflatex → biber → pdflatex → pdflatex` | Word "Update Fields" (Ctrl+A, F9) |

### Blockquotes

The converter provides enhanced blockquote rendering beyond standard Markdown:

#### Basic Blockquote

```markdown
> This is a styled blockquote with italic text,
> a grey left border, and a light background.
```

#### Nested Blockquotes

```markdown
> Outer quote
> > Nested quote (deeper indentation, darker border)
> > > Triple nesting supported
```

#### Attribution

```markdown
> Be the change you wish to see in the world.
>
> --- Mahatma Gandhi
```

The `--- Author` or `— Author` line in the last paragraph is detected and rendered right-aligned in italic.

#### GitHub-Style Callouts

```markdown
> [!NOTE]
> Useful information the user should know.

> [!WARNING]
> Potential issues that need attention.
```

| Callout | LaTeX/PDF | Word |
|---------|-----------|------|
| `[!NOTE]` | Blue `tcolorbox` with `\faInfoCircle` | Blue-bordered table, `DBE9FE` background |
| `[!TIP]` | Green `tcolorbox` with `\faCheck` | Green-bordered table, `D4EDDA` background |
| `[!IMPORTANT]` | Violet `tcolorbox` with `\faExclamation` | Violet-bordered table, `E8D5FF` background |
| `[!WARNING]` | Yellow `tcolorbox` with `\faExclamationTriangle` | Yellow-bordered table, `FFF4CC` background |
| `[!CAUTION]` | Red `tcolorbox` with `\faBan` | Red-bordered table, `FDDEDE` background |

## Extending: Adding a New Export Format

Adding a new format (e.g., HTML) requires **3 additive-only changes** — no existing code is modified:

### Step 1: Add enum member

```csharp
// src/MarkdownConverter.Core/Enums/ExportFormat.cs
public enum ExportFormat
{
    Word,
    Pdf,
    Latex,
    Html   // ← new
}
```

### Step 2: Create converter class

```csharp
// src/MarkdownConverter.Infrastructure/Converters/HtmlFormatConverter.cs
public sealed class HtmlFormatConverter : IFormatConverter
{
    public ExportFormat Format => ExportFormat.Html;

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request, CancellationToken ct = default)
    {
        // Your conversion logic here
    }
}
```

### Step 3: Register in DI

```csharp
// In ServiceCollectionExtensions.AddMarkdownConverter()
services.AddKeyedSingleton<IFormatConverter, HtmlFormatConverter>(ExportFormat.Html);
```

The factory, service, CLI, and all future UIs automatically pick up the new format.

## NuGet Dependencies

| Project | Packages |
|---------|----------|
| **Core** | *(none)* |
| **Infrastructure** | Markdig 0.38.0, DocumentFormat.OpenXml 3.2.0, Microsoft.Extensions.DependencyInjection.Abstractions 10.0.10 |
| **CLI** | Microsoft.Extensions.DependencyInjection 10.0.10, Microsoft.Extensions.Hosting 10.0.10 |
| **WebApp.Core** | *(Core only)* |
| **WebApp** | Microsoft.AspNetCore.Components.WebAssembly 10.0.10, Microsoft.AspNetCore.Components.WebAssembly.DevServer 10.0.10 |
| **Desktop** | Microsoft.Web.WebView2 1.0.2739.15, Microsoft.Extensions.DependencyInjection 10.0.10 |
| **Tests** | xUnit, Moq, coverlet.collector |

## License

MarkdownConverter is licensed under the [Apache License 2.0](LICENSE). Third-party components remain subject to the licenses reproduced in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before proposing a change. Automated contributors must also follow [AGENTS.md](AGENTS.md).

Maintainers assigning Planner and Worker agents should use the [Human Leader Runbook](docs/development/human-leader-runbook.md).
