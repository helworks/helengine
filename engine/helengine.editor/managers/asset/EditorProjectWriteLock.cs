namespace helengine.editor {
    /// <summary>
    /// Coordinates one project-wide native authoring publication across sessions and processes.
    /// </summary>
    internal sealed class EditorProjectWriteLock : IDisposable {
        static readonly TimeSpan DefaultMaximumWait = TimeSpan.FromSeconds(60);
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
            return Acquire(projectRootPath, DefaultMaximumWait);
        }

        /// <summary>
        /// Acquires the project lock using a bounded wait supplied by the caller.
        /// </summary>
        /// <param name="projectRootPath">Project root to coordinate.</param>
        /// <param name="maximumWait">Maximum time to wait for another writer.</param>
        /// <returns>An exclusive project lock.</returns>
        internal static EditorProjectWriteLock Acquire(string projectRootPath, TimeSpan maximumWait) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (maximumWait <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(maximumWait));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string lockPath = Path.Combine(fullProjectRootPath, "cache", "editor", "authoring-write.lock");
            string lockDirectoryPath = Path.GetDirectoryName(lockPath);
            Directory.CreateDirectory(lockDirectoryPath);
            IOException lastIOException = null;
            DateTime deadlineUtc = DateTime.UtcNow + maximumWait;
            while (DateTime.UtcNow <= deadlineUtc) {
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
    /// Describes one ordered, exact-path authoring publication.
    /// </summary>
    internal sealed class EditorProjectWriteChange {
        public EditorProjectWriteChange(long generation, string relativePath) {
            Generation = generation;
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        }

        public long Generation { get; }

        public string RelativePath { get; }
    }

    /// <summary>
    /// Publishes and reads project-scoped exact-path changes.
    /// </summary>
    internal interface IEditorProjectWriteChangeLog {
        long CurrentGeneration { get; }

        IReadOnlyList<EditorProjectWriteChange> ReadAfter(long generation);

        long PublishChange(string relativePath);
    }

    /// <summary>
    /// File-backed exact-path change log used by project authoring sessions.
    /// </summary>
    internal sealed class FileEditorProjectWriteChangeLog : IEditorProjectWriteChangeLog {
        readonly string ProjectRootPath;

        public FileEditorProjectWriteChangeLog(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        public long CurrentGeneration => EditorProjectWriteGeneration.Read(ProjectRootPath);

        public IReadOnlyList<EditorProjectWriteChange> ReadAfter(long generation) {
            return EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, generation);
        }

        public long PublishChange(string relativePath) {
            return EditorProjectWriteGeneration.PublishChange(ProjectRootPath, relativePath);
        }
    }

    /// <summary>
    /// Stores ordered exact-path records for project-scoped authoring publication.
    /// </summary>
    internal static class EditorProjectWriteGeneration {
        /// <summary>
        /// Reads the latest ordered generation, or zero when no record exists.
        /// </summary>
        /// <param name="projectRootPath">Project root to inspect.</param>
        /// <returns>Current generation.</returns>
        public static long Read(string projectRootPath) {
            string generationPath = GetPath(projectRootPath);
            if (!File.Exists(generationPath)) {
                return 0;
            }

            try {
                long generation = 0;
                foreach (string line in File.ReadLines(generationPath)) {
                    string[] fields = line.Split('\t', 2);
                    long value;
                    if (fields.Length == 2 && long.TryParse(fields[0], out value) && value > generation) {
                        generation = value;
                    }
                }
                return generation;
            } catch (FileNotFoundException) {
                return 0;
            } catch (DirectoryNotFoundException) {
                return 0;
            }
        }

        /// <summary>
        /// Reads exact path changes after one observed generation.
        /// </summary>
        /// <param name="projectRootPath">Project root to inspect.</param>
        /// <param name="generation">Last observed generation.</param>
        /// <returns>Ordered exact-path changes.</returns>
        public static IReadOnlyList<EditorProjectWriteChange> ReadAfter(string projectRootPath, long generation) {
            string generationPath = GetPath(projectRootPath);
            if (!File.Exists(generationPath)) {
                return Array.Empty<EditorProjectWriteChange>();
            }

            List<EditorProjectWriteChange> changes = new List<EditorProjectWriteChange>();
            try {
                foreach (string line in File.ReadLines(generationPath)) {
                    string[] fields = line.Split('\t', 2);
                    long value;
                    if (fields.Length != 2 || !long.TryParse(fields[0], out value) || value <= generation) {
                        continue;
                    }

                    string relativePath = fields[1].Replace('\\', '/').Trim('/');
                    if (!string.IsNullOrWhiteSpace(relativePath)) {
                        changes.Add(new EditorProjectWriteChange(value, relativePath));
                    }
                }
            } catch (FileNotFoundException) {
                return Array.Empty<EditorProjectWriteChange>();
            } catch (DirectoryNotFoundException) {
                return Array.Empty<EditorProjectWriteChange>();
            }

            return changes.OrderBy(change => change.Generation).ToList();
        }

        /// <summary>
        /// Durably appends one exact normalized path and its next generation.
        /// </summary>
        /// <param name="projectRootPath">Project root to update.</param>
        /// <param name="relativePath">Assets-relative changed path.</param>
        /// <returns>New ordered generation.</returns>
        public static long PublishChange(string projectRootPath, string relativePath) {
            string normalizedRelativePath = NormalizeRelativePath(projectRootPath, relativePath);
            string generationPath = GetPath(projectRootPath);
            string generationDirectoryPath = Path.GetDirectoryName(generationPath);
            Directory.CreateDirectory(generationDirectoryPath);

            long generation = Read(projectRootPath) + 1;
            byte[] record = new System.Text.UTF8Encoding(false).GetBytes(
                generation.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + normalizedRelativePath + Environment.NewLine);
            using FileStream stream = new FileStream(
                generationPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                record.Length,
                FileOptions.WriteThrough);
            stream.Write(record, 0, record.Length);
            stream.Flush(true);
            return generation;
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

        /// <summary>
        /// Validates and normalizes one path beneath the project assets root.
        /// </summary>
        static string NormalizeRelativePath(string projectRootPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
                relativePath.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0) {
                throw new ArgumentException("Changed path must be a non-rooted assets-relative file path.", nameof(relativePath));
            }

            string assetsRootPath = Path.GetFullPath(Path.Combine(Path.GetFullPath(projectRootPath), "assets"));
            string fullPath = Path.GetFullPath(Path.Combine(assetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = assetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison)) {
                throw new InvalidOperationException("Changed path must remain beneath the project assets root.");
            }

            return Path.GetRelativePath(assetsRootPath, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Trim('/');
        }
    }
}
