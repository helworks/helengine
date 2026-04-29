namespace helengine.editor {
    /// <summary>
    /// Provides helpers for generating unique asset names on disk.
    /// </summary>
    public static class AssetCreationUtils {
        /// <summary>
        /// Builds a unique file name in the target directory.
        /// </summary>
        /// <param name="directory">Directory where the file will be created.</param>
        /// <param name="baseName">Base name for the file.</param>
        /// <param name="extension">File extension including the leading dot.</param>
        /// <returns>Unique file name that does not collide on disk.</returns>
        public static string BuildUniqueFileName(string directory, string baseName, string extension) {
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new ArgumentException("Directory must be provided.", nameof(directory));
            }
            if (string.IsNullOrWhiteSpace(baseName)) {
                throw new ArgumentException("Base name must be provided.", nameof(baseName));
            }
            if (extension == null) {
                throw new ArgumentNullException(nameof(extension));
            }
            if (extension.Length > 0 && !extension.StartsWith(".", StringComparison.Ordinal)) {
                throw new ArgumentException("File extension must start with a dot.", nameof(extension));
            }

            int index = 0;
            while (true) {
                string suffix = BuildIndexSuffix(index);
                string name = string.Concat(baseName, suffix, extension);
                string path = Path.Combine(directory, name);
                if (!File.Exists(path)) {
                    return name;
                }

                index++;
            }
        }

        /// <summary>
        /// Builds a unique folder name in the target directory.
        /// </summary>
        /// <param name="directory">Directory where the folder will be created.</param>
        /// <param name="baseName">Base name for the folder.</param>
        /// <returns>Unique folder name that does not collide on disk.</returns>
        public static string BuildUniqueFolderName(string directory, string baseName) {
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new ArgumentException("Directory must be provided.", nameof(directory));
            }
            if (string.IsNullOrWhiteSpace(baseName)) {
                throw new ArgumentException("Base name must be provided.", nameof(baseName));
            }

            int index = 0;
            while (true) {
                string suffix = BuildIndexSuffix(index);
                string name = string.Concat(baseName, suffix);
                string path = Path.Combine(directory, name);
                if (!Directory.Exists(path)) {
                    return name;
                }

                index++;
            }
        }

        /// <summary>
        /// Builds the suffix used for duplicate names.
        /// </summary>
        /// <param name="index">Duplicate index for the suffix.</param>
        /// <returns>Suffix string including parentheses when needed.</returns>
        static string BuildIndexSuffix(int index) {
            if (index <= 0) {
                return string.Empty;
            }

            return " (" + index + ")";
        }
    }
}
