using Avalonia.Controls;
using helengine.editor.launcher.Services;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Supplies deterministic picker results to launcher shell tests.
/// </summary>
public sealed class FakeLauncherStoragePicker : ILauncherStoragePicker {
    readonly string ProjectFilePath;
    readonly string FolderPath;

    /// <summary>
    /// Creates the fake picker with one predefined project-file result and an empty folder result.
    /// </summary>
    /// <param name="projectFilePath">Project-file path to return for browse-project actions.</param>
    public FakeLauncherStoragePicker(string projectFilePath) : this(projectFilePath, string.Empty) {
    }

    /// <summary>
    /// Creates the fake picker with predefined project-file and folder results.
    /// </summary>
    /// <param name="projectFilePath">Project-file path to return for project-file browse actions.</param>
    /// <param name="folderPath">Folder path to return for folder browse actions.</param>
    public FakeLauncherStoragePicker(string projectFilePath, string folderPath) {
        ProjectFilePath = projectFilePath;
        FolderPath = folderPath;
    }

    /// <summary>
    /// Returns the predefined project-file path for launcher project selection.
    /// </summary>
    /// <param name="owner">Owning control that requested the picker.</param>
    /// <param name="title">Picker title.</param>
    /// <returns>Predefined project-file path or an empty string when simulating cancellation.</returns>
    public Task<string> PickProjectFileAsync(Control owner, string title) {
        return Task.FromResult(ProjectFilePath);
    }

    /// <summary>
    /// Returns the predefined folder path for launcher folder selection.
    /// </summary>
    /// <param name="owner">Owning control that requested the picker.</param>
    /// <param name="title">Picker title.</param>
    /// <returns>Predefined folder path or an empty string when simulating cancellation.</returns>
    public Task<string> PickFolderAsync(Control owner, string title) {
        return Task.FromResult(FolderPath);
    }
}
