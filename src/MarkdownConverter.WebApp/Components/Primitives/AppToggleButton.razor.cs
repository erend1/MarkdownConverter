using Microsoft.AspNetCore.Components;

namespace MarkdownConverter.WebApp.Components.Primitives;

public partial class AppToggleButton
{
    [Parameter] public bool Active { get; set; }
    [Parameter] public bool Invalid { get; set; }
    [Parameter] public EventCallback OnToggle { get; set; }
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string? Title { get; set; }

    private string ComposedCssClass
    {
        get
        {
            var result = "find-toggle";
            if (Active) result += " active";
            if (Invalid) result += " invalid";
            return result;
        }
    }
}
