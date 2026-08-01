namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Thrown when a regular-expression search exceeds the configured execution
/// limit. Callers translate this into a stable find-bar status.
/// </summary>
public sealed class FindTimeoutException : Exception
{
    public FindTimeoutException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
