using System.Security.Cryptography;

namespace helengine.editor {
    /// <summary>
    /// Resolves stable native build roots and mirrors generated C++ inputs without changing timestamps for identical files.
    /// </summary>
    internal static class EditorNativeBuildCache {
        /// <summary>
        /// Resolves a project-local cache root for one platform and build profile.
        /// </summary>
        /// <param name="projectRootPath">Authored project root that owns the ignored cache directory.</param>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <param name="buildProfileId">Selected build profile.</param>
        /// <returns>The stable platform/profile cache slice.</returns>
        internal static string ResolveCacheRootPath(string projectRootPath, string platformId, string buildProfileId) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            ValidatePathSegment(platformId, nameof(platformId));
            ValidatePathSegment(buildProfileId, nameof(buildProfileId));
            return Path.Combine(Path.GetFullPath(projectRootPath), "cache", "build", platformId, buildProfileId);
        }

        /// <summary>
        /// Mirrors a completed generated-core tree while preserving unchanged builder outputs against an editor-source baseline.
        /// </summary>
        /// <param name="sourceRootPath">Fresh generated-core tree for the current build.</param>
        /// <param name="cacheRootPath">Stable generated-core tree consumed by the native builder.</param>
        /// <param name="builderOwnedRelativePaths">Relative builder-owned paths preserved when their editor source is absent or unchanged.</param>
        /// <param name="editorSourceBaselineRootPath">Optional separate mirror used to detect changes to builder-owned editor inputs.</param>
        internal static void SyncGeneratedCore(
            string sourceRootPath,
            string cacheRootPath,
            IReadOnlyCollection<string> builderOwnedRelativePaths = null,
            string editorSourceBaselineRootPath = "") {
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                throw new ArgumentException("Generated-core source root must be provided.", nameof(sourceRootPath));
            }
            if (string.IsNullOrWhiteSpace(cacheRootPath)) {
                throw new ArgumentException("Generated-core cache root must be provided.", nameof(cacheRootPath));
            }

            string sourceRoot = Canonicalize(sourceRootPath);
            string cacheRoot = Canonicalize(cacheRootPath);
            string editorSourceBaselineRoot = string.IsNullOrWhiteSpace(editorSourceBaselineRootPath)
                ? string.Empty
                : Canonicalize(editorSourceBaselineRootPath);
            if (!Directory.Exists(sourceRoot)) {
                throw new DirectoryNotFoundException($"Generated-core source root '{sourceRoot}' was not found.");
            }
            if (IsSameOrDescendant(sourceRoot, cacheRoot) || IsSameOrDescendant(cacheRoot, sourceRoot)) {
                throw new ArgumentException("Generated-core source and cache roots must not overlap.", nameof(cacheRootPath));
            }
            if (!string.IsNullOrEmpty(editorSourceBaselineRoot)
                && (IsSameOrDescendant(sourceRoot, editorSourceBaselineRoot)
                    || IsSameOrDescendant(editorSourceBaselineRoot, sourceRoot)
                    || IsSameOrDescendant(cacheRoot, editorSourceBaselineRoot)
                    || IsSameOrDescendant(editorSourceBaselineRoot, cacheRoot))) {
                throw new ArgumentException("Generated-core source baseline, source, and native cache roots must be separate.", nameof(editorSourceBaselineRootPath));
            }

