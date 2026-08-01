using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MarkdownConverter.WebApp.Components.Primitives;

public partial class AppTextInput
{
    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string CssClass { get; set; } = string.Empty;
    [Parameter] public bool AutoFocus { get; set; }
    [Parameter] public bool StopClickPropagation { get; set; }
    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }
    [Parameter] public EventCallback OnBlur { get; set; }

    private Task OnInput(ChangeEventArgs e)
        => ValueChanged.InvokeAsync(e.Value?.ToString() ?? string.Empty);
}
