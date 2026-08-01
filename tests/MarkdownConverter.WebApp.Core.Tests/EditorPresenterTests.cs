using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;
using Moq;

namespace MarkdownConverter.WebApp.Core.Tests;

public class EditorPresenterTests
{
    private readonly Mock<ITabPresenter> _tabMock;
    private readonly EditorPresenter _sut;

    public EditorPresenterTests()
    {
        _tabMock = new Mock<ITabPresenter>();
        _tabMock.Setup(t => t.ActiveTab).Returns(new TabViewModel());
        _tabMock.Setup(t => t.OpenFileInNewTabAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _sut = new EditorPresenter(_tabMock.Object);
    }

    [Fact]
    public void OnTextChanged_DelegatesToTabPresenter()
    {
        _sut.OnTextChanged("# Hello");

        _tabMock.Verify(t => t.OnActiveTabTextChanged("# Hello"), Times.Once);
    }

    [Fact]
    public void OnTextChanged_SyncsViewModel()
    {
        var tab = new TabViewModel { MarkdownText = "# Hello", IsDirty = true, FileName = "test.md" };
        _tabMock.Setup(t => t.ActiveTab).Returns(tab);

        _sut.OnTextChanged("# Hello");

        Assert.Equal("# Hello", _sut.ViewModel.MarkdownText);
        Assert.True(_sut.ViewModel.IsDirty);
    }

    [Fact]
    public async Task OnFileOpenAsync_DelegatesToTabPresenter()
    {
        await _sut.OnFileOpenAsync("readme.md", "# Title");

        _tabMock.Verify(t => t.OpenFileInNewTabAsync("readme.md", "# Title"), Times.Once);
    }

    [Fact]
    public void OnNewFile_DelegatesToTabPresenter()
    {
        _sut.OnNewFile();

        _tabMock.Verify(t => t.NewTab(), Times.Once);
    }

    [Fact]
    public void GetDownloadContent_DelegatesToTabPresenter()
    {
        _tabMock.Setup(t => t.GetActiveTabContent()).Returns("content");

        Assert.Equal("content", _sut.GetDownloadContent());
    }
}
