using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;

namespace MarkdownConverter.WebApp.Services;

public sealed class NullProcessRunner : IProcessRunner
{
    public Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProcessRunResult
        {
            ExitCode = 1,
            StandardOutput = string.Empty,
            StandardError = "Process execution is not available in the browser. " +
                          "Use the CLI tool for PDF export: dotnet run -- convert --format pdf"
        });
    }
}
