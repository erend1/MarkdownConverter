using Microsoft.AspNetCore.Components;

namespace MarkdownConverter.WebApp.Components.Primitives;

public partial class AppIconButton
{
    [Parameter] public string IconClass { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? LucideIcon { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public bool StopPropagation { get; set; } = true;
}
