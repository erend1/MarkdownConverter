namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Expected, user-correctable failures produced by a find operation.
/// Infrastructure failures continue to propagate to the UI error handler.
/// </summary>
public enum FindFailure
{
    None,
    InvalidPattern,
    TimedOut
}
