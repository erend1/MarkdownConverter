# Blockquotes, Callouts & Attribution

This example demonstrates all blockquote features supported by MarkdownConverter.

## Simple Blockquote

> This is a simple blockquote. It renders with a grey left border,
> light background, and italic text in both PDF and Word output.

## Multi-paragraph Blockquote

> First paragraph of the quote. Blockquotes can contain **bold**,
> *italic*, and `inline code` just like regular text.
>
> Second paragraph continues the same blockquote. Each paragraph
> is separated by a blank `>` line.

## Nested Blockquotes

> This is the outer blockquote.
>
> > This is a nested blockquote inside the first one.
> > It gets deeper indentation and a darker border.
> >
> > > Triple nesting is also supported!

## Blockquote with Attribution

> Be the change you wish to see in the world.
>
> --- Mahatma Gandhi

> The only way to do great work is to love what you do.
>
> — Steve Jobs

## GitHub-Style Callouts (Admonitions)

### Note

> [!NOTE]
> This is a note callout. Use it to highlight information that
> users should take into account, even when skimming.

### Tip

> [!TIP]
> This is a tip callout. Use it for helpful advice and best practices
> that can save time or improve results.

### Important

> [!IMPORTANT]
> This is an important callout. Use it for crucial information
> necessary for users to succeed.

### Warning

> [!WARNING]
> This is a warning callout. Use it for urgent information that
> needs immediate user attention to avoid problems.

### Caution

> [!CAUTION]
> This is a caution callout. Use it to advise about risks or
> negative outcomes of certain actions.

## Callouts with Rich Content

> [!NOTE]
> Callouts can contain all standard Markdown formatting:
>
> - **Bold text** and *italic text*
> - `Inline code` snippets
> - [Links](https://example.com)
>
> They can also contain multiple paragraphs, just like regular blockquotes.

## Mixing Blockquotes and Callouts

Here is a regular quote followed by a callout:

> A ship in harbor is safe, but that is not what ships are built for.
>
> --- John A. Shedd

> [!TIP]
> When converting documents with blockquotes, the converter automatically
> detects the blockquote style and applies the appropriate formatting
> for each output format (PDF, LaTeX, or Word).
