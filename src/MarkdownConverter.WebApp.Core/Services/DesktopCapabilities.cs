namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Immutable capabilities explicitly supplied by the Desktop host.
/// </summary>
public sealed record DesktopCapabilities(
    bool CanCompilePdf,
    bool CanReceivePendingFiles)
{
    public static DesktopCapabilities Standalone { get; } = new(false, false);
}
