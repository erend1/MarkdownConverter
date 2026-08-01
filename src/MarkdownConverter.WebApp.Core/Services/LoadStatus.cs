namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Outcome of a session load attempt. Distinguishes "no data yet"
/// from "data exists but is unreadable" so the UI can react appropriately.
/// </summary>
public enum LoadStatus
{
    Empty,
    Loaded,
    Corrupted
}
