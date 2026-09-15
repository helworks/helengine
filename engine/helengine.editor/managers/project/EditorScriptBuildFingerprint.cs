using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Computes and stores a fingerprint of the script build inputs so unchanged projects can skip the compiler.
    /// </summary>
    public static class EditorScriptBuildFingerprint {
        /// <summary>
        /// File name of the stored fingerprint inside the generated metadata directory.
        /// </summary>
        public const string FileName = "script-build.fingerprint";

        /// <summary>
        /// Search pattern for compiled source files.
        /// </summary>
        const string SourceFilePattern = "*.cs";

        /// <summary>
        /// Computes a stable hash over the supplied inputs.
        /// </summary>
        /// <param name="inputs">Build inputs to fingerprint.</param>
        /// <returns>Lowercase hexadecimal fingerprint.</returns>
        public static string Compute(EditorScriptBuildInputs inputs) {
            if (inputs == null) {
                throw new ArgumentNullException(nameof(inputs));
            }

            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            string[] generatedFilePaths = SortPaths(inputs.GeneratedFilePaths);
            for (int index = 0; index < generatedFilePaths.Length; index++) {
                AppendLine(hash, "gen|" + generatedFilePaths[index]);
                if (File.Exists(generatedFilePaths[index])) {
                    hash.AppendData(File.ReadAllBytes(generatedFilePaths[index]));
                } else {
                    AppendLine(hash, "missing");
                }
            }

            string[] excludedDirectoryPrefixes = BuildDirectoryPrefixes(inputs.ExcludedSourceDirectoryPaths);
            string[] sourceDirectoryPaths = SortPaths(inputs.SourceDirectoryPaths);
            for (int index = 0; index < sourceDirectoryPaths.Length; index++) {
                AppendLine(hash, "dir|" + sourceDirectoryPaths[index]);
                if (!Directory.Exists(sourceDirectoryPaths[index])) {
                    AppendLine(hash, "missing");
                    continue;
                }

                string[] sourceFilePaths = Directory.GetFiles(sourceDirectoryPaths[index], SourceFilePattern, SearchOption.AllDirectories);
                Array.Sort(sourceFilePaths, StringComparer.Ordinal);
                for (int fileIndex = 0; fileIndex < sourceFilePaths.Length; fileIndex++) {
                    if (IsUnderAny(sourceFilePaths[fileIndex], excludedDirectoryPrefixes)) {
                        continue;
                    }

                    AppendFileStamp(hash, "src", sourceFilePaths[fileIndex]);
                }
            }

            string[] referencedAssemblyPaths = SortPaths(inputs.ReferencedAssemblyPaths);
            for (int index = 0; index < referencedAssemblyPaths.Length; index++) {
                AppendFileStamp(hash, "ref", referencedAssemblyPaths[index]);
            }

            for (int index = 0; index < inputs.Tokens.Count; index++) {
                AppendLine(hash, "tok|" + inputs.Tokens[index]);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        /// <summary>
        /// Determines whether the stored fingerprint file holds the supplied value.
        /// </summary>
        /// <param name="filePath">Absolute path of the stored fingerprint file.</param>
        /// <param name="fingerprint">Fingerprint computed from the current inputs.</param>
        /// <returns>True when the file exists and its trimmed contents equal the fingerprint.</returns>
        public static bool MatchesStored(string filePath, string fingerprint) {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(fingerprint) || !File.Exists(filePath)) {
                return false;
            }

            try {
                return string.Equals(File.ReadAllText(filePath, Encoding.UTF8).Trim(), fingerprint, StringComparison.Ordinal);
            } catch (IOException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }
        }

        /// <summary>
        /// Stores the fingerprint of a build that just succeeded.
        /// </summary>
        /// <param name="filePath">Absolute path of the stored fingerprint file.</param>
        /// <param name="fingerprint">Fingerprint computed from the inputs that were built.</param>
        public static void WriteStored(string filePath, string fingerprint) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Fingerprint file path must be provided.", nameof(filePath));
            }
            if (string.IsNullOrWhiteSpace(fingerprint)) {
                throw new ArgumentException("Fingerprint must be provided.", nameof(fingerprint));
            }

            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(filePath, fingerprint + Environment.NewLine, Encoding.UTF8);
        }

        /// <summary>
        /// Appends one file's identity, size, and last-write time; missing files are recorded as such.
        /// </summary>
        static void AppendFileStamp(IncrementalHash hash, string kind, string filePath) {
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) {
                AppendLine(hash, kind + "|" + filePath + "|missing");
                return;
            }

            AppendLine(hash, kind + "|" + filePath + "|" + fileInfo.Length + "|" + fileInfo.LastWriteTimeUtc.Ticks);
        }

        /// <summary>
        /// Appends one UTF-8 line to the running hash.
        /// </summary>
        static void AppendLine(IncrementalHash hash, string line) {
            hash.AppendData(Encoding.UTF8.GetBytes(line));
            hash.AppendData(new byte[] { (byte)'\n' });
        }

        /// <summary>
        /// Normalizes directories into full paths ending with a separator, for prefix matching.
        /// </summary>
        static string[] BuildDirectoryPrefixes(IReadOnlyList<string> directoryPaths) {
            string[] prefixes = new string[directoryPaths.Count];
            for (int index = 0; index < directoryPaths.Count; index++) {
                string fullPath = Path.GetFullPath(directoryPaths[index]).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                prefixes[index] = fullPath + Path.DirectorySeparatorChar;
            }

            return prefixes;
        }

        /// <summary>
        /// Determines whether a file lies under any of the supplied directory prefixes.
        /// </summary>
        static bool IsUnderAny(string filePath, string[] directoryPrefixes) {
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (int index = 0; index < directoryPrefixes.Length; index++) {
                if (filePath.StartsWith(directoryPrefixes[index], comparison)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Copies and sorts paths so input order never affects the fingerprint.
        /// </summary>
        static string[] SortPaths(IReadOnlyList<string> paths) {
            string[] sorted = new string[paths.Count];
            for (int index = 0; index < paths.Count; index++) {
                sorted[index] = Path.GetFullPath(paths[index]);
            }

            Array.Sort(sorted, StringComparer.Ordinal);
            return sorted;
        }
    }
}
