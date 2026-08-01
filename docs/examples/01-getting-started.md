# Getting Started with MarkdownConverter

Welcome to **MarkdownConverter** — a .NET 8 tool that transforms your Markdown files into professional documents.

## What You Can Do

This tool supports three output formats:

1. **Word (.docx)** — Perfect for sharing editable documents
2. **PDF** — Ideal for polished, print-ready output
3. **LaTeX (.tex)** — Great for academic papers and further customization

## Quick Examples

### Text Formatting

You can use **bold**, *italic*, or even ***bold italic*** text. Inline code looks like `Console.WriteLine("Hello")` and stands out clearly in all formats.

### Code Blocks

Here is a simple C# example:

```csharp
public class Greeter
{
    public string Greet(string name)
    {
        return $"Hello, {name}!";
    }
}
```

### Links and Images

Visit the [.NET Documentation](https://learn.microsoft.com/dotnet) to learn more.

![MarkdownConverter icon](../../src/MarkdownConverter.WebApp/wwwroot/icon-192.png)

### Blockquotes

> Clean code always looks like it was written by someone who cares.
> — Robert C. Martin

### Lists

**Unordered:**

- Simple to use
- Extensible architecture
- Multiple output formats

**Ordered:**

1. Install .NET 8 SDK
2. Clone the repository
3. Run `dotnet build`
4. Convert your first document

---

*This example demonstrates basic Markdown features supported by MarkdownConverter.*