            HashSet<string> ownedPaths = new(StringComparer.OrdinalIgnoreCase);
            if (builderOwnedRelativePaths != null) {
                foreach (string relativePath in builderOwnedRelativePaths) {
                    if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) {
                        throw new ArgumentException("Builder-owned generated paths must be non-empty relative paths.", nameof(builderOwnedRelativePaths));
                    }
                    ownedPaths.Add(Path.GetRelativePath(".", relativePath));
                }
            }

            HashSet<string> changedEditorSourcePaths = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(editorSourceBaselineRoot)) {
                foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)) {
                    string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
                    if (!FileContentsMatch(sourcePath, Path.Combine(editorSourceBaselineRoot, relativePath))) {
                        changedEditorSourcePaths.Add(relativePath);
                    }
                }
                SyncGeneratedCore(sourceRoot, editorSourceBaselineRoot);
            }

            Directory.CreateDirectory(cacheRoot);
            HashSet<string> currentRelativePaths = new(StringComparer.OrdinalIgnoreCase);
            foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)) {
                string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
                currentRelativePaths.Add(relativePath);
                string cachePath = Path.Combine(cacheRoot, relativePath);
                if (ownedPaths.Contains(relativePath)
                    && !changedEditorSourcePaths.Contains(relativePath)
                    && File.Exists(cachePath)) {
                    continue;
                }
                if (FileContentsMatch(sourcePath, cachePath)) {
                    continue;
                }

                string cacheDirectoryPath = Path.GetDirectoryName(cachePath);
                if (string.IsNullOrWhiteSpace(cacheDirectoryPath)) {
                    throw new InvalidOperationException($"Could not resolve a parent directory for '{cachePath}'.");
                }
                Directory.CreateDirectory(cacheDirectoryPath);
                File.Copy(sourcePath, cachePath, true);
            }

            foreach (string cachePath in Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories)) {
                string relativePath = Path.GetRelativePath(cacheRoot, cachePath);
                if (!currentRelativePaths.Contains(relativePath) && !ownedPaths.Contains(relativePath)) {
                    File.Delete(cachePath);
                }
            }

            foreach (string directoryPath in Directory.EnumerateDirectories(cacheRoot, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length)) {
                if (!Directory.EnumerateFileSystemEntries(directoryPath).Any()) {
                    Directory.Delete(directoryPath);
                }
            }
        }

        /// <summary>
        /// Validates one profile or platform name as a safe single directory segment.
        /// </summary>
        /// <param name="value">Untrusted path segment.</param>
        /// <param name="parameterName">Argument name used for validation errors.</param>
        static void ValidatePathSegment(string value, string parameterName) {
            if (string.IsNullOrWhiteSpace(value)
                || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_')) {
                throw new ArgumentException("Path segment must contain only letters, digits, hyphens, or underscores.", parameterName);
            }
        }

        /// <summary>
        /// Canonicalizes a path while removing trailing separators except for filesystem roots.
        /// </summary>
        /// <param name="path">Path to canonicalize.</param>
        /// <returns>Absolute canonical path.</returns>
        static string Canonicalize(string path) {
            string fullPath = Path.GetFullPath(path);
            string rootPath = Path.GetPathRoot(fullPath);
            return fullPath.Length <= rootPath.Length ? rootPath : Path.TrimEndingDirectorySeparator(fullPath);
        }

        /// <summary>
        /// Checks whether a canonical path equals or sits below another canonical path.
        /// </summary>
        /// <param name="rootPath">Potential ancestor path.</param>
        /// <param name="candidatePath">Potential descendant path.</param>
        /// <returns>True when the paths overlap in the requested direction.</returns>
        static bool IsSameOrDescendant(string rootPath, string candidatePath) {
            return string.Equals(rootPath, candidatePath, StringComparison.OrdinalIgnoreCase)
                || candidatePath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compares file contents without loading generated translation units into memory.
        /// </summary>
        /// <param name="sourcePath">Freshly generated source file.</param>
        /// <param name="cachePath">Previously cached source file.</param>
        /// <returns>True when cached bytes match the newly generated bytes.</returns>
        static bool FileContentsMatch(string sourcePath, string cachePath) {
            if (!File.Exists(cachePath)) {
                return false;
            }
            FileInfo sourceInfo = new(sourcePath);
            FileInfo cacheInfo = new(cachePath);
            if (sourceInfo.Length != cacheInfo.Length) {
                return false;
            }
            using FileStream sourceStream = File.OpenRead(sourcePath);
            using FileStream cacheStream = File.OpenRead(cachePath);
            return SHA256.HashData(sourceStream).AsSpan().SequenceEqual(SHA256.HashData(cacheStream));
        }
    }
}
