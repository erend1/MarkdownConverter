using MarkdownConverter.Desktop;

namespace MarkdownConverter.Desktop.Tests;

public class SingleInstanceCoordinatorTests
{
    [Fact]
    public void SecondCoordinator_WithSameMutex_IsSecondary()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var primary = new SingleInstanceCoordinator(
            $"MarkdownConverter.Tests.{suffix}",
            $"MarkdownConverter.Tests.{suffix}");
        using var secondary = new SingleInstanceCoordinator(
            $"MarkdownConverter.Tests.{suffix}",
            $"MarkdownConverter.Tests.{suffix}");

        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);
    }

    [Fact]
    public async Task SendToPrimaryAsync_ForwardsLaunchRequest()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var primary = new SingleInstanceCoordinator(
            $"MarkdownConverter.Tests.{suffix}",
            $"MarkdownConverter.Tests.{suffix}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new TaskCompletionSource<LaunchRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var listener = primary.StartListeningAsync(
            request =>
            {
                received.TrySetResult(request);
                return Task.CompletedTask;
            },
            cts.Token);

        using var secondary = new SingleInstanceCoordinator(
            $"MarkdownConverter.Tests.{suffix}",
            $"MarkdownConverter.Tests.{suffix}");

        var expectedPath = @"C:\Kullanıcılar\Hüseyin\Çalışma Notları\ikinci ölçüm.md";
        await secondary.SendToPrimaryAsync(
            new LaunchRequest
            {
                Kind = LaunchRequestKinds.OpenFile,
                FilePath = expectedPath
            },
            cts.Token);

        var request = await received.Task.WaitAsync(cts.Token);
        await cts.CancelAsync();
        await listener;

        Assert.Equal(LaunchRequestKinds.OpenFile, request.Kind);
        Assert.Equal(expectedPath, request.FilePath);
    }
}
