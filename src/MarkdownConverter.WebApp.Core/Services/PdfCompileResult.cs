namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Typed result of <c>fileInterop.compilePdf</c>. Replaces the prior
/// "bool on success, string on failure" contract — System.Text.Json could
/// not distinguish the two when deserialising into <c>object</c>, so a
/// successful compile was misreported as an error.
/// </summary>
public sealed class PdfCompileResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}
