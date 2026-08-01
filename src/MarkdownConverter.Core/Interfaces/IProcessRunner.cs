using MarkdownConverter.Core.Models;

namespace MarkdownConverter.Core.Interfaces;

/// <summary>
/// Abstracts external process invocation (e.g., pdflatex, xelatex).
/// Easily mocked in tests.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}
