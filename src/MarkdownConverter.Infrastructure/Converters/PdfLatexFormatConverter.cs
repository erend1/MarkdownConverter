using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Infrastructure.Converters;

public sealed class PdfLatexFormatConverter : IFormatConverter
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    public PdfLatexFormatConverter(IFileSystem fileSystem, IProcessRunner processRunner)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
    }

    public ExportFormat Format => ExportFormat.Pdf;

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mdconv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            request.Options.TryGetValue("bibliography", out var bibPath);
            var hasBib = !string.IsNullOrEmpty(bibPath) && _fileSystem.FileExists(bibPath!);
            var bibResourceName = hasBib ? "references.bib" : null;

            var latexContent = MarkdownLatexRenderer.Render(request.Document.RawMarkdown, bibResourceName);
            var texFilePath = Path.Combine(tempDir, "output.tex");
            await _fileSystem.WriteAllTextAsync(texFilePath, latexContent, cancellationToken);

            // Copy .bib file into temp directory
            if (hasBib)
            {
                var bibDest = Path.Combine(tempDir, "references.bib");
                var bibContent = await _fileSystem.ReadAllBytesAsync(bibPath!, cancellationToken);
                await _fileSystem.WriteAllBytesAsync(bibDest, bibContent, cancellationToken);
            }

            if (hasBib)
            {
                // pdflatex → biber → pdflatex → pdflatex
                await RunPdfLatex(tempDir, texFilePath, cancellationToken);
                await RunBiber(tempDir, cancellationToken);
                await RunPdfLatex(tempDir, texFilePath, cancellationToken);

                var finalResult = await RunPdfLatex(tempDir, texFilePath, cancellationToken);
                if (!finalResult.Succeeded)
                {
                    return ConversionResult.Fail(
                        request.OutputFilePath,
                        $"pdflatex failed (exit code {finalResult.ExitCode}):\n{finalResult.StandardError}\n{finalResult.StandardOutput}");
                }
            }
            else
            {
                // Run pdflatex twice for cross-references
                for (int i = 0; i < 2; i++)
                {
                    var result = await RunPdfLatex(tempDir, texFilePath, cancellationToken);

                    if (!result.Succeeded && i == 1)
                    {
                        return ConversionResult.Fail(
                            request.OutputFilePath,
                            $"pdflatex failed (exit code {result.ExitCode}):\n{result.StandardError}\n{result.StandardOutput}");
                    }
                }
            }

            var pdfPath = Path.Combine(tempDir, "output.pdf");
            if (!_fileSystem.FileExists(pdfPath))
            {
                return ConversionResult.Fail(
                    request.OutputFilePath,
                    "pdflatex completed but no PDF file was produced.");
            }

            var outputDir = Path.GetDirectoryName(request.OutputFilePath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            var pdfBytes = await _fileSystem.ReadAllBytesAsync(pdfPath, cancellationToken);
            await _fileSystem.WriteAllBytesAsync(request.OutputFilePath, pdfBytes, cancellationToken);

            return ConversionResult.Ok(request.OutputFilePath, pdfBytes.Length);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* best effort cleanup */ }
        }
    }

    private async Task<ProcessRunResult> RunPdfLatex(
        string tempDir, string texFilePath, CancellationToken cancellationToken)
    {
        return await _processRunner.RunAsync(
            "pdflatex",
            $"-interaction=nonstopmode -output-directory=\"{tempDir}\" \"{texFilePath}\"",
            tempDir,
            cancellationToken);
    }

    private async Task<ProcessRunResult> RunBiber(
        string tempDir, CancellationToken cancellationToken)
    {
        return await _processRunner.RunAsync(
            "biber",
            "output",
            tempDir,
            cancellationToken);
    }
}
