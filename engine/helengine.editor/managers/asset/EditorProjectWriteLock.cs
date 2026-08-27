using System.Text.Json;

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
            return EditorProjectWriteGeneration.PublishChangeUnderLock(ProjectRootPath, relativePath);
        }
    }

    /// <summary>
    /// Stores ordered exact-path records for project-scoped authoring publication.
    /// </summary>
    internal static class EditorProjectWriteGeneration {
        const int CurrentVersion = 1;
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <summary>
        /// Reads the current generation from the strict project snapshot, or zero when no snapshot exists.
        /// </summary>
        public static long Read(string projectRootPath) {
            return ReadSnapshot(projectRootPath).CurrentGeneration;
        }

        /// <summary>
        /// Reads latest-per-path changes after one observed generation.
        /// </summary>
        public static IReadOnlyList<EditorProjectWriteChange> ReadAfter(string projectRootPath, long generation) {
            if (generation < 0) {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            return snapshot.Changes
                .Where(change => change.Generation > generation)
                .OrderBy(change => change.Generation)
                .ThenBy(change => change.RelativePath, PathComparer)
                .Select(change => new EditorProjectWriteChange(change.Generation, change.RelativePath))
                .ToArray();
        }

        /// <summary>
        /// Durably publishes one exact normalized path by atomically replacing the generation snapshot.
        /// The caller holds the project write lock for the full read-modify-write boundary.
        /// </summary>
        public static long PublishChange(string projectRootPath, string relativePath) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            return PublishChangeUnderLock(projectRootPath, relativePath);
        }

        /// <summary>
        /// Publishes one exact path while the caller owns the project publication lock.
        /// </summary>
        internal static long PublishChangeUnderLock(string projectRootPath, string relativePath) {
            string normalizedRelativePath = NormalizeRelativePath(projectRootPath, relativePath);
            string generationPath = GetPath(projectRootPath);
            string generationDirectoryPath = Path.GetDirectoryName(generationPath);
            Directory.CreateDirectory(generationDirectoryPath);

            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            long generation = checked(snapshot.CurrentGeneration + 1);
            Dictionary<string, ProjectWriteGenerationChange> changes = snapshot.Changes
                .ToDictionary(change => change.RelativePath, change => change, PathComparer);
            changes[normalizedRelativePath] = new ProjectWriteGenerationChange {
                Generation = generation,
                RelativePath = normalizedRelativePath
            };

            ProjectWriteGenerationSnapshot nextSnapshot = new ProjectWriteGenerationSnapshot {
                Version = CurrentVersion,
                CurrentGeneration = generation,
                Changes = changes.Values
                    .OrderBy(change => change.Generation)
                    .ThenBy(change => change.RelativePath, PathComparer)
                    .ToList()
            };
            WriteSnapshotAtomically(generationPath, nextSnapshot);
            return generation;
        }

        static ProjectWriteGenerationSnapshot ReadSnapshot(string projectRootPath) {
            string generationPath = GetPath(projectRootPath);
            if (!File.Exists(generationPath)) {
                return new ProjectWriteGenerationSnapshot {
                    Version = CurrentVersion,
                    CurrentGeneration = 0,
                    Changes = new List<ProjectWriteGenerationChange>()
                };
            }

            string json;
            try {
                json = File.ReadAllText(generationPath);
            } catch (FileNotFoundException) {
                return new ProjectWriteGenerationSnapshot {
                    Version = CurrentVersion,
                    CurrentGeneration = 0,
                    Changes = new List<ProjectWriteGenerationChange>()
                };
            } catch (DirectoryNotFoundException) {
                return new ProjectWriteGenerationSnapshot {
                    Version = CurrentVersion,
                    CurrentGeneration = 0,
                    Changes = new List<ProjectWriteGenerationChange>()
                };
            }

            ProjectWriteGenerationSnapshot snapshot;
            try {
                snapshot = JsonSerializer.Deserialize<ProjectWriteGenerationSnapshot>(json, JsonOptions);
            } catch (JsonException exception) {
                throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' is malformed.", exception);
            }

            ValidateSnapshot(snapshot, generationPath, Path.GetFullPath(projectRootPath));
            return snapshot;
        }

        static void ValidateSnapshot(ProjectWriteGenerationSnapshot snapshot, string generationPath, string projectRootPath) {
            if (snapshot == null || snapshot.Version != CurrentVersion || snapshot.CurrentGeneration < 0 || snapshot.Changes == null) {
                throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' has an unsupported version or shape.");
            }

            HashSet<string> paths = new HashSet<string>(PathComparer);
            HashSet<long> generations = new HashSet<long>();
            long maximumGeneration = 0;
            for (int index = 0; index < snapshot.Changes.Count; index++) {
                ProjectWriteGenerationChange change = snapshot.Changes[index];
                if (change == null || string.IsNullOrWhiteSpace(change.RelativePath) || change.Generation <= 0 || change.Generation > snapshot.CurrentGeneration ||
                    !generations.Add(change.Generation) || !paths.Add(change.RelativePath)) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid or duplicate change.");
                }

                string normalizedPath;
                try {
                    normalizedPath = NormalizeRelativePath(projectRootPath, change.RelativePath);
                } catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid path.", exception);
                }
                if (!string.Equals(normalizedPath, change.RelativePath, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains a non-canonical path.");
                }

                maximumGeneration = Math.Max(maximumGeneration, change.Generation);
            }

            if ((snapshot.CurrentGeneration == 0 && snapshot.Changes.Count != 0) ||
                (snapshot.CurrentGeneration > 0 && maximumGeneration != snapshot.CurrentGeneration)) {
                throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' has inconsistent generation bounds.");
            }
        }

        static void WriteSnapshotAtomically(string generationPath, ProjectWriteGenerationSnapshot snapshot) {
            string temporaryPath = generationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
                using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(generationPath)) {
                    File.Move(temporaryPath, generationPath, true);
                } else {
                    File.Move(temporaryPath, generationPath);
                }
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

        sealed class ProjectWriteGenerationSnapshot {
            public int Version { get; set; }

            public long CurrentGeneration { get; set; }

            public List<ProjectWriteGenerationChange> Changes { get; set; }
        }

        sealed class ProjectWriteGenerationChange {
            public long Generation { get; set; }

            public string RelativePath { get; set; }
        }
    }
}
