using System.Text.Json;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class PdfCompileResultTests
{
    // The JSON shape produced by `fileInterop.compilePdf`. Pinning
    // deserialisation guards against the original bug where
    // System.Text.Json silently failed to read the result and a successful
    // compile was reported as an error.

    [Fact]
    public void Deserialize_SuccessShape_SetsSuccessTrue()
    {
        var json = """{"success":true}""";

        var result = JsonSerializer.Deserialize<PdfCompileResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Deserialize_FailureShape_CarriesError()
    {
        var json = """{"success":false,"error":"! Undefined control sequence \\foo"}""";

        var result = JsonSerializer.Deserialize<PdfCompileResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal(@"! Undefined control sequence \foo", result.Error);
    }

    [Fact]
    public void Deserialize_LongPdflatexLog_RoundTripsIntact()
    {
        // Realistic case: a multi-line LaTeX log that needs to reach the
        // ErrorDetailsDialog without any truncation or escaping issues.
        var log = "This is pdfTeX, Version 3.141592653-2.6-1.40.21\n" +
                  "Output written on document.pdf (1 page).\n" +
                  "Transcript written on document.log.\n" +
                  "! Undefined control sequence.\n" +
                  "l.42 \\unknownmacro";
        var payload = new { success = false, error = log };
        var json = JsonSerializer.Serialize(payload);

        var result = JsonSerializer.Deserialize<PdfCompileResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal(log, result.Error);
    }

    [Fact]
    public void Deserialize_MissingError_LeavesErrorNull()
    {
        var json = """{"success":false}""";

        var result = JsonSerializer.Deserialize<PdfCompileResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Null(result.Error);
    }
}
