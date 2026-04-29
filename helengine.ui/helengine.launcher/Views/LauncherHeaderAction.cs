using System;

namespace helengine.editor.launcher.Views;

/// <summary>
/// Describes one action button rendered inside the shared launcher header.
/// </summary>
public sealed class LauncherHeaderAction {
    /// <summary>
    /// Initializes one header action definition.
    /// </summary>
    /// <param name="label">Displayed button label.</param>
    /// <param name="kind">Visual priority used by the shell.</param>
    /// <param name="isEnabled">Whether the action can currently be invoked.</param>
    /// <param name="callback">Action raised when the header button is clicked.</param>
    public LauncherHeaderAction(string label, LauncherHeaderActionKind kind, bool isEnabled, Action callback) {
        Label = label;
        Kind = kind;
        IsEnabled = isEnabled;
        Callback = callback;
    }

    /// <summary>
    /// Gets the displayed button label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the visual priority used to style the action.
    /// </summary>
    public LauncherHeaderActionKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the action is currently enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the callback executed when the action is invoked.
    /// </summary>
    public Action Callback { get; }
}
