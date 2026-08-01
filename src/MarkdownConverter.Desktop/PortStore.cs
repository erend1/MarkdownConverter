using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace MarkdownConverter.Desktop;

/// <summary>
/// Persists the local HTTP server port chosen at startup so that subsequent
/// launches reuse the same port — keeping the WebView2 origin
/// (and therefore localStorage) stable across restarts.
/// </summary>
public static class PortStore
{
    /// <summary>
    /// Reads a previously persisted port. Returns <c>null</c> if the file is
    /// missing, empty, non-numeric, or outside the valid TCP range.
    /// </summary>
    public static int? Read(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        string text;
        try
        {
            text = File.ReadAllText(filePath).Trim();
        }
        catch
        {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            return null;

        return port is >= 1 and <= 65535 ? port : null;
    }

    /// <summary>
    /// Persists the chosen port atomically. Creates the parent directory if needed.
    /// </summary>
    public static void Write(string filePath, int port)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, port.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Returns true if the given loopback port can be bound right now.
    /// </summary>
    public static bool IsAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            try { listener?.Stop(); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Asks the OS for an available loopback port.
    /// </summary>
    public static int PickAvailable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Returns the persisted port if it is still bindable, otherwise picks a
    /// fresh port from the OS and writes it through.
    /// </summary>
    public static int ResolveStablePort(string filePath)
    {
        var saved = Read(filePath);
        if (saved is int p && IsAvailable(p)) return p;

        var fresh = PickAvailable();
        try { Write(filePath, fresh); } catch { /* best effort — the port still works for this run */ }
        return fresh;
    }
}
