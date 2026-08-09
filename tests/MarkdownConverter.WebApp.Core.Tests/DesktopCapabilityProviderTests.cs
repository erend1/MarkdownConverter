using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class DesktopCapabilityProviderTests
{
    [Fact]
    public async Task MissingMarker_ResolvesToStandaloneCapabilities()
    {
        var readCount = 0;
        var failures = new List<Exception>();
        var provider = new DesktopCapabilityProvider(
            () =>
            {
                readCount++;
                return ValueTask.FromResult<DesktopCapabilities?>(null);
            },
            failures.Add);

        var capabilities = await provider.GetCapabilitiesAsync();

        Assert.Same(DesktopCapabilities.Standalone, capabilities);
        Assert.False(capabilities.CanCompilePdf);
        Assert.False(capabilities.CanReceivePendingFiles);
        Assert.Equal(1, readCount);
        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ExplicitCapabilities_AreResolvedIndependently(
        bool canCompilePdf,
        bool canReceivePendingFiles)
    {
        var declared = new DesktopCapabilities(canCompilePdf, canReceivePendingFiles);
        var provider = new DesktopCapabilityProvider(
            () => ValueTask.FromResult<DesktopCapabilities?>(declared),
            _ => { });

        var capabilities = await provider.GetCapabilitiesAsync();

        Assert.Equal(canCompilePdf, capabilities.CanCompilePdf);
        Assert.Equal(canReceivePendingFiles, capabilities.CanReceivePendingFiles);
    }

    [Fact]
    public async Task RepeatedConcurrentConsumers_ReadCapabilitiesOnce()
    {
        var readCount = 0;
        var declared = new DesktopCapabilities(true, true);
        var releaseRead = new TaskCompletionSource<DesktopCapabilities?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new DesktopCapabilityProvider(
            () =>
            {
                Interlocked.Increment(ref readCount);
                return new ValueTask<DesktopCapabilities?>(releaseRead.Task);
            },
            _ => { });

        var reads = Enumerable.Range(0, 20)
            .Select(_ => provider.GetCapabilitiesAsync())
            .ToArray();

        Assert.Equal(1, Volatile.Read(ref readCount));
        releaseRead.SetResult(declared);

        var results = await Task.WhenAll(reads);
        Assert.All(results, result => Assert.Same(declared, result));
        Assert.Equal(1, readCount);
    }

    [Fact]
    public async Task AdapterFailure_DisablesCapabilitiesAndReportsOnce()
    {
        var expected = new InvalidOperationException("Interop failed.");
        var readCount = 0;
        var failures = new List<Exception>();
        var provider = new DesktopCapabilityProvider(
            () =>
            {
                readCount++;
                return ValueTask.FromException<DesktopCapabilities?>(expected);
            },
            failures.Add);

        var results = await Task.WhenAll(
            provider.GetCapabilitiesAsync(),
            provider.GetCapabilitiesAsync(),
            provider.GetCapabilitiesAsync());

        Assert.All(results, result => Assert.Same(DesktopCapabilities.Standalone, result));
        Assert.Equal(1, readCount);
        Assert.Same(expected, Assert.Single(failures));
    }
}
