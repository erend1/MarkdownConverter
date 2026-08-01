namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Half-open <c>[Start, End)</c> range produced by <see cref="FindEngine"/>.
/// Used by the find session to drive the textarea selection and the
/// match-highlight overlay.
/// </summary>
public readonly record struct TextMatch(int Start, int End)
{
    public int Length => End - Start;
}
