namespace MarkdownConverter.WebApp.Core.Tests;

public class FindReplaceFixtureTests
{
    // The document-size x match-density matrix of the performance evidence.
    // 10 KiB cannot hold 10,000 five-character needles, so that cell does
    // not exist. The hashes pin the generator: the browser measurements
    // build their documents from the same definition and report the same
    // SHA-256, so the timed input and the unit-tested input are identical.
    [Theory]
    [InlineData(10 * 1024, 10, 10244, "4edef399296d1e4f7d4b8822ea4c9c9879836052aadf661ed255cfa3e1fee298")]
    [InlineData(10 * 1024, 1_000, 10251, "c95a31e79c9808b575d93ab91ede53d2d45d58b5f270efd4773fb8c3b4d306a5")]
    [InlineData(100 * 1024, 10, 102400, "c91386a147f60e22e57be245864cf63d3f3a6ce11a704da1c995d1bcdd78895a")]
    [InlineData(100 * 1024, 1_000, 102403, "6c7589dcf72acc1bbe50e6440f7796eca6426c7b27feb4fda9c82d3555792e55")]
    [InlineData(100 * 1024, 10_000, 102403, "eb5fa40d61deccc07744c2f1fb32166c893e3d3a30ff183c13aeb64df5900b6b")]
    [InlineData(1024 * 1024, 10, 1048578, "6042b55ea2a545211dc82c1d39c5709abec6e823f3b1717d20f14d9a07258fd8")]
    [InlineData(1024 * 1024, 1_000, 1048582, "dd81fdf7a7fc2e7fa23f1224ea8f99822621d07342cf2e01a17cedd41b2812e0")]
    [InlineData(1024 * 1024, 10_000, 1048581, "1ec8200e20a9598a29c18be066c9d2f76b43e87f51b169afcc403738dbb7f2dc")]
    public void Build_IsDeterministicAndHoldsExactlyTheRequestedMatches(
        int targetChars, int matchCount, int expectedLength, string expectedSha256)
    {
        var text = FindReplaceFixture.Build(targetChars, matchCount);

        Assert.Equal(expectedLength, text.Length);
        Assert.Equal(expectedSha256, FindReplaceFixture.Sha256(text));
        Assert.Equal(
            matchCount,
            FindReplaceFixture.Count(text, FindReplaceFixture.Needle, StringComparison.Ordinal));
        Assert.Equal(
            0,
            FindReplaceFixture.Count(text, FindReplaceFixture.Replacement, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain('\r', text);
    }
}
