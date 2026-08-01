using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class ToastServiceTests
{
    [Fact]
    public void ShowSuccess_RaisesOnShowWithSuccessType()
    {
        var sut = new ToastService();
        ToastMessage? captured = null;
        sut.OnShow += m => captured = m;

        sut.ShowSuccess("ok");

        Assert.NotNull(captured);
        Assert.Equal("ok", captured!.Text);
        Assert.Equal("success", captured.Type);
        Assert.Null(captured.Details);
    }

    [Fact]
    public void ShowInfo_RaisesOnShowWithInfoType()
    {
        var sut = new ToastService();
        ToastMessage? captured = null;
        sut.OnShow += m => captured = m;

        sut.ShowInfo("fyi");

        Assert.NotNull(captured);
        Assert.Equal("info", captured!.Type);
    }

    [Fact]
    public void ShowError_WithoutDetails_HasNullDetails()
    {
        var sut = new ToastService();
        ToastMessage? captured = null;
        sut.OnShow += m => captured = m;

        sut.ShowError("something broke");

        Assert.NotNull(captured);
        Assert.Equal("something broke", captured!.Text);
        Assert.Equal("error", captured.Type);
        Assert.Null(captured.Details);
    }

    [Fact]
    public void ShowError_WithDetails_PropagatesDetailsForModal()
    {
        // Long pdflatex logs must reach Details so the user can
        // read them in the modal instead of the auto-dismissing toast.
        var longLog = new string('x', 5000);
        var sut = new ToastService();
        ToastMessage? captured = null;
        sut.OnShow += m => captured = m;

        sut.ShowError("PDF compilation failed", longLog);

        Assert.NotNull(captured);
        Assert.Equal("PDF compilation failed", captured!.Text);
        Assert.Equal(longLog, captured.Details);
    }

    [Fact]
    public void NoSubscribers_DoesNotThrow()
    {
        var sut = new ToastService();
        var ex = Record.Exception(() => sut.ShowError("nobody listening", "with details"));
        Assert.Null(ex);
    }
}
