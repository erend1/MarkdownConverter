using System.Collections.Concurrent;
using System.Text;
using MarkdownConverter.Core.Interfaces;

namespace MarkdownConverter.WebApp.Services;

public sealed class BrowserFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public Stream CreateFileStream(string path, FileMode mode)
    {
        return new CallbackMemoryStream(bytes => _files[path] = bytes);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        return _files.TryGetValue(path, out var data)
            ? Task.FromResult(data)
            : throw new FileNotFoundException($"Virtual file not found: {path}", path);
    }

    public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        _files[path] = data;
        return Task.CompletedTask;
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_files.TryGetValue(path, out var data))
            return Task.FromResult(Encoding.UTF8.GetString(data));
        throw new FileNotFoundException($"Virtual file not found: {path}", path);
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        _files[path] = Encoding.UTF8.GetBytes(content);
        return Task.CompletedTask;
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public long GetFileSize(string path)
    {
        return _files.TryGetValue(path, out var data) ? data.Length : 0;
    }

    /// <summary>
    /// A MemoryStream that calls a callback with its bytes when disposed.
    /// This captures data written by OpenXml SDK via using/dispose pattern.
    /// </summary>
    private sealed class CallbackMemoryStream : MemoryStream
    {
        private readonly Action<byte[]> _onClose;
        private bool _captured;

        public CallbackMemoryStream(Action<byte[]> onClose)
        {
            _onClose = onClose;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_captured)
            {
                _captured = true;
                _onClose(ToArray());
            }
            base.Dispose(disposing);
        }
    }
}
