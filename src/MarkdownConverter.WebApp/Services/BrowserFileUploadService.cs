using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Services;

public sealed class BrowserFileUploadService : IFileUploadService
{
    public async Task<(string FileName, string Content)> ReadTextFileAsync(Stream fileStream, string fileName)
    {
        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync();
        return (fileName, content);
    }
}
