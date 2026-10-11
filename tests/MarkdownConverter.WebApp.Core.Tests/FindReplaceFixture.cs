using System.Security.Cryptography;
using System.Text;

namespace MarkdownConverter.WebApp.Core.Tests;

/// <summary>
/// Deterministic synthetic Markdown for find / replace tests and for the
/// real-browser performance matrix (document size x match density). The
/// browser measurements generate the same text from the same definition, so
/// a unit-test result and a timing always refer to an identical input:
/// <list type="bullet">
/// <item>Tokens are emitted until the text is at least <c>targetChars</c>
/// long and all <c>matchCount</c> needles were emitted.</item>
/// <item>Needle <c>k</c> (0-based) is emitted as soon as the text length
/// reaches <c>floor((2k + 1) * targetChars / (2 * matchCount))</c>, the
/// centre of the k-th of <c>matchCount</c> equal segments.</item>
/// <item>Every other token is <c>Filler[state % Filler.Length]</c> with
/// <c>state = state * 1664525 + 1013904223 (mod 2^32)</c>, seed
/// <c>0x9E3779B9</c>.</item>
/// <item>Token <c>i</c> (0-based) is preceded by
/// <c>"## Section {i / 480 + 1}\n\n"</c> when <c>i % 480 == 0</c> and
/// followed by <c>"\n\n"</c> when <c>(i + 1) % 96 == 0</c>, else
/// <c>"\n"</c> when <c>(i + 1) % 12 == 0</c>, else a space.</item>
/// </list>
/// The text is ASCII with LF line endings, and no filler word contains the
/// needle or the replacement.
/// </summary>
internal static class FindReplaceFixture
{
    public const string Needle = "alpha";
    public const string Replacement = "beta";

    private static readonly string[] Filler =
    [
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
        "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et",
        "dolore", "magna", "aliqua", "enim", "minim", "veniam", "quis", "nostrud"
    ];

    public static string Build(int targetChars, int matchCount)
    {
        var state = 0x9E3779B9u;
        var text = new StringBuilder(targetChars + 64);
        var emitted = 0;
        var index = 0;

        while (text.Length < targetChars || emitted < matchCount)
        {
            if (index % 480 == 0) text.Append("## Section ").Append(index / 480 + 1).Append("\n\n");

            if (emitted < matchCount
                && text.Length >= (2L * emitted + 1) * targetChars / (2L * matchCount))
            {
                text.Append(Needle);
                emitted++;
            }
            else
            {
                state = unchecked(state * 1664525u + 1013904223u);
                text.Append(Filler[state % (uint)Filler.Length]);
            }

            index++;
            if (index % 96 == 0) text.Append("\n\n");
            else if (index % 12 == 0) text.Append('\n');
            else text.Append(' ');
        }

        return text.ToString();
    }

    public static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static int Count(string text, string value, StringComparison comparison)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(value, at, comparison)) >= 0)
        {
            count++;
            at += value.Length;
        }

        return count;
    }
}
