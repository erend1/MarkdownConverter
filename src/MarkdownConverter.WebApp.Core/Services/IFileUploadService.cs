namespace MarkdownConverter.WebApp.Core.Services;

public interface IFileUploadService
{
    Task<(string FileName, string Content)> ReadTextFileAsync(Stream fileStream, string fileName);
}
