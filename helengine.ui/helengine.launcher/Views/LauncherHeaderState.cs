using System.Collections.Generic;

namespace helengine.editor.launcher.Views;

/// <summary>
/// Carries the active page metadata rendered by the shared launcher header.
/// </summary>
public sealed class LauncherHeaderState {
    /// <summary>
    /// Initializes one immutable launcher header snapshot.
    /// </summary>
    /// <param name="title">Primary page title shown in the header.</param>
    /// <param name="subtitle">Supporting page subtitle shown below the title.</param>
    /// <param name="actions">Action set rendered on the right side of the header.</param>
    public LauncherHeaderState(string title, string subtitle, IReadOnlyList<LauncherHeaderAction> actions) {
        Title = title;
        Subtitle = subtitle;
        Actions = actions;
    }

    /// <summary>
    /// Gets the title displayed for the active launcher page.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the supporting subtitle displayed for the active launcher page.
    /// </summary>
    public string Subtitle { get; }

    /// <summary>
    /// Gets the actions rendered by the shared launcher header.
    /// </summary>
    public IReadOnlyList<LauncherHeaderAction> Actions { get; }
}
