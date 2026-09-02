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
        /// Environment variable that can explicitly point the locator at the active HelEngine source root.
        /// </summary>
        const string HelEngineSourceRootEnvironmentVariableName = "HELENGINE_SOURCE_ROOT";

        /// <summary>
        /// Environment variable that can explicitly point the locator at engine-level user settings.
        /// </summary>
        const string EngineUserSettingsRootEnvironmentVariableName = "HELENGINE_ENGINE_USER_SETTINGS_ROOT";

        /// <summary>
        /// Hidden git worktree directory name used by this source workspace.
        /// </summary>
        const string HiddenWorktreeDirectoryName = ".worktrees";

        /// <summary>
        /// Non-hidden git worktree directory name used by this source workspace.
        /// </summary>
        const string WorktreeDirectoryName = "worktrees";

        /// <summary>
        /// Prefix written by Git into a worktree's file-based .git pointer.
        /// </summary>
        const string GitDirectoryPointerPrefix = "gitdir:";

        /// <summary>
        /// Resolves the HelEngine source root that contains the current editor assembly.
        /// </summary>
        /// <returns>Absolute HelEngine source root path.</returns>
        public string ResolveHelEngineRootPath() {
            if (TryResolveEnvironmentOverrideRootPath(out string environmentOverrideRootPath)) {
                return environmentOverrideRootPath;
            }

            string embeddedManifestRootPath = GeneratedHelengineSourceRoot.Path.Trim();
            if (!string.IsNullOrWhiteSpace(embeddedManifestRootPath)) {
                string fullEmbeddedManifestRootPath = Path.GetFullPath(embeddedManifestRootPath);
                string embeddedMarkerPath = Path.Combine(fullEmbeddedManifestRootPath, HelEngineEditorProjectRelativePath);
                if (File.Exists(embeddedMarkerPath)) {
                    return fullEmbeddedManifestRootPath;
                }
            }

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
        /// Attempts to resolve the HelEngine source root from an explicit environment-variable override.
        /// </summary>
        /// <param name="helEngineRootPath">Resolved HelEngine source root path when the override is valid.</param>
        /// <returns>True when a valid override was supplied; otherwise false.</returns>
        bool TryResolveEnvironmentOverrideRootPath(out string helEngineRootPath) {
            helEngineRootPath = string.Empty;
            string configuredRootPath = Environment.GetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(configuredRootPath)) {
                return false;
            }

            string fullConfiguredRootPath = Path.GetFullPath(configuredRootPath);
            string markerPath = Path.Combine(fullConfiguredRootPath, HelEngineEditorProjectRelativePath);
            if (!File.Exists(markerPath)) {
                throw new InvalidOperationException($"The HelEngine source-root override '{fullConfiguredRootPath}' does not contain '{HelEngineEditorProjectRelativePath}'.");
            }

            helEngineRootPath = fullConfiguredRootPath;
            return true;
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
            if (sharedRootDirectory != null) {
                string markerPath = Path.Combine(sharedRootDirectory.FullName, HelEngineEditorProjectRelativePath);
                if (File.Exists(markerPath)) {
                    return sharedRootDirectory.FullName;
                }
            }

            if (TryResolveGitWorktreeMainRootPath(helEngineRootPath, out string gitWorktreeMainRootPath)) {
                return gitWorktreeMainRootPath;
            }

            if (sharedRootDirectory == null) {
                throw new InvalidOperationException("Shared HelEngine source root could not be resolved from the current git worktree path.");
            }

            throw new InvalidOperationException($"Expected shared HelEngine source root was not found at '{sharedRootDirectory.FullName}'.");
        }

        /// <summary>
        /// Resolves and validates the main checkout named by a file-based Git worktree pointer.
        /// Git stores the pointer as <c>gitdir: &lt;main&gt;/.git/worktrees/&lt;name&gt;</c>; only that
        /// conventional metadata shape and a checkout containing the HelEngine marker are accepted.
        /// </summary>
        /// <param name="worktreeRootPath">Absolute source root of the active Git worktree.</param>
        /// <param name="mainRootPath">Validated main checkout path when the pointer is usable.</param>
        /// <returns>True when the worktree pointer identifies a valid main checkout; otherwise false.</returns>
        static bool TryResolveGitWorktreeMainRootPath(string worktreeRootPath, out string mainRootPath) {
            mainRootPath = string.Empty;
            string gitPointerPath = Path.Combine(worktreeRootPath, ".git");
            if (!File.Exists(gitPointerPath)) {
                return false;
            }

            string pointerContents;
            try {
                pointerContents = File.ReadAllText(gitPointerPath).Trim();
            } catch (IOException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }

            if (!pointerContents.StartsWith(GitDirectoryPointerPrefix, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string gitDirectoryPath = pointerContents.Substring(GitDirectoryPointerPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(gitDirectoryPath)) {
                return false;
            }

            string fullGitDirectoryPath;
            try {
                fullGitDirectoryPath = Path.IsPathRooted(gitDirectoryPath)
                    ? Path.GetFullPath(gitDirectoryPath)
                    : Path.GetFullPath(Path.Combine(worktreeRootPath, gitDirectoryPath));
            } catch (ArgumentException) {
                return false;
            } catch (NotSupportedException) {
                return false;
            }

            if (!Directory.Exists(fullGitDirectoryPath)) {
                return false;
            }

            DirectoryInfo worktreeMetadataDirectory = new DirectoryInfo(fullGitDirectoryPath);
            DirectoryInfo worktreesDirectory = worktreeMetadataDirectory.Parent;
            DirectoryInfo gitDirectory = worktreesDirectory?.Parent;
            DirectoryInfo mainRootDirectory = gitDirectory?.Parent;
            if (worktreesDirectory == null || gitDirectory == null || mainRootDirectory == null
                || !string.Equals(worktreesDirectory.Name, WorktreeDirectoryName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(gitDirectory.Name, ".git", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(mainRootDirectory.FullName, HelEngineEditorProjectRelativePath))) {
                return false;
            }

            mainRootPath = mainRootDirectory.FullName;
            return true;
        }

        /// <summary>
        /// Resolves the engine-level user-settings root shared by source builds and git worktrees.
        /// </summary>
        /// <returns>Absolute shared engine user-settings root path.</returns>
        public string ResolveSharedEngineUserSettingsRootPath() {
            string configuredRootPath = Environment.GetEnvironmentVariable(EngineUserSettingsRootEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configuredRootPath)) {
                return Path.GetFullPath(configuredRootPath);
            }

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
