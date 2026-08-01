namespace MarkdownConverter.Core.Models;

public sealed class ProcessRunResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public bool Succeeded => ExitCode == 0;
}
