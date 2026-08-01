namespace MarkdownConverter.Core.Interfaces;

/// <summary>
/// Abstracts file-system operations so converters can be tested
/// without touching the real file system.
/// </summary>
public interface IFileSystem
{
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
    bool FileExists(string path);
    long GetFileSize(string path);
    Stream CreateFileStream(string path, FileMode mode);
}
