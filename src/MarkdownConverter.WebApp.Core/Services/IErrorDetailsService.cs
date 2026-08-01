namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Carries an open-modal request from the toast container to the
/// <c>ErrorDetailsDialog</c> rendered by <c>MainLayout</c>.
/// </summary>
public interface IErrorDetailsService
{
    event Action<ErrorDetails>? OnShow;
    void Show(string title, string details);
}

public sealed class ErrorDetails
{
    public required string Title { get; init; }
    public required string Details { get; init; }
}

public sealed class ErrorDetailsService : IErrorDetailsService
{
    public event Action<ErrorDetails>? OnShow;

    public void Show(string title, string details) =>
        OnShow?.Invoke(new ErrorDetails { Title = title, Details = details });
}
