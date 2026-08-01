using MarkdownConverter.Core.Interfaces;

namespace MarkdownConverter.Infrastructure.FileSystem;

public sealed class PhysicalFileSystem : IFileSystem
{
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllBytesAsync(path, cancellationToken);

    public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        => File.WriteAllBytesAsync(path, data, cancellationToken);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, content, cancellationToken);

    public bool FileExists(string path)
        => File.Exists(path);

    public long GetFileSize(string path)
        => new FileInfo(path).Length;

    public Stream CreateFileStream(string path, FileMode mode)
        => new FileStream(path, mode);
}
