namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Result of <see cref="ILocalStorageService.SaveTabsAsync"/>.
/// </summary>
public sealed class SaveResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static SaveResult Ok() => new() { Success = true };

    public static SaveResult Error(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
