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
        /// Resolves the sibling `csharpcodegen` source repository used by local source builds.
        /// </summary>
        /// <returns>Absolute `csharpcodegen` source root path.</returns>
        public string ResolveCSharpCodegenRootPath() {
            string helEngineRootPath = ResolveHelEngineRootPath();
            string parentDirectoryPath = ResolveWorkspaceParentDirectoryPath(helEngineRootPath);
            string cSharpCodegenRootPath = Path.Combine(parentDirectoryPath, "csharpcodegen");
            if (!Directory.Exists(cSharpCodegenRootPath)) {
                throw new InvalidOperationException($"Expected source-build csharpcodegen repo was not found at '{cSharpCodegenRootPath}'.");
            }

            return Path.GetFullPath(cSharpCodegenRootPath);
        }

        /// <summary>
        /// Resolves the sibling `helengine-windows` source repository used by local Windows source builds.
        /// </summary>
        /// <returns>Absolute `helengine-windows` source root path.</returns>
        public string ResolveHelEngineWindowsRootPath() {
            string helEngineRootPath = ResolveHelEngineRootPath();
            string parentDirectoryPath = ResolveWorkspaceParentDirectoryPath(helEngineRootPath);
            string helEngineWindowsRootPath = Path.Combine(parentDirectoryPath, "helworks", "helengine-windows");
            if (!Directory.Exists(helEngineWindowsRootPath)) {
                throw new InvalidOperationException($"Expected source-build helengine-windows repo was not found at '{helEngineWindowsRootPath}'.");
            }

            return Path.GetFullPath(helEngineWindowsRootPath);
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
