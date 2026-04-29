namespace helengine.editor.launcher.Services;

/// <summary>
/// Represents launcher-local settings stored outside the canonical project contract.
/// </summary>
public sealed class LauncherSettingsDocument {
    /// <summary>
    /// Gets or sets the most recent canonical project file path created or opened from the launcher.
    /// </summary>
    public string LastProjectPath { get; set; } = string.Empty;
}
