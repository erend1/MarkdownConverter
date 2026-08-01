using MarkdownConverter.Desktop;

namespace MarkdownConverter.Desktop.Tests;

public class PortStoreTests : IDisposable
{
    private readonly string _tempDir;

    public PortStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PortStoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    // -------- Read --------

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        var result = PortStore.Read(TempFile("missing.txt"));

        Assert.Null(result);
    }

    [Fact]
    public void Read_EmptyFile_ReturnsNull()
    {
        var path = TempFile("empty.txt");
        File.WriteAllText(path, "");

        Assert.Null(PortStore.Read(path));
    }

    [Fact]
    public void Read_WhitespaceFile_ReturnsNull()
    {
        var path = TempFile("ws.txt");
        File.WriteAllText(path, "   \r\n  ");

        Assert.Null(PortStore.Read(path));
    }

    [Fact]
    public void Read_NonNumericFile_ReturnsNull()
    {
        var path = TempFile("garbage.txt");
        File.WriteAllText(path, "not a port");

        Assert.Null(PortStore.Read(path));
    }

    [Fact]
    public void Read_OutOfRangeLow_ReturnsNull()
    {
        var path = TempFile("low.txt");
        File.WriteAllText(path, "0");

        Assert.Null(PortStore.Read(path));
    }

    [Fact]
    public void Read_OutOfRangeHigh_ReturnsNull()
    {
        var path = TempFile("high.txt");
        File.WriteAllText(path, "70000");

        Assert.Null(PortStore.Read(path));
    }

    [Fact]
    public void Read_ValidPort_ReturnsValue()
    {
        var path = TempFile("ok.txt");
        File.WriteAllText(path, "47823");

        Assert.Equal(47823, PortStore.Read(path));
    }

    [Fact]
    public void Read_ValidPortWithSurroundingWhitespace_IsTrimmed()
    {
        var path = TempFile("padded.txt");
        File.WriteAllText(path, "  47823\n");

        Assert.Equal(47823, PortStore.Read(path));
    }

    // -------- Write --------

    [Fact]
    public void Write_CreatesFileWithPortNumber()
    {
        var path = TempFile("created.txt");

        PortStore.Write(path, 51234);

        Assert.Equal("51234", File.ReadAllText(path));
    }

    [Fact]
    public void Write_OverwritesExistingFile()
    {
        var path = TempFile("over.txt");
        File.WriteAllText(path, "11111");

        PortStore.Write(path, 22222);

        Assert.Equal("22222", File.ReadAllText(path));
    }

    [Fact]
    public void Write_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "child", "port.txt");

        PortStore.Write(nestedPath, 40000);

        Assert.True(File.Exists(nestedPath));
        Assert.Equal(40000, PortStore.Read(nestedPath));
    }

    // -------- PickAvailable --------

    [Fact]
    public void PickAvailable_ReturnsPortInValidRange()
    {
        var port = PortStore.PickAvailable();

        Assert.InRange(port, 1, 65535);
    }

    // -------- ResolveStablePort --------

    [Fact]
    public void ResolveStablePort_NoSavedFile_PicksFreshAndPersists()
    {
        var path = TempFile("stable.txt");
        Assert.False(File.Exists(path));

        var resolved = PortStore.ResolveStablePort(path);

        Assert.InRange(resolved, 1, 65535);
        Assert.True(File.Exists(path));
        Assert.Equal(resolved, PortStore.Read(path));
    }

    [Fact]
    public void ResolveStablePort_SavedFreePort_ReusesIt()
    {
        // Pick a free port now, persist it, then verify Resolve reuses it.
        var freePort = PortStore.PickAvailable();
        var path = TempFile("reuse.txt");
        PortStore.Write(path, freePort);

        var resolved = PortStore.ResolveStablePort(path);

        Assert.Equal(freePort, resolved);
    }

    [Fact]
    public void ResolveStablePort_SavedButBound_PicksFreshAndPersists()
    {
        // Bind a port so it is not available, persist that port, then verify
        // Resolve falls back to a different one and writes the new value.
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var boundPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var path = TempFile("bound.txt");
            PortStore.Write(path, boundPort);

            var resolved = PortStore.ResolveStablePort(path);

            Assert.NotEqual(boundPort, resolved);
            Assert.Equal(resolved, PortStore.Read(path));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void ResolveStablePort_CorruptFile_RecoversWithFreshPort()
    {
        // Regression: a corrupt/non-numeric port file should not crash; the
        // resolver should silently move on and persist a fresh value.
        var path = TempFile("corrupt.txt");
        File.WriteAllText(path, "not a number");

        var resolved = PortStore.ResolveStablePort(path);

        Assert.InRange(resolved, 1, 65535);
        Assert.Equal(resolved, PortStore.Read(path));
    }
}
