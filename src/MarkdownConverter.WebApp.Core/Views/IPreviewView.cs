namespace MarkdownConverter.WebApp.Core.Views;

public interface IPreviewView
{
    void RequestRender();
    Task RenderMathAsync();
}
