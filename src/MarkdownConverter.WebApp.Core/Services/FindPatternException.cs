namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Thrown by <see cref="FindEngine"/> when the user-supplied regex fails
/// to compile. Callers translate this into the "Invalid regex" find-bar
/// state without surfacing a generic exception to the UI.
/// </summary>
public sealed class FindPatternException : Exception
{
    public FindPatternException(string message) : base(message) { }
    public FindPatternException(string message, Exception inner) : base(message, inner) { }
}
