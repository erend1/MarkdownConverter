using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;
using Moq;

namespace MarkdownConverter.WebApp.Core.Tests;

public class TabPresenterTests
{
    private readonly Mock<IPreviewPresenter> _previewMock;
    private readonly Mock<IDebouncer> _debouncerMock;
    private readonly Mock<ITabView> _viewMock;
    private readonly TabPresenter _sut;

    public TabPresenterTests()
    {
        _previewMock = new Mock<IPreviewPresenter>();
        _previewMock.Setup(p => p.RenderPreviewAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _debouncerMock = new Mock<IDebouncer>();
        _viewMock = new Mock<ITabView>();
        _sut = new TabPresenter(_previewMock.Object, _debouncerMock.Object);
        _sut.Attach(_viewMock.Object);
    }

    [Fact]
    public void Constructor_StartsWithOneTab()
    {
        Assert.Single(_sut.Tabs);
        Assert.Equal(0, _sut.ActiveIndex);
        Assert.StartsWith("Untitled ", _sut.ActiveTab.FileName);
    }

    [Fact]
    public void NewTab_AddsTabAndSwitchesToIt()
    {
        _sut.NewTab();

        Assert.Equal(2, _sut.Tabs.Count);
        Assert.Equal(1, _sut.ActiveIndex);
    }

    [Fact]
    public void NewTab_RequestsRenderAndFocus()
    {
        _sut.NewTab();

        _viewMock.Verify(v => v.RequestRender(), Times.Once);
        _viewMock.Verify(v => v.FocusEditor(), Times.Once);
    }

    [Fact]
    public void NewTab_RespectsMaxTabs()
    {
        for (int i = 0; i < 15; i++) _sut.NewTab();

        Assert.Equal(10, _sut.Tabs.Count); // MaxTabs = 10
        Assert.False(_sut.CanAddTab);
    }

    [Fact]
    public void SwitchTo_ChangesActiveIndex()
    {
        _sut.NewTab();
        _sut.SwitchTo(0);

        Assert.Equal(0, _sut.ActiveIndex);
    }

    [Fact]
    public void SwitchTo_TriggersPreviewRender()
    {
        _sut.OnActiveTabTextChanged("# Tab 1");
        _sut.NewTab();
        _sut.OnActiveTabTextChanged("# Tab 2");

        _previewMock.Invocations.Clear();
        _sut.SwitchTo(0);

        _previewMock.Verify(p => p.RenderPreviewAsync("# Tab 1"), Times.Once);
    }

    [Fact]
    public void SwitchTo_InvalidIndex_DoesNothing()
    {
        _sut.SwitchTo(-1);
        _sut.SwitchTo(99);

        Assert.Equal(0, _sut.ActiveIndex);
    }

    [Fact]
    public void SwitchTo_SameIndex_DoesNothing()
    {
        _viewMock.Invocations.Clear();
        _sut.SwitchTo(0);

        _viewMock.Verify(v => v.RequestRender(), Times.Never);
    }

    [Fact]
    public void CloseTab_RemovesTab()
    {
        _sut.NewTab();
        _sut.NewTab();
        Assert.Equal(3, _sut.Tabs.Count);

        _sut.CloseTab(1);

        Assert.Equal(2, _sut.Tabs.Count);
    }

    [Fact]
    public void CloseTab_LastTab_ResetsInsteadOfRemoving()
    {
        _sut.OnActiveTabTextChanged("some content");

        _sut.CloseTab(0);

        Assert.Single(_sut.Tabs);
        Assert.Equal(string.Empty, _sut.ActiveTab.MarkdownText);
        Assert.StartsWith("Untitled ", _sut.ActiveTab.FileName);
    }

    [Fact]
    public void CloseTab_ActiveTab_AdjustsIndex()
    {
        _sut.NewTab();
        _sut.NewTab();
        _sut.SwitchTo(2); // Active = index 2

        _sut.CloseTab(2);

        Assert.Equal(1, _sut.ActiveIndex); // Adjusted to last valid
    }

    [Fact]
    public void CloseTab_BeforeActive_AdjustsIndex()
    {
        _sut.NewTab();
        _sut.NewTab();
        _sut.SwitchTo(2);

        _sut.CloseTab(0);

        Assert.Equal(1, _sut.ActiveIndex); // Shifted down by 1
    }

    [Fact]
    public async Task OpenFileInNewTabAsync_AddsTabWithContent()
    {
        await _sut.OpenFileInNewTabAsync("readme.md", "# Hello");

        Assert.Equal(2, _sut.Tabs.Count);
        Assert.Equal(1, _sut.ActiveIndex);
        Assert.Equal("readme.md", _sut.ActiveTab.FileName);
        Assert.Equal("# Hello", _sut.ActiveTab.MarkdownText);
    }

    [Fact]
    public async Task OpenFileInNewTabAsync_WhenMaxTabs_ReplacesActive()
    {
        for (int i = 0; i < 9; i++) _sut.NewTab();
        Assert.Equal(10, _sut.Tabs.Count);

        await _sut.OpenFileInNewTabAsync("overflow.md", "content");

        Assert.Equal(10, _sut.Tabs.Count);
        Assert.Equal("overflow.md", _sut.ActiveTab.FileName);
    }

    [Fact]
    public void OnActiveTabTextChanged_UpdatesActiveTab()
    {
        _sut.OnActiveTabTextChanged("new text");

        Assert.Equal("new text", _sut.ActiveTab.MarkdownText);
        Assert.True(_sut.ActiveTab.IsDirty);
    }

    [Fact]
    public void OnActiveTabTextChanged_TriggersDebounce()
    {
        _sut.OnActiveTabTextChanged("test");

        _debouncerMock.Verify(d => d.Debounce(300, It.IsAny<Func<Task>>()), Times.Once);
    }

    [Fact]
    public void GetActiveTabContent_ReturnsCurrentText()
    {
        _sut.OnActiveTabTextChanged("# Content");

        Assert.Equal("# Content", _sut.GetActiveTabContent());
    }

    [Fact]
    public void TabViewModel_WordCount_CountsWords()
    {
        _sut.OnActiveTabTextChanged("one two three");

        Assert.Equal(3, _sut.ActiveTab.WordCount);
    }

    [Fact]
    public void TabViewModel_WordCount_EmptyIsZero()
    {
        Assert.Equal(0, _sut.ActiveTab.WordCount);
    }

    [Fact]
    public void TabViewModel_CharCount_CountsCharacters()
    {
        _sut.OnActiveTabTextChanged("abc");

        Assert.Equal(3, _sut.ActiveTab.CharCount);
    }

    [Fact]
    public void TabViewModel_LineCount_CountsLines()
    {
        _sut.OnActiveTabTextChanged("a\nb\nc");

        Assert.Equal(3, _sut.ActiveTab.LineCount);
    }

    [Fact]
    public void Tabs_AreIndependent()
    {
        _sut.OnActiveTabTextChanged("Tab 1 content");
        _sut.NewTab();
        _sut.OnActiveTabTextChanged("Tab 2 content");

        _sut.SwitchTo(0);
        Assert.Equal("Tab 1 content", _sut.ActiveTab.MarkdownText);

        _sut.SwitchTo(1);
        Assert.Equal("Tab 2 content", _sut.ActiveTab.MarkdownText);
    }

    [Fact]
    public void SetActiveTabScrollRatio_StoresScrollPerTab()
    {
        _sut.SetActiveTabScrollRatio(0.25);
        _sut.NewTab();
        _sut.SetActiveTabScrollRatio(0.75);

        _sut.SwitchTo(0);
        Assert.Equal(0.25, _sut.ActiveTab.ScrollRatio);

        _sut.SwitchTo(1);
        Assert.Equal(0.75, _sut.ActiveTab.ScrollRatio);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(2, 1)]
    public void SetActiveTabScrollRatio_ClampsToValidRange(double input, double expected)
    {
        _sut.SetActiveTabScrollRatio(input);

        Assert.Equal(expected, _sut.ActiveTab.ScrollRatio);
    }

    [Fact]
    public void NewTab_IncrementingNames()
    {
        // First tab is "Untitled 1.md" (from constructor)
        Assert.Contains("1", _sut.ActiveTab.FileName);

        _sut.NewTab();
        Assert.Contains("2", _sut.ActiveTab.FileName);

        _sut.NewTab();
        Assert.Contains("3", _sut.ActiveTab.FileName);
    }

    [Fact]
    public void Tabs_HaveUniqueIds()
    {
        _sut.NewTab();
        _sut.NewTab();

        var ids = _sut.Tabs.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // -------- Rebind-precondition regression tests --------
    // The textarea has @key=ActiveTab.Id, so opening / switching tabs
    // recreates the DOM element and the JS scroll-sync / Tab / shortcut
    // listeners die with it. MarkdownEditor.OnAfterRenderAsync re-binds when
    // ActiveTab.Id changes — these tests pin the C# guarantee that the Id
    // *does* change on those code paths, so the re-bind is guaranteed to fire.

    [Fact]
    public async Task OpenFileInNewTabAsync_ProducesNewActiveTabId()
    {
        var idBefore = _sut.ActiveTab.Id;

        await _sut.OpenFileInNewTabAsync("opened.md", "# opened");

        Assert.NotEqual(idBefore, _sut.ActiveTab.Id);
    }

    [Fact]
    public void NewTab_ProducesNewActiveTabId()
    {
        var idBefore = _sut.ActiveTab.Id;

        _sut.NewTab();

        Assert.NotEqual(idBefore, _sut.ActiveTab.Id);
    }

    [Fact]
    public void SwitchTo_ChangesActiveTabId()
    {
        _sut.NewTab();
        var idOnSecondTab = _sut.ActiveTab.Id;

        _sut.SwitchTo(0);

        Assert.NotEqual(idOnSecondTab, _sut.ActiveTab.Id);
    }

    [Fact]
    public void OnActiveTabTextChanged_DoesNotChangeActiveTabId()
    {
        // The hot path: every keystroke renders, but the rebind must not fire.
        // ActiveTab.Id is the rebind trigger — typing must not invalidate it.
        var idBefore = _sut.ActiveTab.Id;

        _sut.OnActiveTabTextChanged("a");
        _sut.OnActiveTabTextChanged("ab");
        _sut.OnActiveTabTextChanged("abc");

        Assert.Equal(idBefore, _sut.ActiveTab.Id);
    }

    // -------- Session save / restore regression tests --------

    private static TabPresenter CreatePresenterWithStorage(ILocalStorageService storage)
    {
        var preview = new Mock<IPreviewPresenter>();
        preview.Setup(p => p.RenderPreviewAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        var debouncer = new Mock<IDebouncer>();
        return new TabPresenter(preview.Object, debouncer.Object, storage);
    }

    [Fact]
    public async Task SaveStateAsync_NoStorage_ReturnsSuccess()
    {
        var result = await _sut.SaveStateAsync();

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SaveStateAsync_StorageSucceeds_ClearsDirtyFlags()
    {
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.SaveTabsAsync(It.IsAny<IReadOnlyList<TabState>>(), It.IsAny<int>()))
            .ReturnsAsync(SaveResult.Ok());
        var sut = CreatePresenterWithStorage(storage.Object);
        sut.OnActiveTabTextChanged("dirty content");
        Assert.True(sut.ActiveTab.IsDirty);

        var result = await sut.SaveStateAsync();

        Assert.True(result.Success);
        Assert.False(sut.ActiveTab.IsDirty);
    }

    [Fact]
    public async Task SaveStateAsync_StorageFails_KeepsDirtyAndSurfacesError()
    {
        // Regression: prior behaviour cleared dirty flags + showed a success toast
        // even when localStorage write threw.
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.SaveTabsAsync(It.IsAny<IReadOnlyList<TabState>>(), It.IsAny<int>()))
            .ReturnsAsync(SaveResult.Error("quota exceeded"));
        var sut = CreatePresenterWithStorage(storage.Object);
        sut.OnActiveTabTextChanged("dirty content");

        var result = await sut.SaveStateAsync();

        Assert.False(result.Success);
        Assert.Equal("quota exceeded", result.ErrorMessage);
        Assert.True(sut.ActiveTab.IsDirty); // not cleared on failure
    }

    [Fact]
    public async Task SaveStateAsync_PassesAllTabsAndActiveIndexToStorage()
    {
        IReadOnlyList<TabState>? captured = null;
        var capturedIndex = -1;
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.SaveTabsAsync(It.IsAny<IReadOnlyList<TabState>>(), It.IsAny<int>()))
            .Callback<IReadOnlyList<TabState>, int>((t, i) => { captured = t; capturedIndex = i; })
            .ReturnsAsync(SaveResult.Ok());
        var sut = CreatePresenterWithStorage(storage.Object);
        sut.OnActiveTabTextChanged("first body");
        await sut.OpenFileInNewTabAsync("second.md", "second body");

        await sut.SaveStateAsync();

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);
        Assert.Equal("first body", captured[0].Content);
        Assert.Equal("second.md", captured[1].FileName);
        Assert.Equal("second body", captured[1].Content);
        Assert.Equal(1, capturedIndex); // active is the newly opened tab
    }

