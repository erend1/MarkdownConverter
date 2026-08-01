using Microsoft.AspNetCore.Components;

namespace MarkdownConverter.WebApp.Components;

public partial class ThemeToggle
{
    [Parameter] public bool IsDark { get; set; }
    [Parameter] public EventCallback OnToggle { get; set; }
}
