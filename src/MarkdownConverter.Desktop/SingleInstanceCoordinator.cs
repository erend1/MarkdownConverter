using System.IO.Pipes;
using System.Text.Json;

namespace MarkdownConverter.Desktop;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string DefaultMutexName = "Global\\MarkdownConverter.Desktop.SingleInstance";
    private const string DefaultPipeName = "MarkdownConverter.Desktop.SingleInstance";
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private readonly string _pipeName;
    private bool _disposed;

    public SingleInstanceCoordinator(
        string mutexName = DefaultMutexName,
        string pipeName = DefaultPipeName)
    {
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        _ownsMutex = createdNew;
        _pipeName = pipeName;
    }

    public bool IsPrimary => _ownsMutex;

    public async Task StartListeningAsync(
        Func<LaunchRequest, Task> handler,
        CancellationToken cancellationToken)
    {
        if (!IsPrimary) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken);
                var request = await ReadRequestAsync(server, cancellationToken);
                if (request is not null)
                    await handler(request);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Keep the primary listener alive if a secondary exits mid-write
                // or sends malformed data.
            }
        }
    }

    public async Task SendToPrimaryAsync(
        LaunchRequest request,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + SendTimeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);

                await client.ConnectAsync(500, cancellationToken);
                await JsonSerializer.SerializeAsync(client, request, cancellationToken: cancellationToken);
                await client.FlushAsync(cancellationToken);
                return;
            }
            catch (TimeoutException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    private static async Task<LaunchRequest?> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<LaunchRequest>(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsMutex)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }

        _mutex.Dispose();
    }
}
