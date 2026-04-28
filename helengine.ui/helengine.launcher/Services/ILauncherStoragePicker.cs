using System.Threading.Tasks;
using Avalonia.Controls;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Provides launcher-specific file and folder selection abstractions for shell orchestration code.
/// </summary>
public interface ILauncherStoragePicker {
    /// <summary>
    /// Opens a project-file picker restricted to helengine project files.
    /// </summary>
    /// <param name="owner">Control that owns the picker request.</param>
    /// <param name="title">Picker title shown to the user.</param>
    /// <returns>Selected local `.heproj` file path or an empty string when the picker is cancelled.</returns>
    Task<string> PickProjectFileAsync(Control owner, string title);

    /// <summary>
    /// Opens a folder picker for launcher flows that still operate on directories.
    /// </summary>
    /// <param name="owner">Control that owns the picker request.</param>
    /// <param name="title">Picker title shown to the user.</param>
    /// <returns>Selected local folder path or an empty string when the picker is cancelled.</returns>
    Task<string> PickFolderAsync(Control owner, string title);
}
