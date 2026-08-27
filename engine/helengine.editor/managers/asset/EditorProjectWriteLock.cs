namespace helengine.editor {
    /// <summary>
    /// Coordinates one project-wide native authoring publication across sessions and processes.
    /// </summary>
    internal sealed class EditorProjectWriteLock : IDisposable {
        const int MaximumAttempts = 200;
        const int RetryDelayMilliseconds = 10;
        readonly FileStream LockStream;
        bool IsDisposed;

        EditorProjectWriteLock(FileStream lockStream) {
            LockStream = lockStream;
        }

        /// <summary>
        /// Acquires an exclusive handle for the project's authoring publication boundary.
        /// </summary>
        /// <param name="projectRootPath">Project root to coordinate.</param>
        /// <returns>An exclusive project lock.</returns>
        public static EditorProjectWriteLock Acquire(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string lockPath = Path.Combine(fullProjectRootPath, "cache", "editor", "authoring-write.lock");
            string lockDirectoryPath = Path.GetDirectoryName(lockPath);
            Directory.CreateDirectory(lockDirectoryPath);
            IOException lastIOException = null;
            for (int attempt = 0; attempt < MaximumAttempts; attempt++) {
                try {
                    FileStream lockStream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.SequentialScan);
                    return new EditorProjectWriteLock(lockStream);
                } catch (IOException exception) {
                    lastIOException = exception;
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }

            throw new IOException($"Could not acquire the project authoring write lock at '{lockPath}'.", lastIOException);
        }

        /// <summary>
        /// Releases the exclusive project lock.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;
            LockStream.Dispose();
        }
    }

    /// <summary>
    /// Stores one generation marker for project-scoped authoring publication.
    /// </summary>
    internal static class EditorProjectWriteGeneration {
        /// <summary>
        /// Reads the last published generation, or an empty value when none exists.
        /// </summary>
        /// <param name="projectRootPath">Project root to inspect.</param>
        /// <returns>Current generation marker.</returns>
        public static string Read(string projectRootPath) {
            string generationPath = GetPath(projectRootPath);
            if (!File.Exists(generationPath)) {
                return string.Empty;
            }

            try {
                return File.ReadAllText(generationPath).Trim();
            } catch (FileNotFoundException) {
                return string.Empty;
            } catch (DirectoryNotFoundException) {
                return string.Empty;
            }
        }

        /// <summary>
        /// Atomically publishes a new generation marker.
        /// </summary>
        /// <param name="projectRootPath">Project root to update.</param>
        /// <returns>New generation marker.</returns>
        public static string Advance(string projectRootPath) {
            string generationPath = GetPath(projectRootPath);
            string generationDirectoryPath = Path.GetDirectoryName(generationPath);
            Directory.CreateDirectory(generationDirectoryPath);
            string generation = Guid.NewGuid().ToString("N");
            string temporaryPath = generationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temporaryPath, generation, new System.Text.UTF8Encoding(false));
                File.Move(temporaryPath, generationPath, true);
                return generation;
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Gets the generation marker path for one project.
        /// </summary>
        /// <param name="projectRootPath">Project root to inspect.</param>
        /// <returns>Absolute generation marker path.</returns>
        static string GetPath(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            return Path.Combine(Path.GetFullPath(projectRootPath), "cache", "editor", "authoring-write.generation");
        }
    }
}
