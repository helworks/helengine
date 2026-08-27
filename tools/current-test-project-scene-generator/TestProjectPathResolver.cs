namespace helengine.current_test_project_scene_generator {
    /// <summary>
    /// Resolves the engine repository and its maintained test project from a tool or test process location.
    /// </summary>
    public static class TestProjectPathResolver {
        /// <summary>
        /// Finds the repository root by walking parent directories from one existing or not-yet-created directory.
        /// </summary>
        /// <param name="startingDirectory">Directory from which the search should begin.</param>
        /// <returns>Absolute engine repository root path.</returns>
        public static string ResolveRepositoryRoot(string startingDirectory) {
            if (string.IsNullOrWhiteSpace(startingDirectory)) {
                throw new ArgumentException("Starting directory must be provided.", nameof(startingDirectory));
            }

            DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(startingDirectory));
            while (directory != null) {
                if (Directory.Exists(Path.Combine(directory.FullName, "engine")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "test-project")) &&
                    File.Exists(Path.Combine(directory.FullName, "helengine.ui", "helengine.sln"))) {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not find the helengine repository root from '{Path.GetFullPath(startingDirectory)}'.");
        }

        /// <summary>
        /// Resolves the repository's maintained test-project root from a tool process location.
        /// </summary>
        /// <param name="startingDirectory">Directory from which the repository search should begin.</param>
        /// <returns>Absolute test-project root path.</returns>
        public static string ResolveDefaultProjectRoot(string startingDirectory) {
            return Path.Combine(ResolveRepositoryRoot(startingDirectory), "test-project");
        }
    }
}
