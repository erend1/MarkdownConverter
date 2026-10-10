using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Models;
using MarkdownConverter.Infrastructure.Converters;
using MarkdownConverter.Infrastructure.FileSystem;
using MarkdownConverter.Infrastructure.Process;

namespace MarkdownConverter.Infrastructure.Tests;

/// <summary>
/// Compiles each document once and shares the result between the tests of the class.
/// Set MDCONVERT_LATEX_ARTIFACTS to a directory to keep the .tex, .log and .pdf files.
/// </summary>
public sealed class LatexCompilationFixture : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<CompiledLatex>>> _documents = new();
    private readonly SemaphoreSlim _compilers = new(4);
    private readonly bool _keepFiles;

    public LatexCompilationFixture()
    {
        var artifacts = Environment.GetEnvironmentVariable(LatexToolchain.ArtifactsVariable);
        _keepFiles = !string.IsNullOrWhiteSpace(artifacts);
        Directory = _keepFiles
            ? Path.GetFullPath(artifacts!)
            : Path.Combine(Path.GetTempPath(), $"mdconv-latex-{Guid.NewGuid():N}");

        // Start the fixtures side by side; each test then only waits for its own document.
        if (LatexToolchain.Find(LatexToolchain.PdfLatex) is not null)
        {
            foreach (var fixture in LatexTableFixtures.Names)
            {
                _ = CompileFixtureAsync(fixture);
            }
        }
    }

    internal string Directory { get; }

    internal Task<CompiledLatex> CompileFixtureAsync(string fixture) =>
        CompileAsync(fixture, MarkdownLatexRenderer.Render(LatexTableFixtures.Read(fixture)));

    internal Task<CompiledLatex> CompileAsync(string name, string tex) =>
        _documents.GetOrAdd(name, _ => new Lazy<Task<CompiledLatex>>(() => CompileLimitedAsync(name, tex))).Value;

    private async Task<CompiledLatex> CompileLimitedAsync(string name, string tex)
    {
        await _compilers.WaitAsync();
        try
        {
            return await LatexToolchain.CompileAsync(Path.Combine(Directory, name), name, tex);
        }
        finally
        {
            _compilers.Release();
        }
    }

    public void Dispose()
    {
        if (_keepFiles) return;

        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary build directory.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Compiles the table fixtures with the local pdflatex and checks what TeX and the PDF say
/// about their width. A zero exit code proves nothing about layout, so the tests read the
/// overfull-box diagnostics and measure where text and ink end on every page.
/// </summary>
public partial class LatexTableCompilationTests : IClassFixture<LatexCompilationFixture>
{
    // \geometry{margin=1in}
    private const double MarginPt = 72;
    private const double TextTolerancePt = 0.5;
    // Two pixels of the 150dpi raster.
    private const double InkTolerancePt = 1;

    private const string IssueUrl =
        "https://example.org/very/long/unbreakable/path/with/identifiers/and/detailed/names";

    private readonly LatexCompilationFixture _latex;

    public LatexTableCompilationTests(LatexCompilationFixture latex) => _latex = latex;

    public static TheoryData<string> Fixtures => new(LatexTableFixtures.Names);

    [LatexTheory(LatexToolchain.PdfLatex)]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_CompilesWithoutOverfullBoxes(string fixture)
    {
        var document = await _latex.CompileFixtureAsync(fixture);

        AssertCompiled(document);
        Assert.True(
            document.OverfullBoxes.Count == 0,
            $"{fixture}: " + string.Join("; ", document.OverfullBoxes.Select(box => Invariant($"{box.Points}pt {box.Place}"))));
        Assert.DoesNotContain(@"Overfull \vbox", document.Log);
        Assert.DoesNotContain("Table widths have changed", document.Log);
    }

    [LatexTheory(LatexToolchain.PdfLatex, LatexToolchain.PdfToText)]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_TextStaysInsideThePageMargins(string fixture)
    {
        var document = await _latex.CompileFixtureAsync(fixture);
        AssertCompiled(document);

        var pages = await LatexToolchain.ReadTextExtentsAsync(document.PdfPath);

        Assert.NotEmpty(pages);
        AssertInsideMargins(fixture, pages, TextTolerancePt);
    }

    [LatexTheory(LatexToolchain.PdfLatex, LatexToolchain.PdfToPpm)]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_InkStaysInsideThePageMargins(string fixture)
    {
        var document = await _latex.CompileFixtureAsync(fixture);
        AssertCompiled(document);

        var pages = await LatexToolchain.ReadInkExtentsAsync(document.PdfPath);

        Assert.NotEmpty(pages);
        AssertInsideMargins(fixture, pages, InkTolerancePt);
        // The rules span the text block, so a table that fits ends exactly at the right margin.
        Assert.All(pages, page => Assert.InRange(page.Right!.Value, page.Width - MarginPt - 3, page.Width - MarginPt + InkTolerancePt));
    }

    [LatexFact(LatexToolchain.PdfLatex, LatexToolchain.PdfToText)]
    public async Task MultipageFixture_ContinuesOnTheFollowingPages()
    {
        var document = await _latex.CompileFixtureAsync("multipage-4col");
        AssertCompiled(document);

        var pages = (await LatexToolchain.ReadTextAsync(document.PdfPath))
            .Split('\f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.True(pages.Length >= 3, $"Expected the table to span at least three pages, found {pages.Length}.");
        Assert.Contains("001", pages[0]);
        Assert.Contains("072", pages[^1]);

        // Every row is present exactly once and in source order.
        var text = string.Join("\n", pages);
        int previous = -1;
        for (int row = 1; row <= 72; row++)
        {
            var label = row.ToString("D3", CultureInfo.InvariantCulture);
            int position = text.IndexOf(label, StringComparison.Ordinal);
            Assert.True(position > previous, $"Row {label} is missing or out of order.");
            Assert.Equal(position, text.LastIndexOf(label, StringComparison.Ordinal));
            previous = position;
        }
    }

    [LatexFact(LatexToolchain.PdfLatex, LatexToolchain.PdfToText)]
    public async Task UnbreakableFixture_KeepsEveryCharacterOfItsLongTokens()
    {
        var document = await _latex.CompileFixtureAsync("unbreakable-content");
        AssertCompiled(document);

        var text = WhitespacePattern().Replace(await LatexToolchain.ReadTextAsync(document.PdfPath), "");

        Assert.Contains(IssueUrl + "?query=value&other=another-long-value#fragment", text);
        Assert.Contains("https://docs.example.org/reference/api/v2/conversion/latex/tables", text);
        Assert.Contains("MarkdownConverter.Infrastructure.Converters.MarkdownLatexRenderer.RenderTable", text);
        Assert.Contains("very_long_configuration_option_name_with_many_separate_segments_and_no_spaces", text);
        Assert.Contains("src/MarkdownConverter.Infrastructure/Converters/MarkdownLatexRenderer.cs", text);
        Assert.Equal(2, Regex.Matches(text, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855").Count);
    }

    [LatexFact(LatexToolchain.PdfLatex, LatexToolchain.PdfToPpm)]
    public async Task LegacyColumnSpecification_OverflowsTheLine()
    {
        // The table this renderer emitted before the width budget existed. It keeps the
        // measurements honest: the checks above must be able to see a table that overflows.
        var legacy = string.Join("\n",
            @"\begin{longtable}{>{\raggedleft\arraybackslash}p{0.33\textwidth} | >{\raggedright\arraybackslash}p{0.33\textwidth} | >{\raggedleft\arraybackslash}p{0.33\textwidth}}",
            @"\hline",
            @"\textbf{ID} & \textbf{Description} & \textbf{Result} \\",
            @"\hline",
            @"12 & This description contains a lengthy explanation with many words that should wrap inside the available column width. & 42 \\",
            @"\hline",
            $@"13 & \href{{{IssueUrl}}}{{{IssueUrl}}} & 7 \\",
            @"\hline",
            @"\end{longtable}");

        var document = await _latex.CompileAsync("legacy-column-specification", DocumentWith(legacy));

        Assert.Equal(0, document.ExitCode);
        // 3 x 0.33\textwidth + 6\tabcolsep + 2\arrayrulewidth is 32.27pt more than \textwidth.
        var table = Assert.Single(document.OverfullBoxes, box => box.Place.StartsWith("in alignment", StringComparison.Ordinal));
        Assert.InRange(table.Points, 32.2, 32.4);
        Assert.Contains(document.OverfullBoxes, box => box.Place.StartsWith("in paragraph", StringComparison.Ordinal) && box.Points > 200);

        var page = Assert.Single(await LatexToolchain.ReadInkExtentsAsync(document.PdfPath));
        Assert.True(page.Right > page.Width - MarginPt + 30, Invariant($"Ink ends at {page.Right}pt on a {page.Width}pt page."));
    }

    [LatexFact(LatexToolchain.PdfLatex, LatexToolchain.PdfToPpm)]
    public async Task TablesInsideQuotesListsAndCallouts_StayInsideThePage()
    {
        const string row = "| Alpha | A reasonably long description that needs to wrap inside the table without leaving its container. | 12 |";
        var markdown = string.Join("\n",
            "> A quote before the table.",
            ">",
            "> | Name | Description | Count |",
            "> |------|-------------|------:|",
            "> " + row,
            "",
            "- A list item with a table:",
            "",
            "  | Name | Description | Count |",
            "  |------|-------------|------:|",
            "  " + row,
            "",
            "> [!NOTE]",
            "> A callout with a table.",
            ">",
            "> | Name | Description | Count |",
            "> |------|-------------|------:|",
            "> " + row,
            "");

        var tex = MarkdownLatexRenderer.Render(markdown);
        Assert.Equal(3, LatexTableSource.ParseAll(tex).Count);

        var document = await _latex.CompileAsync("nested-tables", tex);

        AssertCompiled(document);
        Assert.Empty(document.OverfullBoxes);
        AssertInsideMargins("nested-tables", await LatexToolchain.ReadInkExtentsAsync(document.PdfPath), InkTolerancePt);
    }

    [LatexFact(LatexToolchain.PdfLatex)]
    public async Task WidthEstimates_CoverTheTypesetWidth()
    {
        string[] samples =
        [
            "Infrastructure", "Documentation", "Responsibility", "Description", "Median duration",
            "1,234.50", "2026-10-11", "WWW MMM", "illicit filling",
            "MarkdownConverter.Infrastructure", "IFormatConverter", "e3b0c44298fc1c149afbf4c8996fb924",
            "Kraftfahrzeughaftpflichtversicherung", "The quick brown fox jumps over the lazy dog"
        ];

        // One table per sample: a bold header cell, a body cell and an inline-code cell.
        var cells = samples
            .Select(sample => LatexTableSource.Single(
                MarkdownLatexRenderer.Render($"| {sample} |\n|---|\n| {sample} |\n| `{sample}` |")))
            .ToList();
        var body = new StringBuilder().AppendLine(@"\newlength{\samplewidth}");
        for (int i = 0; i < samples.Length; i++)
        {
            for (int font = 0; font < 3; font++)
            {
                body.AppendLine($@"\settowidth{{\samplewidth}}{{{cells[i].Rows[font][0]}}}\typeout{{SAMPLE {i} {font} \the\samplewidth}}");
            }
        }

        var document = await _latex.CompileAsync("width-estimates", DocumentWith(body.ToString()));
        Assert.Equal(0, document.ExitCode);

        var measured = SampleWidthPattern().Matches(document.Log);
        Assert.Equal(samples.Length * 3, measured.Count);
        foreach (Match match in measured)
        {
            var sample = samples[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)];
            int font = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            double typesetEm = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) / 10.95;
            double estimatedEm = sample.Sum(ch => ch == ' '
                ? LatexTableLayout.SpaceWidthEm(bold: font == 0, monospace: font == 2)
                : LatexTableLayout.CharWidthEm(ch, bold: font == 0, monospace: font == 2));

            // The planner's safety margin is meant for narrower lines, not for metric errors:
            // the estimate itself must never be the smaller of the two, nor wildly larger.
            Assert.True(
                typesetEm <= estimatedEm + 0.01 && estimatedEm <= typesetEm * 1.15,
                Invariant($"'{sample}' (font {font}) is {typesetEm:F3}em typeset but estimated at {estimatedEm:F3}em."));
        }
    }

    [LatexFact(LatexToolchain.PdfLatex, LatexToolchain.PdfToPpm)]
    public async Task PdfConverter_WritesAPdfThatStaysInsideTheMargins()
    {
        var output = Path.Combine(_latex.Directory, "pdf-converter", "wide-6col.pdf");
        var converter = new PdfLatexFormatConverter(new PhysicalFileSystem(), new SystemProcessRunner());

        var result = await converter.ConvertAsync(new ConversionRequest
        {
            Document = new MarkdownDocument { RawMarkdown = LatexTableFixtures.Read("wide-6col"), ParsedAst = new object() },
            TargetFormat = ExportFormat.Pdf,
            OutputFilePath = output
        });

        Assert.True(result.Success, result.ErrorMessage);
        AssertInsideMargins("pdf-converter", await LatexToolchain.ReadInkExtentsAsync(output), InkTolerancePt);
    }

    private static void AssertCompiled(CompiledLatex document)
    {
        Assert.True(document.ExitCode == 0, $"pdflatex exited with {document.ExitCode} for {document.Name}:\n{document.LogTail}");
        Assert.True(File.Exists(document.PdfPath), $"pdflatex produced no PDF for {document.Name}:\n{document.LogTail}");
    }

    private static void AssertInsideMargins(string name, IReadOnlyList<PdfPageExtent> pages, double tolerancePt)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            Assert.True(page.Right is not null, $"{name}: page {i + 1} is empty.");
            Assert.True(
                page.Left >= MarginPt - tolerancePt,
                Invariant($"{name}: page {i + 1} starts at {page.Left:F1}pt, left of the {MarginPt}pt margin."));
            Assert.True(
                page.Right <= page.Width - MarginPt + tolerancePt,
                Invariant($"{name}: page {i + 1} ends at {page.Right:F1}pt, {page.Right - (page.Width - MarginPt):F1}pt past the right margin") +
                (page.RightmostWord is null ? "." : $" ('{page.RightmostWord}')."));
        }
    }

    private static string Invariant(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    // The renderer's own preamble around hand-written TeX.
    private static string DocumentWith(string body)
    {
        var empty = MarkdownLatexRenderer.Render("");
        var preamble = empty[..empty.IndexOf(@"\begin{document}", StringComparison.Ordinal)];
        return preamble + "\\begin{document}\n\n" + body + "\n\n\\end{document}\n";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"SAMPLE (\d+) (\d) ([0-9.]+)pt")]
    private static partial Regex SampleWidthPattern();
}
