using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Infrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

// Build DI container
var services = new ServiceCollection();
services.AddMarkdownConverter();
var provider = services.BuildServiceProvider();

var conversionService = provider.GetRequiredService<IConversionService>();
var converterFactory = provider.GetRequiredService<IConverterFactory>();

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "convert":
        return await HandleConvert(args, conversionService);

    case "formats":
        PrintFormats(converterFactory);
        return 0;

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
}

static async Task<int> HandleConvert(string[] args, IConversionService service)
{
    string? input = null;
    string? output = null;
    string formatStr = "pdf";
    string? bibliography = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--input" or "-i":
                input = args[++i];
                break;
            case "--output" or "-o":
                output = args[++i];
                break;
            case "--format" or "-f":
                formatStr = args[++i];
                break;
            case "--bibliography" or "-b":
                bibliography = args[++i];
                break;
        }
    }

    if (string.IsNullOrEmpty(input))
    {
        Console.Error.WriteLine("Error: --input is required.");
        return 1;
    }

    if (!File.Exists(input))
    {
        Console.Error.WriteLine($"Error: Input file not found: {input}");
        return 1;
    }

    if (!Enum.TryParse<ExportFormat>(formatStr, ignoreCase: true, out var format))
    {
        Console.Error.WriteLine($"Error: Unknown format '{formatStr}'. Use 'formats' command to see available formats.");
        return 1;
    }

    output ??= Path.ChangeExtension(input, format.ToString().ToLowerInvariant() switch
    {
        "pdf" => ".pdf",
        "word" => ".docx",
        "latex" => ".tex",
        _ => $".{format.ToString().ToLowerInvariant()}"
    });

    // Resolve bibliography: explicit path, or auto-discover .bib sidecar
    var options = new Dictionary<string, string>();
    if (!string.IsNullOrEmpty(bibliography))
    {
        options["bibliography"] = Path.GetFullPath(bibliography);
    }
    else
    {
        var autoBib = Path.ChangeExtension(input, ".bib");
        if (File.Exists(autoBib))
            options["bibliography"] = Path.GetFullPath(autoBib);
    }

    Console.WriteLine($"Converting: {input} -> {output} (format: {format})");
    if (options.ContainsKey("bibliography"))
        Console.WriteLine($"Bibliography: {options["bibliography"]}");

    var rawMarkdown = await File.ReadAllTextAsync(input);
    var result = await service.ConvertAsync(rawMarkdown, format, output, options);

    if (result.Success)
    {
        Console.WriteLine($"Success! Output: {result.OutputFilePath} ({result.BytesWritten:N0} bytes)");
        return 0;
    }
    else
    {
        Console.Error.WriteLine($"Conversion failed: {result.ErrorMessage}");
        return 1;
    }
}

static void PrintFormats(IConverterFactory factory)
{
    Console.WriteLine("Supported formats:");
    foreach (var format in factory.GetSupportedFormats())
    {
        Console.WriteLine($"  - {format}");
    }
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  mdconvert convert --input <file.md> [--output <file.pdf>] [--format <pdf|word>] [--bibliography <file.bib>]");
    Console.WriteLine("  mdconvert formats");
}
