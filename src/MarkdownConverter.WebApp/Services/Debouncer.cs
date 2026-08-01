using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Services;

public sealed class Debouncer : IDebouncer, IDisposable
{
    private Timer? _timer;

    public void Debounce(int millisecondsDelay, Func<Task> action)
    {
        _timer?.Dispose();
        _timer = new Timer(async _ =>
        {
            try
            {
                await action();
            }
            catch
            {
                // Swallow errors in debounced actions
            }
        }, null, millisecondsDelay, Timeout.Infinite);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
