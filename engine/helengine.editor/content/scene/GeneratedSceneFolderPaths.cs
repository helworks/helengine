namespace helengine.editor {
    /// <summary>
    /// Maps between a scene-mode source file and its sibling <c>&lt;source&gt;.generated</c> folder.
    /// </summary>
    public static class GeneratedSceneFolderPaths {
        /// <summary>
        /// Suffix appended to the source path to name its generated folder.
        /// </summary>
        public const string FolderSuffix = ".generated";

        /// <summary>
        /// Gets the generated folder path of a source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path of the scene-mode source.</param>
        /// <returns>Absolute path of its generated folder.</returns>
        public static string GetFolderPath(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            return sourcePath + FolderSuffix;
        }

        /// <summary>
        /// Gets the path of the blueprint the importer writes into the generated folder of a source.
        /// </summary>
        /// <param name="sourcePath">Absolute path of the scene-mode source.</param>
        /// <returns>Absolute path of the generated blueprint.</returns>
        public static string GetBlueprintPath(string sourcePath) {
            return Path.Combine(GetFolderPath(sourcePath), GetBlueprintFileName(sourcePath));
        }

        /// <summary>
        /// Gets the file name of the blueprint the importer writes for a source: the source name without extension plus the blueprint extension.
        /// </summary>
        /// <param name="sourcePath">Path of the scene-mode source.</param>
        /// <returns>Blueprint file name.</returns>
        public static string GetBlueprintFileName(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            return Path.GetFileNameWithoutExtension(sourcePath) + BlueprintAsset.FileExtension;
        }

        /// <summary>
        /// Finds the source file that owns a file inside a generated folder: the nearest ancestor folder named "&lt;name&gt;.generated" whose
        /// sibling "&lt;name&gt;" file exists.
        /// </summary>
        /// <param name="filePath">Absolute path of a file that may live inside a generated folder.</param>
        /// <param name="sourcePath">Absolute path of the owning source when found.</param>
        /// <returns>True when the file lies inside the generated folder of an existing source.</returns>
        public static bool TryGetOwningSourcePath(string filePath, out string sourcePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            string directoryPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
            while (!string.IsNullOrEmpty(directoryPath)) {
                string trimmed = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (trimmed.Length > FolderSuffix.Length && trimmed.EndsWith(FolderSuffix, StringComparison.OrdinalIgnoreCase)) {
                    string candidate = trimmed.Substring(0, trimmed.Length - FolderSuffix.Length);
                    if (File.Exists(candidate)) {
                        sourcePath = candidate;
                        return true;
                    }
                }

                directoryPath = Path.GetDirectoryName(trimmed);
            }

            sourcePath = string.Empty;
            return false;
        }
    }
}
