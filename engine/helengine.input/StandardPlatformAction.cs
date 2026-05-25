namespace helengine;

/// <summary>
/// Identifies one engine-owned platform-standard UI action that projects can query without hardcoding raw button names.
/// </summary>
public enum StandardPlatformAction {
    /// <summary>
    /// Confirms the current selection or activates the focused option.
    /// </summary>
    Accept = 0,

    /// <summary>
    /// Cancels the current state or returns to the previous menu or scene.
    /// </summary>
    Return = 1
}
