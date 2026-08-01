using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;
using Moq;

namespace MarkdownConverter.WebApp.Core.Tests;

public class PreviewPresenterTests
{
    private readonly Mock<IHtmlPreviewRenderer> _rendererMock;
    private readonly Mock<IPreviewView> _viewMock;
    private readonly PreviewPresenter _sut;

    public PreviewPresenterTests()
    {
        _rendererMock = new Mock<IHtmlPreviewRenderer>();
        _viewMock = new Mock<IPreviewView>();
        _viewMock.Setup(v => v.RenderMathAsync()).Returns(Task.CompletedTask);
        _sut = new PreviewPresenter(_rendererMock.Object);
        _sut.Attach(_viewMock.Object);
    }

    [Fact]
    public async Task RenderPreviewAsync_SetsHtmlContent()
    {
        _rendererMock.Setup(r => r.RenderToHtml("# Hello")).Returns("<h1>Hello</h1>");

        await _sut.RenderPreviewAsync("# Hello");

        Assert.Equal("<h1>Hello</h1>", _sut.ViewModel.HtmlContent);
    }

    [Fact]
    public async Task RenderPreviewAsync_CallsRenderMath()
    {
        _rendererMock.Setup(r => r.RenderToHtml(It.IsAny<string>())).Returns("");

        await _sut.RenderPreviewAsync("test");

        _viewMock.Verify(v => v.RenderMathAsync(), Times.Once);
    }

    [Fact]
    public async Task RenderPreviewAsync_SetsIsRenderingToFalseWhenDone()
    {
        _rendererMock.Setup(r => r.RenderToHtml(It.IsAny<string>())).Returns("");

        await _sut.RenderPreviewAsync("test");

        Assert.False(_sut.ViewModel.IsRendering);
    }

    [Fact]
    public async Task RenderPreviewAsync_RequestsRender()
    {
        _rendererMock.Setup(r => r.RenderToHtml(It.IsAny<string>())).Returns("");

        await _sut.RenderPreviewAsync("test");

        // Called twice: once for IsRendering=true, once for content update
        _viewMock.Verify(v => v.RequestRender(), Times.AtLeast(2));
    }

    [Fact]
    public async Task RenderPreviewAsync_EmptyString_ReturnsEmpty()
    {
        _rendererMock.Setup(r => r.RenderToHtml("")).Returns("");

        await _sut.RenderPreviewAsync("");

        Assert.Equal("", _sut.ViewModel.HtmlContent);
    }
}
