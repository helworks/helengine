namespace helengine {
    /// <summary>
    /// Opens runtime content streams from one host filesystem root while preserving virtual rooted path conventions used by platform runtimes.
    /// </summary>
    public sealed class HostFileSystemContentStreamSource : IContentStreamSource {
        /// <summary>
        /// Stores the normalized host or virtual content root.
        /// </summary>
        readonly string RootPath;

        /// <summary>
        /// Initializes one host-filesystem-backed content stream source.
        /// </summary>
        /// <param name="rootPath">Directory or virtual root used to resolve relative content paths.</param>
        public HostFileSystemContentStreamSource(string rootPath) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }

            RootPath = HasVirtualRootPrefix(rootPath)
                ? rootPath
                : Path.GetFullPath(rootPath);
        }

        /// <summary>
        /// Gets the normalized root used to resolve relative content paths.
        /// </summary>
        public string RootDirectory => RootPath;

        /// <summary>
        /// Opens one readable filesystem stream for the supplied asset path.
        /// </summary>
        /// <param name="assetPath">Relative, absolute, or virtual-rooted asset path.</param>
        /// <returns>Readable stream for the resolved asset path.</returns>
        public Stream OpenRead(string assetPath) {
            string fullPath = ResolveContentPath(assetPath);
            return File.OpenRead(fullPath);
        }

        /// <summary>
        /// Resolves one relative or absolute content path to the final path that should be opened by the host filesystem.
        /// </summary>
        /// <param name="assetPath">Relative, absolute, or virtual-rooted asset path.</param>
        /// <returns>Resolved path to open.</returns>
        string ResolveContentPath(string assetPath) {
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }

            if (HasVirtualRootPrefix(assetPath)) {
                return assetPath;
            }

            if (Path.IsPathRooted(assetPath)) {
                return Path.GetFullPath(assetPath);
            }

            if (HasVirtualRootPrefix(RootPath)) {
                return CombineVirtualRootedPath(RootPath, assetPath);
            }

            return Path.GetFullPath(Path.Combine(RootPath, assetPath));
        }

        /// <summary>
        /// Combines one relative path beneath a virtual rooted content prefix such as <c>dvd:/</c>.
        /// </summary>
        /// <param name="rootPath">Virtual rooted content prefix.</param>
        /// <param name="relativePath">Relative path to append beneath the prefix.</param>
        /// <returns>Combined virtual rooted path.</returns>
        static string CombineVirtualRootedPath(string rootPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return EnsureTrailingDirectorySeparator(rootPath) + TrimLeadingDirectorySeparators(relativePath);
        }

        /// <summary>
        /// Returns whether one path uses a virtual platform root such as <c>dvd:/...</c> that must be preserved verbatim.
        /// </summary>
        /// <param name="path">Path text to inspect.</param>
        /// <returns>True when the path uses a non-drive virtual root prefix; otherwise false.</returns>
        static bool HasVirtualRootPrefix(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            int colonIndex = -1;
            for (int index = 0; index < path.Length; index++) {
                if (path[index] == ':') {
                    colonIndex = index;
                    break;
                }
            }

            if (colonIndex <= 0) {
                return false;
            } else if (colonIndex == 1) {
                return false;
            } else if (colonIndex >= path.Length - 1) {
                return false;
            }

            char nextCharacter = path[colonIndex + 1];
            return nextCharacter == Path.DirectorySeparatorChar || nextCharacter == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Ensures one directory path ends with a trailing separator before prefix combinations occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        static string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Trims any leading directory separators from one relative path before it is combined beneath a virtual root.
        /// </summary>
        /// <param name="path">Relative path whose leading separators should be removed.</param>
        /// <returns>Relative path without leading separators.</returns>
        static string TrimLeadingDirectorySeparators(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            int startIndex = 0;
            while (startIndex < path.Length
                && (path[startIndex] == Path.DirectorySeparatorChar || path[startIndex] == Path.AltDirectorySeparatorChar)) {
                startIndex++;
            }

            if (startIndex == 0) {
                return path;
            }
            if (startIndex >= path.Length) {
                return string.Empty;
            }

            return path.Substring(startIndex);
        }
    }
}
