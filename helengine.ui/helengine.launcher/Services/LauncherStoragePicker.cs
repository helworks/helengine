using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Uses Avalonia storage APIs to provide launcher file and folder selection.
/// </summary>
public sealed class LauncherStoragePicker : ILauncherStoragePicker {
    /// <summary>
    /// Opens a `.heproj` file picker and returns the selected local path.
    /// </summary>
    /// <param name="owner">Control that owns the picker request.</param>
    /// <param name="title">Picker title shown to the user.</param>
    /// <returns>Selected local `.heproj` file path or an empty string when the picker is cancelled.</returns>
    public async Task<string> PickProjectFileAsync(Control owner, string title) {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is { CanOpen: true } provider) {
            IReadOnlyList<IStorageFile> results = await provider.OpenFilePickerAsync(
                new FilePickerOpenOptions {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType> {
                        new("helengine project") {
                            Patterns = new[] { "*.heproj" }
                        }
                    }
                });

            string localPath = results
                .Select(currentFile => currentFile.TryGetLocalPath())
                .FirstOrDefault(currentPath => !string.IsNullOrWhiteSpace(currentPath))
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(localPath)) {
                return localPath;
            }

            return string.Empty;
        }

        throw new InvalidOperationException("Project file picking is not available on this platform.");
    }

    /// <summary>
    /// Opens a folder picker and returns the selected local path.
    /// </summary>
    /// <param name="owner">Control that owns the picker request.</param>
    /// <param name="title">Picker title shown to the user.</param>
    /// <returns>Selected local folder path or an empty string when the picker is cancelled.</returns>
    public async Task<string> PickFolderAsync(Control owner, string title) {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is { CanPickFolder: true } provider) {
            IReadOnlyList<IStorageFolder> results = await provider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions {
                    Title = title,
                    AllowMultiple = false
                });

            string localPath = results
                .Select(currentFolder => currentFolder.TryGetLocalPath())
                .FirstOrDefault(currentPath => !string.IsNullOrWhiteSpace(currentPath))
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(localPath)) {
                return localPath;
            }

            return string.Empty;
        }

        throw new InvalidOperationException("Folder picking is not available on this platform.");
    }
}
