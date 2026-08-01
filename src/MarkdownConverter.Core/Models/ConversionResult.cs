namespace MarkdownConverter.Core.Models;

public sealed class ConversionResult
{
    public required bool Success { get; init; }
    public required string OutputFilePath { get; init; }
    public string? ErrorMessage { get; init; }
    public long BytesWritten { get; init; }

    public static ConversionResult Ok(string outputPath, long bytes) =>
        new() { Success = true, OutputFilePath = outputPath, BytesWritten = bytes };

    public static ConversionResult Fail(string outputPath, string error) =>
        new() { Success = false, OutputFilePath = outputPath, ErrorMessage = error };
}
