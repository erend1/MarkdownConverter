using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Views;
using MarkdownConverter.Core.Interfaces;
using Moq;

namespace MarkdownConverter.WebApp.Core.Tests;

public class BibliographyPresenterTests
{
    private readonly Mock<IFileSystem> _fsMock;
    private readonly Mock<IBibliographyView> _viewMock;
    private readonly BibliographyPresenter _sut;

    public BibliographyPresenterTests()
    {
        _fsMock = new Mock<IFileSystem>();
        _viewMock = new Mock<IBibliographyView>();
        _sut = new BibliographyPresenter(_fsMock.Object);
        _sut.Attach(_viewMock.Object);
    }

    [Fact]
    public async Task OnBibFileUploadedAsync_StoresInFileSystem()
    {
        var bibContent = "@article{key, author={A}, title={B}, year={2024}}";
        await _sut.OnBibFileUploadedAsync("refs.bib", bibContent);

        _fsMock.Verify(f => f.WriteAllTextAsync("bibliography.bib", bibContent, default), Times.Once);
    }

    [Fact]
    public async Task OnBibFileUploadedAsync_SetsViewModel()
    {
        var bibContent = "@article{a, author={A}}\n@book{b, author={B}}";
        await _sut.OnBibFileUploadedAsync("refs.bib", bibContent);

        Assert.Equal("refs.bib", _sut.ViewModel.BibFileName);
        Assert.Equal(2, _sut.ViewModel.EntryCount);
        Assert.True(_sut.ViewModel.IsLoaded);
        Assert.Equal("bibliography.bib", _sut.ViewModel.VirtualPath);
    }

    [Fact]
    public async Task OnBibFileUploadedAsync_IgnoresCommentsAndPreamble()
    {
        var bibContent = "@comment{ignored}\n@preamble{stuff}\n@article{key, author={A}}";
        await _sut.OnBibFileUploadedAsync("refs.bib", bibContent);

        Assert.Equal(1, _sut.ViewModel.EntryCount);
    }

    [Fact]
    public async Task OnBibFileUploadedAsync_RequestsRender()
    {
        await _sut.OnBibFileUploadedAsync("test.bib", "@article{a}");

        _viewMock.Verify(v => v.RequestRender(), Times.Once);
    }

    [Fact]
    public void ClearBibliography_ResetsViewModel()
    {
        _sut.ClearBibliography();

        Assert.Null(_sut.ViewModel.BibFileName);
        Assert.Equal(0, _sut.ViewModel.EntryCount);
        Assert.False(_sut.ViewModel.IsLoaded);
        Assert.Null(_sut.ViewModel.VirtualPath);
    }
}
