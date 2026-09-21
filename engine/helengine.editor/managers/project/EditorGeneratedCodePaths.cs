namespace helengine.editor {
    /// <summary>
    /// Resolves where a project's generated scripting workspace lives. Generated solutions, project files and their
    /// build outputs are regenerated from <c>assets/codebase</c> on demand, so they live under <c>cache/</c>, which a
    /// developer may delete at any time, never under <c>user_settings/</c>.
    /// </summary>
    public static class EditorGeneratedCodePaths {
        /// <summary>
        /// Project folder that holds regenerable state.
        /// </summary>
        public const string CacheFolderName = "cache";

        /// <summary>
        /// Folder beneath <see cref="CacheFolderName"/> that holds the generated scripting workspace.
        /// </summary>
        public const string GeneratedCodeFolderName = "generated_code";

        /// <summary>
        /// Resolves the absolute generated scripting workspace root for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <returns>Absolute path of <c>cache/generated_code</c> beneath the project.</returns>
        public static string ResolveWorkspaceRootPath(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            return Path.Combine(Path.GetFullPath(projectRootPath), CacheFolderName, GeneratedCodeFolderName);
        }
    }
}
