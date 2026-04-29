namespace helengine.editor.launcher.Services;

/// <summary>
/// Represents launcher-local per-project settings that should not define the canonical project contract.
/// </summary>
public sealed class ProjectLocalSettingsDocument {
    /// <summary>
    /// Gets or sets the currently active platform identifier selected for this local workspace.
    /// </summary>
    public string ActivePlatform { get; set; } = string.Empty;
}
