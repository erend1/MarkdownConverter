using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class FindPresenterTests
{
    private const string Selector = ".editor-textarea";

    [Fact]
    public async Task OverlappingNavigation_WithSameQuery_IsSerializedInOrder()
    {
        var bridge = new ControlledEditorBridge("foo foo");
        var sut = CreatePresenter(bridge);

        var first = sut.NextAsync(Selector, "foo", FindOptions.Default);
        await bridge.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var second = sut.NextAsync(Selector, "foo", FindOptions.Default);
        Assert.Equal(1, bridge.ValueReadCalls);

        bridge.ReleaseFirstRead();
        var results = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, bridge.MaxConcurrentValueReads);
        Assert.False(results[0].IsStale);
        Assert.False(results[1].IsStale);
        Assert.Equal(0, results[0].Index);
        Assert.Equal(1, results[1].Index);
    }

    [Fact]
    public async Task NewQuery_SupersedesOlderOperationWithoutApplyingOldSelection()
    {
        var bridge = new ControlledEditorBridge("foo bar");
        var sut = CreatePresenter(bridge);

        var oldOperation = sut.NextAsync(Selector, "foo", FindOptions.Default);
        await bridge.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var newOperation = sut.NextAsync(Selector, "bar", FindOptions.Default);
        bridge.ReleaseFirstRead();

        var oldResult = await oldOperation.WaitAsync(TimeSpan.FromSeconds(2));
        var newResult = await newOperation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(oldResult.IsStale);
        Assert.False(newResult.IsStale);
        Assert.Equal(new TextRange(4, 7), bridge.LastSelection);
        Assert.Equal(new TextMatch(4, 7), Assert.Single(sut.Matches));
    }

    [Fact]
    public async Task InteropException_ReleasesGateForFollowingOperation()
    {
        var bridge = new ControlledEditorBridge("foo") { ThrowOnFirstRead = true };
        var sut = CreatePresenter(bridge);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.NextAsync(Selector, "foo", FindOptions.Default));

        var result = await sut.NextAsync(Selector, "foo", FindOptions.Default)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(result.IsStale);
        Assert.Equal(0, result.Index);
        Assert.Equal(2, bridge.ValueReadCalls);
    }

    private static FindPresenter CreatePresenter(IEditorBridge bridge)
    {
        var engine = new FindEngine();
        return new FindPresenter(engine, new FindSession(engine), bridge);
    }

    private sealed class ControlledEditorBridge : IEditorBridge
    {
        private readonly string _value;
        private readonly TaskCompletionSource _firstReadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstReadRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeValueReads;

        public ControlledEditorBridge(string value) => _value = value;

        public TaskCompletionSource FirstReadStarted => _firstReadStarted;
        public bool ThrowOnFirstRead { get; init; }
        public int ValueReadCalls { get; private set; }
        public int MaxConcurrentValueReads { get; private set; }
        public TextRange LastSelection { get; private set; }

        public void ReleaseFirstRead() => _firstReadRelease.TrySetResult();

        public ValueTask<TextRange> GetSelectionAsync(string selector) =>
            ValueTask.FromResult(new TextRange(0, 0));

        public ValueTask SetSelectionAsync(string selector, int start, int end)
        {
            LastSelection = new TextRange(start, end);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<string> GetValueAsync(string selector)
        {
            ValueReadCalls++;
            var active = Interlocked.Increment(ref _activeValueReads);
            MaxConcurrentValueReads = Math.Max(MaxConcurrentValueReads, active);
            try
            {
                if (ValueReadCalls == 1)
                {
                    _firstReadStarted.TrySetResult();
                    if (ThrowOnFirstRead)
                        throw new InvalidOperationException("Simulated interop failure.");

                    await _firstReadRelease.Task;
                }

                return _value;
            }
            finally
            {
                Interlocked.Decrement(ref _activeValueReads);
            }
        }

        public ValueTask ScrollSelectionIntoViewAsync(string selector) =>
            ValueTask.CompletedTask;

        public ValueTask<double> GetScrollRatioAsync(string selector) =>
            ValueTask.FromResult(0d);

        public ValueTask SetScrollRatioAsync(string selector, double ratio) =>
            ValueTask.CompletedTask;

        public ValueTask InsertTextAtCursorAsync(string selector, string text) =>
            ValueTask.CompletedTask;

        public ValueTask FocusAsync(string selector) =>
            ValueTask.CompletedTask;
    }
}