    [Fact]
    public async Task RestoreStateAsync_NoStorage_ReturnsEmpty()
    {
        var status = await _sut.RestoreStateAsync();

        Assert.Equal(LoadStatus.Empty, status);
    }

    [Fact]
    public async Task RestoreStateAsync_NoData_ReturnsEmpty_KeepsDefaultUntitledTab()
    {
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.LoadTabsAsync()).ReturnsAsync(LoadResult.Empty());
        var sut = CreatePresenterWithStorage(storage.Object);

        var status = await sut.RestoreStateAsync();

        Assert.Equal(LoadStatus.Empty, status);
        Assert.Single(sut.Tabs);
        Assert.StartsWith("Untitled ", sut.ActiveTab.FileName);
    }

    [Fact]
    public async Task RestoreStateAsync_LoadedData_ReplacesTabs()
    {
        var stored = new List<TabState>
        {
            new() { FileName = "alpha.md", Content = "# Alpha" },
            new() { FileName = "beta.md", Content = "# Beta" }
        };
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.LoadTabsAsync()).ReturnsAsync(LoadResult.Loaded(stored, 1));
        var sut = CreatePresenterWithStorage(storage.Object);

        var status = await sut.RestoreStateAsync();

        Assert.Equal(LoadStatus.Loaded, status);
        Assert.Equal(2, sut.Tabs.Count);
        Assert.Equal("alpha.md", sut.Tabs[0].FileName);
        Assert.Equal("# Alpha", sut.Tabs[0].MarkdownText);
        Assert.Equal("beta.md", sut.Tabs[1].FileName);
        Assert.Equal(1, sut.ActiveIndex);
        Assert.False(sut.ActiveTab.IsDirty);
    }

    [Fact]
    public async Task RestoreStateAsync_Corrupted_DoesNotSilentlyReplaceTabs()
    {
        // Regression: corrupted localStorage used to look identical to "no data"
        // and silently dropped the user back to a fresh Untitled tab — exactly
        // the regression where saving appeared not to work.
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.LoadTabsAsync())
            .ReturnsAsync(LoadResult.Corrupted("invalid JSON"));
        var sut = CreatePresenterWithStorage(storage.Object);
        sut.OnActiveTabTextChanged("in-memory work");

        var status = await sut.RestoreStateAsync();

        Assert.Equal(LoadStatus.Corrupted, status);
        // The presenter must NOT clobber the in-memory tab when load fails.
        Assert.Single(sut.Tabs);
        Assert.Equal("in-memory work", sut.ActiveTab.MarkdownText);
    }

    [Fact]
    public async Task SaveThenRestore_RoundTrip_PreservesEverything()
    {
        // Use the real serializer + an in-memory storage stand-in to verify
        // the full save → load contract end-to-end.
        var fakeStorage = new InMemoryLocalStorage();
        var saver = CreatePresenterWithStorage(fakeStorage);
        saver.OnActiveTabTextChanged("# saved doc");
        await saver.OpenFileInNewTabAsync("notes.md", "notes body");
        await saver.SaveStateAsync();

        var loader = CreatePresenterWithStorage(fakeStorage);
        var status = await loader.RestoreStateAsync();

        Assert.Equal(LoadStatus.Loaded, status);
        Assert.Equal(2, loader.Tabs.Count);
        Assert.Equal("# saved doc", loader.Tabs[0].MarkdownText);
        Assert.Equal("notes.md", loader.Tabs[1].FileName);
        Assert.Equal("notes body", loader.Tabs[1].MarkdownText);
        Assert.Equal(1, loader.ActiveIndex);
    }

    [Fact]
    public async Task AutoSaveFailure_RaisesAutoSaveFailedEvent()
    {
        // Regression: autosaves were "fire and forget" — silent failures meant
        // the user thought Save worked when it had quietly stopped working.
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(s => s.SaveTabsAsync(It.IsAny<IReadOnlyList<TabState>>(), It.IsAny<int>()))
            .ReturnsAsync(SaveResult.Error("disk full"));
        var sut = CreatePresenterWithStorage(storage.Object);
        string? observedError = null;
        sut.AutoSaveFailed += msg => observedError = msg;

        // NewTab triggers an autosave internally.
        sut.NewTab();

        // Allow the fire-and-forget Task to settle.
        await Task.Delay(50);

        Assert.Equal("disk full", observedError);
    }

    /// <summary>
    /// Minimal in-memory <see cref="ILocalStorageService"/> for round-trip tests.
    /// Uses the real <see cref="LocalStorageSerializer"/> so the JSON contract
    /// is exercised end-to-end.
    /// </summary>
    private sealed class InMemoryLocalStorage : ILocalStorageService
    {
        private string? _payload;

        public Task<SaveResult> SaveTabsAsync(IReadOnlyList<TabState> tabs, int activeIndex)
        {
            _payload = LocalStorageSerializer.Serialize(tabs, activeIndex);
            return Task.FromResult(SaveResult.Ok());
        }

        public Task<LoadResult> LoadTabsAsync() =>
            Task.FromResult(LocalStorageSerializer.Deserialize(_payload));
    }
}
