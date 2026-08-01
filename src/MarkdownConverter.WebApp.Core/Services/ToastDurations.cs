namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Per-type auto-dismiss durations for toasts. Errors deliberately stay
/// longer than success / info so the user has time to read them.
/// </summary>
public static class ToastDurations
{
    public static readonly TimeSpan Success = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Info = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Error = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(3);

    public static TimeSpan AutoDismissFor(string type) => type switch
    {
        "success" => Success,
        "info" => Info,
        "error" => Error,
        _ => Default
    };
}
