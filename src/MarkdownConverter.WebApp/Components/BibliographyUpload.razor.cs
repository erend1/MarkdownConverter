using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Components;

public partial class BibliographyUpload : IBibliographyView
{
    [Inject] private IBibliographyPresenter Presenter { get; set; } = default!;
    [Inject] private IFileUploadService FileUploadService { get; set; } = default!;

    protected override void OnInitialized()
    {
        Presenter.Attach(this);
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null) return;

        using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
        var (name, content) = await FileUploadService.ReadTextFileAsync(stream, file.Name);
        await Presenter.OnBibFileUploadedAsync(name, content);
    }

    private void OnClear() => Presenter.ClearBibliography();

    public void RequestRender() => InvokeAsync(StateHasChanged);
}
