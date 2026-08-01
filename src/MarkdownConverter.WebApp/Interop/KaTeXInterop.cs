using Microsoft.JSInterop;

namespace MarkdownConverter.WebApp.Interop;

public sealed class KaTeXInterop
{
    private readonly IJSRuntime _js;

    public KaTeXInterop(IJSRuntime js) => _js = js;

    public async ValueTask RenderMathAsync(string containerId)
    {
        try
        {
            await _js.InvokeVoidAsync("katexInterop.renderMath", containerId);
        }
        catch
        {
            // KaTeX may not be loaded yet; silently ignore
        }
    }
}
