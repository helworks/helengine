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
        /// Computes one fingerprint per module, so a module is only considered stale when something it
        /// actually consumes changed. A runtime module never sees the editor assemblies.
        /// </summary>
        /// <param name="moduleInputs">Build inputs keyed by module id.</param>
        /// <returns>Fingerprints keyed by module id.</returns>
        public static Dictionary<string, string> ComputeAll(IReadOnlyList<KeyValuePair<string, EditorScriptBuildInputs>> moduleInputs) {
            if (moduleInputs == null) {
                throw new ArgumentNullException(nameof(moduleInputs));
            }

            Dictionary<string, string> fingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < moduleInputs.Count; index++) {
                fingerprints[moduleInputs[index].Key] = Compute(moduleInputs[index].Value);
            }

            return fingerprints;
        }

        /// <summary>
        /// Reads the stored per-module fingerprints. A missing or unreadable file yields an empty map, which
        /// simply means every module is treated as stale.
        /// </summary>
        /// <param name="filePath">Absolute path of the stored fingerprint file.</param>
        /// <returns>Fingerprints keyed by module id.</returns>
        public static Dictionary<string, string> ReadStoredModules(string filePath) {
            Dictionary<string, string> fingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return fingerprints;
            }

            string[] lines;
            try {
                lines = File.ReadAllLines(filePath, Encoding.UTF8);
            } catch (IOException) {
                return fingerprints;
            } catch (UnauthorizedAccessException) {
                return fingerprints;
            }

            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index].Trim();
                if (line.Length == 0) {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == line.Length - 1) {
                    // An unrecognized line means an older or damaged file; treat the whole thing as absent.
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                fingerprints[line.Substring(0, separatorIndex)] = line.Substring(separatorIndex + 1);
            }

            return fingerprints;
        }

        /// <summary>
        /// Stores the per-module fingerprints of a build that just succeeded.
        /// </summary>
        /// <param name="filePath">Absolute path of the stored fingerprint file.</param>
        /// <param name="fingerprints">Fingerprints keyed by module id.</param>
        public static void WriteStoredModules(string filePath, IReadOnlyDictionary<string, string> fingerprints) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Fingerprint file path must be provided.", nameof(filePath));
            }
            if (fingerprints == null) {
                throw new ArgumentNullException(nameof(fingerprints));
            }

            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in fingerprints.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                builder.Append(entry.Key).Append('=').Append(entry.Value).Append('\n');
            }

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Determines whether every module's current fingerprint matches what was stored.
        /// </summary>
        /// <param name="stored">Fingerprints recorded by the last successful build.</param>
        /// <param name="current">Fingerprints computed from the current inputs.</param>
        /// <returns>True when both sets describe exactly the same modules with the same fingerprints.</returns>
        public static bool AllModulesMatch(IReadOnlyDictionary<string, string> stored, IReadOnlyDictionary<string, string> current) {
            if (stored == null) {
                throw new ArgumentNullException(nameof(stored));
            }
            if (current == null) {
                throw new ArgumentNullException(nameof(current));
            }
            if (current.Count == 0 || stored.Count != current.Count) {
                return false;
            }

            foreach (KeyValuePair<string, string> entry in current) {
                if (!stored.TryGetValue(entry.Key, out string storedFingerprint)
                    || !string.Equals(storedFingerprint, entry.Value, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
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
