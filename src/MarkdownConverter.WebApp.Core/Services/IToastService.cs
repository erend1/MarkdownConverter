namespace MarkdownConverter.WebApp.Core.Services;

public interface IToastService
{
    event Action<ToastMessage>? OnShow;
    void ShowSuccess(string message);
    void ShowError(string message, string? details = null);
    void ShowInfo(string message);
}

public sealed class ToastMessage
{
    public required string Text { get; init; }
    public required string Type { get; init; } // "success", "error", "info"

    /// <summary>
    /// Optional long-form detail (stack trace, pdflatex log, etc.). When set,
    /// the toast renders a "Show details" button that opens the message in a
    /// modal so the user can read and copy it without the auto-dismiss timer.
    /// </summary>
    public string? Details { get; init; }
}

public sealed class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message) =>
        OnShow?.Invoke(new ToastMessage { Text = message, Type = "success" });

    public void ShowError(string message, string? details = null) =>
        OnShow?.Invoke(new ToastMessage { Text = message, Type = "error", Details = details });

    public void ShowInfo(string message) =>
        OnShow?.Invoke(new ToastMessage { Text = message, Type = "info" });
}
