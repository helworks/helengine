namespace helengine.editor {
    /// <summary>
    /// Copies built game-script binaries into an isolated snapshot and loads them into a collectible context.
    /// </summary>
    public sealed class EditorGameScriptAssemblyHost : IEditorScriptAssemblyHost {
        /// <summary>
        /// Root directory that owns all script snapshots for the current project.
        /// </summary>
        readonly string SnapshotRootPath;

        /// <summary>
        /// Currently loaded collectible context for the active script assembly.
        /// </summary>
        EditorCollectibleScriptAssemblyLoadContext CurrentLoadContext;

        /// <summary>
        /// Snapshot directory that backs the currently loaded collectible context.
        /// </summary>
        string CurrentSnapshotDirectoryPath;

        /// <summary>
        /// Initializes one assembly host rooted under the supplied project directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        public EditorGameScriptAssemblyHost(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            SnapshotRootPath = Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "script_snapshots");
            DeleteDirectoryIfPresent(SnapshotRootPath);
            Directory.CreateDirectory(SnapshotRootPath);
        }

        /// <summary>
        /// Reloads the current script assembly by copying the build output into a snapshot and loading it.
        /// </summary>
        /// <param name="sourceOutputDirectoryPath">Absolute path to the fresh build output directory.</param>
        /// <param name="mainAssemblyPath">Absolute path to the main scripting assembly inside the build output.</param>
        public void Reload(string sourceOutputDirectoryPath, string mainAssemblyPath) {
            if (string.IsNullOrWhiteSpace(sourceOutputDirectoryPath)) {
                throw new ArgumentException("Source output directory path must be provided.", nameof(sourceOutputDirectoryPath));
            }
            if (string.IsNullOrWhiteSpace(mainAssemblyPath)) {
                throw new ArgumentException("Main assembly path must be provided.", nameof(mainAssemblyPath));
            }
            if (!Directory.Exists(sourceOutputDirectoryPath)) {
                throw new DirectoryNotFoundException($"Script build output directory '{sourceOutputDirectoryPath}' does not exist.");
            }
            if (!File.Exists(mainAssemblyPath)) {
                throw new FileNotFoundException($"Script assembly '{mainAssemblyPath}' was not produced.", mainAssemblyPath);
            }

            string snapshotDirectoryPath = Path.Combine(SnapshotRootPath, Guid.NewGuid().ToString("N"));
            CopyDirectory(sourceOutputDirectoryPath, snapshotDirectoryPath);

            string snapshotAssemblyPath = Path.Combine(snapshotDirectoryPath, Path.GetFileName(mainAssemblyPath));
            EditorCollectibleScriptAssemblyLoadContext nextLoadContext = null;
            try {
                nextLoadContext = new EditorCollectibleScriptAssemblyLoadContext(snapshotAssemblyPath);
                nextLoadContext.LoadFromAssemblyPath(snapshotAssemblyPath);

                EditorCollectibleScriptAssemblyLoadContext previousLoadContext = CurrentLoadContext;
                string previousSnapshotDirectoryPath = CurrentSnapshotDirectoryPath;
                CurrentLoadContext = null;
                CurrentSnapshotDirectoryPath = null;

                if (previousLoadContext != null) {
                    WeakReference previousLoadContextReference = BeginUnload(previousLoadContext);
                    previousLoadContext = null;
                    WaitForUnload(previousLoadContextReference);
                }

                CurrentLoadContext = nextLoadContext;
                CurrentSnapshotDirectoryPath = snapshotDirectoryPath;
            } catch {
                if (nextLoadContext != null) {
                    WeakReference nextLoadContextReference = BeginUnload(nextLoadContext);
                    nextLoadContext = null;
                    WaitForUnload(nextLoadContextReference);
                }

                CurrentLoadContext = previousLoadContext;
                CurrentSnapshotDirectoryPath = previousSnapshotDirectoryPath;
                throw;
            }
        }

        /// <summary>
        /// Releases the current collectible context when the host is disposed.
        /// </summary>
        public void Dispose() {
            if (CurrentLoadContext == null) {
                return;
            }

            try {
                WeakReference loadContextReference = BeginUnload(CurrentLoadContext);
                CurrentLoadContext = null;
                WaitForUnload(loadContextReference);
            } catch {
            }

            CurrentLoadContext = null;
            CurrentSnapshotDirectoryPath = null;
        }

        /// <summary>
        /// Copies one directory tree recursively into another directory.
        /// </summary>
        /// <param name="sourceDirectoryPath">Directory that should be copied.</param>
        /// <param name="destinationDirectoryPath">Destination directory that should receive the copied files.</param>
        void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath) {
            Directory.CreateDirectory(destinationDirectoryPath);

            string[] files = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++) {
                string sourceFilePath = files[index];
                string relativeFilePath = Path.GetRelativePath(sourceDirectoryPath, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationDirectoryPath, relativeFilePath);
                string destinationFileDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationFileDirectoryPath)) {
                    Directory.CreateDirectory(destinationFileDirectoryPath);
                }

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }

        /// <summary>
        /// Deletes one directory tree when it exists.
        /// </summary>
        /// <param name="directoryPath">Directory path to delete.</param>
        void DeleteDirectoryIfPresent(string directoryPath) {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath)) {
                Directory.Delete(directoryPath, true);
            }
        }

        /// <summary>
        /// Initiates unload for one collectible context and returns a weak reference that can be polled later.
        /// </summary>
        /// <param name="loadContext">Collectible context to unload.</param>
        /// <returns>Weak reference that can be observed until the context is collected.</returns>
        WeakReference BeginUnload(EditorCollectibleScriptAssemblyLoadContext loadContext) {
            if (loadContext == null) {
                return null;
            }

            WeakReference loadContextReference = new WeakReference(loadContext);
            loadContext.Unload();
            return loadContextReference;
        }

        /// <summary>
        /// Forces garbage collection until one collectible context disappears or the retry limit is reached.
        /// </summary>
        /// <param name="loadContextReference">Weak reference returned by <see cref="BeginUnload(EditorCollectibleScriptAssemblyLoadContext)"/>.</param>
        void WaitForUnload(WeakReference loadContextReference) {
            if (loadContextReference == null) {
                return;
            }

            for (int attempt = 0; attempt < 12 && loadContextReference.IsAlive; attempt++) {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            if (loadContextReference.IsAlive) {
                throw new InvalidOperationException("The previous scripting assembly could not be unloaded.");
            }
        }
    }
}
