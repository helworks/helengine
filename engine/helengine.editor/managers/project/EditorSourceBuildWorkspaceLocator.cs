namespace helengine.editor {
    /// <summary>
    /// Resolves sibling source-build repository paths used by the local Windows build pipeline.
    /// </summary>
    public sealed class EditorSourceBuildWorkspaceLocator {
        /// <summary>
        /// Relative marker path used to detect the HelEngine source root.
        /// </summary>
        const string HelEngineEditorProjectRelativePath = "engine/helengine.editor/helengine.editor.csproj";

        /// <summary>
        /// Hidden git worktree directory name used by this source workspace.
        /// </summary>
        const string HiddenWorktreeDirectoryName = ".worktrees";

        /// <summary>
        /// Non-hidden git worktree directory name used by this source workspace.
        /// </summary>
        const string WorktreeDirectoryName = "worktrees";

        /// <summary>
        /// Resolves the HelEngine source root that contains the current editor assembly.
        /// </summary>
        /// <returns>Absolute HelEngine source root path.</returns>
        public string ResolveHelEngineRootPath() {
            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory)) {
                throw new InvalidOperationException("Application base directory could not be resolved.");
            }

            DirectoryInfo currentDirectory = new DirectoryInfo(baseDirectory);
            while (currentDirectory != null) {
                string markerPath = Path.Combine(currentDirectory.FullName, HelEngineEditorProjectRelativePath);
                if (File.Exists(markerPath)) {
                    return currentDirectory.FullName;
                }

                currentDirectory = currentDirectory.Parent;
            }

            throw new InvalidOperationException("HelEngine source root could not be resolved from the current editor build.");
        }

        /// <summary>
        /// Resolves the shared HelEngine source root that owns engine-level source-build settings even when the current editor build runs from a git worktree.
        /// </summary>
        /// <returns>Absolute shared HelEngine source root path.</returns>
        public string ResolveSharedHelEngineRootPath() {
            string helEngineRootPath = ResolveHelEngineRootPath();
            DirectoryInfo directoryInfo = new DirectoryInfo(helEngineRootPath);
            DirectoryInfo worktreeDirectory = directoryInfo.Parent;
            if (worktreeDirectory == null) {
                return helEngineRootPath;
            }

            string worktreeDirectoryName = worktreeDirectory.Name;
            if (!string.Equals(worktreeDirectoryName, HiddenWorktreeDirectoryName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(worktreeDirectoryName, WorktreeDirectoryName, StringComparison.OrdinalIgnoreCase)) {
                return helEngineRootPath;
            }

            DirectoryInfo sharedRootDirectory = worktreeDirectory.Parent;
            if (sharedRootDirectory == null) {
                throw new InvalidOperationException("Shared HelEngine source root could not be resolved from the current git worktree path.");
            }

            string markerPath = Path.Combine(sharedRootDirectory.FullName, HelEngineEditorProjectRelativePath);
            if (!File.Exists(markerPath)) {
                throw new InvalidOperationException($"Expected shared HelEngine source root was not found at '{sharedRootDirectory.FullName}'.");
            }

            return sharedRootDirectory.FullName;
        }

        /// <summary>
        /// Resolves the engine-level user-settings root shared by source builds and git worktrees.
        /// </summary>
        /// <returns>Absolute shared engine user-settings root path.</returns>
        public string ResolveSharedEngineUserSettingsRootPath() {
            string sharedHelEngineRootPath = ResolveSharedHelEngineRootPath();
            return Path.Combine(sharedHelEngineRootPath, "user_settings");
        }

        /// <summary>
        /// Resolves the sibling `csharpcodegen` source repository used by local source builds.
        /// </summary>
        /// <returns>Absolute `csharpcodegen` source root path.</returns>
        public string ResolveCSharpCodegenRootPath() {
            string helEngineRootPath = ResolveSharedHelEngineRootPath();
            string parentDirectoryPath = ResolveWorkspaceParentDirectoryPath(helEngineRootPath);
            string cSharpCodegenRootPath = Path.Combine(parentDirectoryPath, "csharpcodegen");
            if (!Directory.Exists(cSharpCodegenRootPath)) {
                throw new InvalidOperationException($"Expected source-build csharpcodegen repo was not found at '{cSharpCodegenRootPath}'.");
            }

            return Path.GetFullPath(cSharpCodegenRootPath);
        }

        /// <summary>
        /// Resolves the parent workspace directory that owns the sibling source repositories.
        /// </summary>
        /// <param name="helEngineRootPath">Absolute HelEngine source root path.</param>
        /// <returns>Absolute parent workspace directory path.</returns>
        string ResolveWorkspaceParentDirectoryPath(string helEngineRootPath) {
            if (string.IsNullOrWhiteSpace(helEngineRootPath)) {
                throw new ArgumentException("HelEngine root path must be provided.", nameof(helEngineRootPath));
            }

            DirectoryInfo directoryInfo = Directory.GetParent(Path.GetFullPath(helEngineRootPath));
            if (directoryInfo == null) {
                throw new InvalidOperationException("Workspace parent directory could not be resolved.");
            }

            return directoryInfo.FullName;
        }
    }
}
