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

    [Fact]
    public async Task NextAsync_SeedsFromCaret_RevealsExplicitNextMatch()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(4, 4)
        };
        var sut = CreatePresenter(bridge);

        var result = await sut.NextAsync(Selector, "foo", FindOptions.Default);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Index);
        Assert.Equal(new TextRange(8, 11), bridge.Selection);
        Assert.Equal(
            ["GetValue", "GetSelection", "RevealSelection:8:11"],
            bridge.Calls);
    }

    [Fact]
    public async Task PrevAsync_SeedsFromCaret_SelectsNearestPreviousMatch()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(4, 4)
        };
        var sut = CreatePresenter(bridge);

        var result = await sut.PrevAsync(Selector, "foo", FindOptions.Default);

        Assert.Equal(2, result.Total);
        Assert.Equal(0, result.Index);
        Assert.Equal(new TextRange(0, 3), bridge.Selection);
        Assert.Equal(1, bridge.RevealCalls);
    }

    [Fact]
    public async Task PrevAsync_CaretBeforeFirstMatch_WrapsToLast()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(0, 0)
        };
        var sut = CreatePresenter(bridge);

        var result = await sut.PrevAsync(Selector, "foo", FindOptions.Default);

        Assert.Equal(1, result.Index);
        Assert.Equal(new TextRange(8, 11), bridge.Selection);
    }

    [Fact]
    public async Task ReplaceCurrentAsync_CollapsedDomSelection_ReplacesPresenterCurrentMatch()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(0, 0)
        };
        var sut = CreatePresenter(bridge);
        await sut.NextAsync(Selector, "foo", FindOptions.Default);
        bridge.Selection = new TextRange(0, 0);
        bridge.Calls.Clear();

        var result = await sut.ReplaceCurrentAsync(
            Selector, "foo", "zip", FindOptions.Default);

        Assert.Equal(1, result.Count);
        Assert.Equal("zip bar foo", bridge.Value);
        Assert.Equal(["GetValue", "SetSelection:0:3", "InsertText:zip"], bridge.Calls);
        Assert.Empty(sut.Matches);
    }

    [Fact]
    public async Task ReplaceCurrentAsync_WithoutCurrentMatch_DoesNotNavigateOrEdit()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(4, 7)
        };
        var sut = CreatePresenter(bridge);

        var result = await sut.ReplaceCurrentAsync(
            Selector, "foo", "zip", FindOptions.Default);

        Assert.Equal(0, result.Count);
        Assert.Equal("foo bar foo", bridge.Value);
        Assert.Equal(["GetValue"], bridge.Calls);
        Assert.DoesNotContain(bridge.Calls, call => call.StartsWith("InsertText:"));
        Assert.Equal(new TextRange(4, 7), bridge.Selection);
    }

    [Fact]
    public async Task ReplaceNextAsync_CompatibilityAlias_DoesNotNavigate()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(4, 7)
        };
        var sut = CreatePresenter(bridge);

        var result = await sut.ReplaceNextAsync(
            Selector, "foo", "zip", FindOptions.Default);

        Assert.Equal(0, result.Count);
        Assert.Null(result.Navigation);
        Assert.Equal("foo bar foo", bridge.Value);
        Assert.Equal(["GetValue"], bridge.Calls);
    }

    [Fact]
    public async Task ReplaceCurrentAsync_ChangedDocument_InvalidatesCurrentMatch()
    {
        var bridge = new RecordingEditorBridge("foo bar foo");
        var sut = CreatePresenter(bridge);
        await sut.NextAsync(Selector, "foo", FindOptions.Default);
        bridge.ReplaceValue("changed foo bar foo");
        bridge.Calls.Clear();

        var result = await sut.ReplaceCurrentAsync(
            Selector, "foo", "zip", FindOptions.Default);

        Assert.Equal(0, result.Count);
        Assert.Equal("changed foo bar foo", bridge.Value);
        Assert.Equal(-1, sut.CurrentIndex);
        Assert.Equal(["GetValue"], bridge.Calls);
    }

    [Fact]
    public async Task ReplaceCurrentAsync_InvalidRegex_ReturnsTypedFailureWithoutEditing()
    {
        var bridge = new RecordingEditorBridge("foo");
        var sut = CreatePresenter(bridge);

        var result = await sut.ReplaceCurrentAsync(
            Selector, "[", "zip", new FindOptions { Regex = true });

        Assert.Equal(FindFailure.InvalidPattern, result.Failure);
        Assert.Equal("foo", bridge.Value);
        Assert.Equal(["GetValue"], bridge.Calls);
    }

    [Fact]
    public async Task ReplaceAllAsync_UsesOneWholeDocumentEditAndResetsSession()
    {
        var bridge = new RecordingEditorBridge("foo bar foo");
        var sut = CreatePresenter(bridge);

        var result = await sut.ReplaceAllAsync(
            Selector, "foo", "zip", FindOptions.Default);

        Assert.Equal(2, result.Count);
        Assert.Equal("zip bar zip", bridge.Value);
        Assert.Equal(
            ["GetValue", "SetSelection:0:11", "InsertText:zip bar zip"],
            bridge.Calls);
        Assert.Empty(sut.Matches);
        Assert.Equal(-1, sut.CurrentIndex);
    }

    [Fact]
    public async Task ReplaceAllAsync_WithSelectionScope_ReplacesOnlyScopedMatches()
    {
        var bridge = new RecordingEditorBridge("foo foo foo")
        {
            Selection = new TextRange(0, 7)
        };
        var sut = CreatePresenter(bridge);
        await sut.SetScopeFromSelectionAsync(Selector);
        bridge.Calls.Clear();

        var result = await sut.ReplaceAllAsync(
            Selector,
            "foo",
            "x",
            new FindOptions { InSelection = true });

        Assert.Equal(2, result.Count);
        Assert.Equal("x x foo", bridge.Value);
        Assert.False(sut.HasScope);
        Assert.Equal(
            ["GetValue", "SetSelection:0:11", "InsertText:x x foo"],
            bridge.Calls);
    }

    [Fact]
    public async Task ReplaceCurrentAsync_WithScope_UsesScopedPresenterMatch()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(0, 3)
        };
        var sut = CreatePresenter(bridge);
        await sut.SetScopeFromSelectionAsync(Selector);
        await sut.NextAsync(
            Selector,
            "foo",
            new FindOptions { InSelection = true });
        bridge.Selection = new TextRange(8, 11);
        bridge.Calls.Clear();

        var result = await sut.ReplaceCurrentAsync(
            Selector,
            "foo",
            "zip",
            new FindOptions { InSelection = true });

        Assert.Equal(1, result.Count);
        Assert.Equal("zip bar foo", bridge.Value);
        Assert.False(sut.HasScope);
        Assert.Equal(
            ["GetValue", "SetSelection:0:3", "InsertText:zip"],
            bridge.Calls);
    }

    [Fact]
    public async Task ReplaceAllAsync_ChangedDocumentClearsStaleScopeBeforeEditing()
    {
        var bridge = new RecordingEditorBridge("foo bar foo")
        {
            Selection = new TextRange(0, 3)
        };
        var sut = CreatePresenter(bridge);
        await sut.SetScopeFromSelectionAsync(Selector);
        bridge.ReplaceValue("foo foo foo");
        bridge.Calls.Clear();

        var result = await sut.ReplaceAllAsync(
            Selector,
            "foo",
            "x",
            new FindOptions { InSelection = true });

        Assert.Equal(3, result.Count);
        Assert.Equal("x x x", bridge.Value);
        Assert.False(sut.HasScope);
    }

    [Fact]
    public async Task SetScopeFromSelectionAsync_EmptySelection_DoesNotActivateScope()
    {
        var bridge = new RecordingEditorBridge("foo")
        {
            Selection = new TextRange(1, 1)
        };
        var sut = CreatePresenter(bridge);

        await sut.SetScopeFromSelectionAsync(Selector);

        Assert.False(sut.HasScope);
        Assert.Equal(["GetSelection"], bridge.Calls);
    }

    [Fact]
    public async Task RefreshAgainst_DocumentEditClearsCapturedScope()
    {
        var bridge = new RecordingEditorBridge("foo foo foo")
        {
            Selection = new TextRange(0, 7)
        };
        var sut = CreatePresenter(bridge);
        await sut.SetScopeFromSelectionAsync(Selector);
        var scoped = sut.RefreshAgainst(
            "foo foo foo", "foo", new FindOptions { InSelection = true });
        Assert.True(sut.HasScope);
        Assert.Equal(2, scoped.Total);

        var unscoped = sut.RefreshAgainst(
            "changed foo foo", "foo", new FindOptions { InSelection = true });

        Assert.False(sut.HasScope);
        Assert.Equal(2, unscoped.Total);
        Assert.Equal(-1, unscoped.Index);
    }

    [Fact]
    public async Task ClearScope_SupersedesPendingScopeCapture()
    {
        var bridge = new DelayedSelectionEditorBridge(new TextRange(0, 3));
        var sut = CreatePresenter(bridge);

        var capture = sut.SetScopeFromSelectionAsync(Selector);
        await bridge.SelectionReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        sut.ClearScope();
        bridge.ReleaseSelectionRead();
        await capture.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(sut.HasScope);
    }

    [Fact]
    public async Task NextAsync_InvalidRegex_ReturnsTypedFailureWithoutBridgeWrites()
    {
        var bridge = new RecordingEditorBridge("alpha beta");
        var sut = CreatePresenter(bridge);

        var result = await sut.NextAsync(
            Selector, "[", new FindOptions { Regex = true });

        Assert.Equal(FindFailure.InvalidPattern, result.Failure);
        Assert.Equal(-1, result.Index);
        Assert.DoesNotContain(bridge.Calls, call =>
            call.StartsWith("RevealSelection:"));
    }

    [Fact]
    public async Task ReplaceAllAsync_RegexTimeout_ReturnsTypedFailureWithoutEditing()
    {
        var input = new string('a', 50_000) + "!";
        var bridge = new RecordingEditorBridge(input);
        var engine = new FindEngine(TimeSpan.FromMilliseconds(10));
        var sut = CreatePresenter(bridge, engine);

        var result = await sut.ReplaceAllAsync(
            Selector,
            "^(a+)+$",
            "x",
            new FindOptions { Regex = true });

        Assert.Equal(FindFailure.TimedOut, result.Failure);
        Assert.Equal(input, bridge.Value);
        Assert.DoesNotContain(bridge.Calls, call => call.StartsWith("InsertText:"));
    }

    private static FindPresenter CreatePresenter(
        IEditorBridge bridge,
        FindEngine? engine = null)
    {
        engine ??= new FindEngine();
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

        public ValueTask RevealSelectionAsync(string selector, int start, int end)
        {
            LastSelection = new TextRange(start, end);
            return ValueTask.CompletedTask;
        }

        public ValueTask<double> GetScrollRatioAsync(string selector) =>
            ValueTask.FromResult(0d);

        public ValueTask SetScrollRatioAsync(string selector, double ratio) =>
            ValueTask.CompletedTask;

        public ValueTask InsertTextAtCursorAsync(string selector, string text) =>
            ValueTask.CompletedTask;

        public ValueTask FocusAsync(string selector) =>
            ValueTask.CompletedTask;
    }

    private sealed class RecordingEditorBridge : IEditorBridge
    {
        public RecordingEditorBridge(string value) => Value = value;

        public string Value { get; private set; }
        public TextRange Selection { get; set; }
        public List<string> Calls { get; } = [];
        public int RevealCalls { get; private set; }

        public void ReplaceValue(string value) => Value = value;

        public ValueTask<TextRange> GetSelectionAsync(string selector)
        {
            Calls.Add("GetSelection");
            return ValueTask.FromResult(Selection);
        }

        public ValueTask SetSelectionAsync(string selector, int start, int end)
        {
            Calls.Add($"SetSelection:{start}:{end}");
            Selection = new TextRange(start, end);
            return ValueTask.CompletedTask;
        }

        public ValueTask<string> GetValueAsync(string selector)
        {
            Calls.Add("GetValue");
            return ValueTask.FromResult(Value);
        }

        public ValueTask RevealSelectionAsync(string selector, int start, int end)
        {
            Calls.Add($"RevealSelection:{start}:{end}");
            Selection = new TextRange(start, end);
            RevealCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<double> GetScrollRatioAsync(string selector) =>
            ValueTask.FromResult(0d);

        public ValueTask SetScrollRatioAsync(string selector, double ratio) =>
            ValueTask.CompletedTask;

        public ValueTask InsertTextAtCursorAsync(string selector, string text)
        {
            Calls.Add($"InsertText:{text}");
            Value = string.Concat(
                Value.AsSpan(0, Selection.Start),
                text,
                Value.AsSpan(Selection.End));
            var caret = Selection.Start + text.Length;
            Selection = new TextRange(caret, caret);
            return ValueTask.CompletedTask;
        }

        public ValueTask FocusAsync(string selector) => ValueTask.CompletedTask;
    }

    private sealed class DelayedSelectionEditorBridge : IEditorBridge
    {
        private readonly TextRange _selection;
        private readonly TaskCompletionSource _selectionReadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _selectionReadRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DelayedSelectionEditorBridge(TextRange selection) => _selection = selection;

        public TaskCompletionSource SelectionReadStarted => _selectionReadStarted;

        public void ReleaseSelectionRead() => _selectionReadRelease.TrySetResult();

        public async ValueTask<TextRange> GetSelectionAsync(string selector)
        {
            _selectionReadStarted.TrySetResult();
            await _selectionReadRelease.Task;
            return _selection;
        }

        public ValueTask SetSelectionAsync(string selector, int start, int end) =>
            ValueTask.CompletedTask;

        public ValueTask<string> GetValueAsync(string selector) =>
            ValueTask.FromResult(string.Empty);

        public ValueTask RevealSelectionAsync(string selector, int start, int end) =>
            ValueTask.CompletedTask;

        public ValueTask<double> GetScrollRatioAsync(string selector) =>
            ValueTask.FromResult(0d);

        public ValueTask SetScrollRatioAsync(string selector, double ratio) =>
            ValueTask.CompletedTask;

        public ValueTask InsertTextAtCursorAsync(string selector, string text) =>
            ValueTask.CompletedTask;

        public ValueTask FocusAsync(string selector) => ValueTask.CompletedTask;
    }
}
