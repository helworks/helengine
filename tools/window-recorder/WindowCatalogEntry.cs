namespace helengine.tools.windowrecorder;
/// <summary>Metadata for one visible top-level window; listing never captures its pixels.</summary>
sealed class WindowCatalogEntry {
    /// <summary>Explicit handle formatted for the record command.</summary>
    public string Hwnd { get; init; }
    /// <summary>Owning process identifier.</summary>
    public uint ProcessId { get; init; }
    /// <summary>Process name when accessible, empty if the process disappears or denies access.</summary>
    public string ProcessName { get; init; }
    /// <summary>Current window title, which may be empty or change after enumeration.</summary>
    public string Title { get; init; }
}
