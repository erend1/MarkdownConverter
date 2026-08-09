namespace MarkdownConverter.WebApp.Core.Services;

public interface IDesktopCapabilityProvider
{
    Task<DesktopCapabilities> GetCapabilitiesAsync();
}

/// <summary>
/// Reads the outer-host capability declaration at most once for the application lifetime.
/// </summary>
public sealed class DesktopCapabilityProvider : IDesktopCapabilityProvider
{
    private readonly Lazy<Task<DesktopCapabilities>> _capabilities;

    public DesktopCapabilityProvider(
        Func<ValueTask<DesktopCapabilities?>> readCapabilities,
        Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(readCapabilities);
        ArgumentNullException.ThrowIfNull(reportFailure);

        _capabilities = new Lazy<Task<DesktopCapabilities>>(
            () => ReadOnceAsync(readCapabilities, reportFailure),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<DesktopCapabilities> GetCapabilitiesAsync() => _capabilities.Value;

    private static async Task<DesktopCapabilities> ReadOnceAsync(
        Func<ValueTask<DesktopCapabilities?>> readCapabilities,
        Action<Exception> reportFailure)
    {
        try
        {
            return await readCapabilities() ?? DesktopCapabilities.Standalone;
        }
        catch (Exception exception)
        {
            try
            {
                reportFailure(exception);
            }
            catch
            {
                // A diagnostic sink must not turn optional Desktop capability
                // detection into an application-startup failure.
            }

            return DesktopCapabilities.Standalone;
        }
    }
}
