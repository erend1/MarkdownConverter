using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Infrastructure.Converters;

public sealed class LatexFormatConverter : IFormatConverter
{
    private readonly IFileSystem _fileSystem;

    public LatexFormatConverter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public ExportFormat Format => ExportFormat.Latex;

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Options.TryGetValue("bibliography", out var bibPath);
        var hasBib = !string.IsNullOrEmpty(bibPath) && _fileSystem.FileExists(bibPath!);
        var bibResourceName = hasBib ? "references.bib" : null;

        var latexContent = MarkdownLatexRenderer.Render(request.Document.RawMarkdown, bibResourceName);

        var outputDir = Path.GetDirectoryName(request.OutputFilePath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await _fileSystem.WriteAllTextAsync(request.OutputFilePath, latexContent, cancellationToken);

        // Copy .bib file next to the .tex output
        if (hasBib)
        {
            var bibDest = Path.Combine(outputDir ?? ".", "references.bib");
            var bibContent = await _fileSystem.ReadAllBytesAsync(bibPath!, cancellationToken);
            await _fileSystem.WriteAllBytesAsync(bibDest, bibContent, cancellationToken);
        }

        var size = _fileSystem.GetFileSize(request.OutputFilePath);
        return ConversionResult.Ok(request.OutputFilePath, size);
    }
}
