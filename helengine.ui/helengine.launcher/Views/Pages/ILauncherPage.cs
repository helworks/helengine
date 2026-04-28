using helengine.editor.launcher.Views;

namespace helengine.editor.launcher.Views.Pages;

/// <summary>
/// Supplies the shell-owned header metadata for one launcher page.
/// </summary>
public interface ILauncherPage {
    /// <summary>
    /// Builds the current header state for this page.
    /// </summary>
    /// <returns>Header state rendered by the shared launcher shell.</returns>
    LauncherHeaderState BuildHeaderState();
}
