using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Resolves stable per-project build-isolation roots so concurrent platform builds do not share mutable output trees.
    /// </summary>
    internal sealed class EditorBuildIsolationPathResolver {
        /// <summary>
        /// Top-level temporary folder used to hold isolated build state.
        /// </summary>
        const string IsolationFolderName = "helengine-builds";

        /// <summary>
        /// Environment setting that allows build hosts to select a short visible isolation root.
        /// </summary>
        const string WorkspaceRootEnvironmentVariableName = "HELENGINE_BUILD_WORKSPACE_ROOT";

        /// <summary>
        /// Environment setting that enables deterministic headless build caches.
        /// </summary>
        internal const string CacheRootEnvironmentVariableName = "HELENGINE_BUILD_CACHE_ROOT";

        /// <summary>
        /// Environment setting that selects the deterministic build configuration segment.
        /// </summary>
        internal const string ConfigurationEnvironmentVariableName = "HELENGINE_BUILD_CONFIGURATION";

        /// <summary>
        /// Environment setting that selects the deterministic build-profile segment.
        /// </summary>
        internal const string ProfileEnvironmentVariableName = "HELENGINE_BUILD_PROFILE";

        /// <summary>
        /// Number of SHA-256 bytes retained in the stable project hash segment.
        /// </summary>
        const int ProjectHashByteCount = 16;

        /// <summary>
        /// Number of SHA-256 bytes retained in each default temporary invocation segment.
        /// </summary>
        const int InvocationHashByteCount = 8;

        /// <summary>
        /// Short marker used to identify the default temporary workspace branch.
        /// </summary>
        const string DefaultWorkspaceMarker = "w";

        /// <summary>
        /// Absolute authored project root path used to seed stable isolation roots.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Stable hash segment derived from the authored project root path.
        /// </summary>
        readonly string ProjectHashSegment;

        /// <summary>
        /// Wrapper-compatible project hash segment used only by deterministic headless cache mode.
        /// </summary>
        readonly string StableProjectHashSegment;

        /// <summary>
        /// Initializes one resolver for the supplied authored project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root path.</param>
        public EditorBuildIsolationPathResolver(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectHashSegment = ComputeProjectHashSegment(ProjectRootPath);
            StableProjectHashSegment = ComputeStableProjectHashSegment(ProjectRootPath);
        }

        /// <summary>
        /// Resolves the stable isolated root for one target platform.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <returns>Absolute isolated root path for the supplied platform.</returns>
        public string ResolvePlatformRootPath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(
                ResolveIsolationRootPath(),
                ProjectHashSegment,
                SanitizePathSegment(platformId));
        }

        /// <summary>
        /// Resolves the execution root used by one queued workspace run for the supplied platform.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <param name="queueItemId">Stable queued build item identifier.</param>
        /// <returns>Absolute isolated execution root path for the queued workspace run.</returns>
        public string ResolveWorkspaceExecutionRootPath(string platformId, string queueItemId) {
            if (string.IsNullOrWhiteSpace(queueItemId)) {
                throw new ArgumentException("Queue item id must be provided.", nameof(queueItemId));
            }

            if (UsesStableCacheRoot()) {
                return CombineStrictDescendantPath(ResolveStableProfileRootPath(platformId), "build-graph");
            }

            if (UsesConfiguredWorkspaceRoot()) {
                return Path.Combine(ResolveIsolationRootPath(), SanitizePathSegment(platformId), SanitizePathSegment(queueItemId));
            }

            return Path.Combine(
                ResolvePlatformRootPath(platformId),
                DefaultWorkspaceMarker,
                ComputeInvocationSegment(queueItemId));
        }

        /// <summary>
        /// Resolves a unique execution root for one invocation of a queued build item.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <param name="queueItemId">Stable queued build item identifier.</param>
        /// <param name="executionId">Unique identifier for this invocation of the queued build item.</param>
        /// <returns>Absolute isolated execution root path for this build invocation.</returns>
        public string ResolveWorkspaceExecutionRootPath(string platformId, string queueItemId, string executionId) {
            if (string.IsNullOrWhiteSpace(executionId)) {
                throw new ArgumentException("Execution id must be provided.", nameof(executionId));
            }

            if (UsesStableCacheRoot()) {
                return CombineStrictDescendantPath(ResolveStableProfileRootPath(platformId), "build-graph");
            }

            if (UsesConfiguredWorkspaceRoot()) {
                return Path.Combine(ResolveIsolationRootPath(), SanitizePathSegment(platformId), SanitizePathSegment(executionId));
            }

            return Path.Combine(
                ResolveWorkspaceExecutionRootPath(platformId, queueItemId),
                ComputeInvocationSegment(executionId));
        }

        /// <summary>
        /// Resolves the generated managed-code output root used by one headless platform build invocation.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <param name="executionId">Unique identifier for the build invocation.</param>
        /// <returns>Absolute invocation-isolated generated managed-code output root path.</returns>
        public string ResolveGeneratedCodeOutputRootPath(string platformId, string executionId) {
            if (UsesStableCacheRoot()) {
                return CombineStrictDescendantPath(ResolveStableProfileRootPath(platformId), "generated-dotnet");
            }

            return Path.Combine(ResolveWorkspaceExecutionRootPath(platformId, executionId), "generated-dotnet");
        }

        /// <summary>
        /// Resolves the generated solution workspace root used by one headless build or editor-command invocation.
        /// </summary>
        /// <param name="platformId">Stable build-route identifier used to isolate the invocation.</param>
        /// <param name="executionId">Unique identifier for the build or command invocation.</param>
        /// <returns>Absolute invocation-isolated generated solution workspace root path.</returns>
        public string ResolveGeneratedCodeWorkspaceRootPath(string platformId, string executionId) {
            return Path.Combine(ResolveGeneratedCodeOutputRootPath(platformId, executionId), "workspace");
        }

        /// <summary>
        /// Resolves the deterministic authored workspace that contains generated solution and project files.
        /// </summary>
        /// <returns>Absolute project-scoped generated-code workspace path.</returns>
        public string ResolveGeneratedCodeProjectWorkspaceRootPath() {
            return Path.Combine(ProjectRootPath, "user_settings", "generated_code");
        }

        /// <summary>
        /// Resolves a deterministic generated-code metadata root for one build route and script surface profile.
        /// </summary>
        /// <param name="routeId">Stable route identifier, such as an editor command or platform build id.</param>
        /// <param name="compilationMode">Script surface profile generated by the route.</param>
        /// <returns>Absolute route-scoped generated-code metadata root.</returns>
        public string ResolveGeneratedCodeProjectWorkspaceRootPath(string routeId, EditorScriptCompilationMode compilationMode) {
            if (string.IsNullOrWhiteSpace(routeId)) {
                throw new ArgumentException("Generated-code route id must be provided.", nameof(routeId));
            }
            if (!Enum.IsDefined(compilationMode)) {
                throw new ArgumentOutOfRangeException(nameof(compilationMode), compilationMode, "Unknown script compilation mode.");
            }

            return Path.Combine(
                ResolveGeneratedCodeProjectWorkspaceRootPath(),
                SanitizeStablePathSegment(routeId),
                SanitizeStablePathSegment(compilationMode.ToString()));
        }

        /// <summary>
        /// Resolves the deterministic fallback output root embedded in authored generated project metadata.
        /// </summary>
        /// <returns>Absolute project-scoped generated-code fallback output path.</returns>
        public string ResolveGeneratedCodeProjectOutputRootPath() {
            return Path.Combine(ResolveGeneratedCodeProjectWorkspaceRootPath(), "output");
        }

        /// <summary>
        /// Resolves the stable fallback output root for one route and script surface profile.
        /// </summary>
        /// <param name="routeId">Stable route identifier.</param>
        /// <param name="compilationMode">Script surface profile generated by the route.</param>
        /// <returns>Absolute route-scoped fallback output root.</returns>
        public string ResolveGeneratedCodeProjectOutputRootPath(string routeId, EditorScriptCompilationMode compilationMode) {
            return Path.Combine(ResolveGeneratedCodeProjectWorkspaceRootPath(routeId, compilationMode), "output");
        }

        /// <summary>
        /// Resolves the persistent generated-core root used by deterministic headless builds.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <returns>Absolute generated-core root beneath the selected stable profile.</returns>
        internal string ResolveGeneratedCoreRootPath(string platformId) {
            return CombineStrictDescendantPath(ResolveStableProfileRootPath(platformId), "generated-core");
        }

        /// <summary>
        /// Resolves the persistent native builder root used by deterministic headless builds.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <returns>Absolute native root beneath the selected stable profile.</returns>
        internal string ResolveNativeRootPath(string platformId) {
            return CombineStrictDescendantPath(ResolveStableProfileRootPath(platformId), "native");
        }

        /// <summary>
        /// Determines whether deterministic headless cache mode is enabled.
        /// </summary>
        /// <returns><c>true</c> when a non-empty stable cache root is configured.</returns>
        internal bool UsesStableCacheRoot() {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CacheRootEnvironmentVariableName));
        }

        /// <summary>
        /// Computes one stable project hash segment from the canonical authored project root path.
        /// </summary>
        /// <param name="projectRootPath">Absolute authored project root path.</param>
        /// <returns>Filesystem-safe lowercase hexadecimal hash segment.</returns>
        static string ComputeProjectHashSegment(string projectRootPath) {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(projectRootPath));
            StringBuilder builder = new StringBuilder(ProjectHashByteCount * 2);
            for (int index = 0; index < ProjectHashByteCount; index++) {
                builder.Append(hashBytes[index].ToString("x2"));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Computes one compact deterministic invocation segment from a sanitized identifier.
        /// </summary>
        /// <param name="identifier">Queue or execution identifier used by the default temporary layout.</param>
        /// <returns>Lowercase fixed-width hexadecimal SHA-256 segment.</returns>
        static string ComputeInvocationSegment(string identifier) {
            string sanitizedIdentifier = SanitizePathSegment(identifier);
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sanitizedIdentifier));
            StringBuilder builder = new StringBuilder(InvocationHashByteCount * 2);
            for (int index = 0; index < InvocationHashByteCount; index++) {
                builder.Append(hashBytes[index].ToString("x2"));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Computes the wrapper-compatible case-stable project identity used by deterministic cache mode.
        /// </summary>
        /// <param name="projectRootPath">Absolute authored project root path.</param>
        /// <returns>First sixteen SHA-256 bytes encoded as lowercase hexadecimal.</returns>
        static string ComputeStableProjectHashSegment(string projectRootPath) {
            string projectIdentityPath = ResolveCanonicalDirectoryPath(projectRootPath).ToLowerInvariant();
            return ComputeProjectHashSegment(projectIdentityPath);
        }

        /// <summary>
        /// Resolves the deterministic profile root shared with the platform wrapper cache layout.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <returns>Absolute deterministic cache root for the selected platform profile.</returns>
        string ResolveStableProfileRootPath(string platformId) {
            string cacheRootPath = Environment.GetEnvironmentVariable(CacheRootEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(cacheRootPath)) {
                throw new InvalidOperationException("A stable build cache root must be configured.");
            }

            string configuration = Environment.GetEnvironmentVariable(ConfigurationEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(configuration)) {
                throw new InvalidOperationException($"{ConfigurationEnvironmentVariableName} must be configured when stable build cache mode is enabled.");
            }

            string profile = Environment.GetEnvironmentVariable(ProfileEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(profile)) {
                throw new InvalidOperationException($"{ProfileEnvironmentVariableName} must be configured when stable build cache mode is enabled.");
            }

            string versionRootPath = CombineStrictDescendantPath(ResolveCanonicalDirectoryPath(cacheRootPath), "v2");
            string projectRootPath = CombineStrictDescendantPath(versionRootPath, StableProjectHashSegment);
            string platformsRootPath = CombineStrictDescendantPath(projectRootPath, "b");
            string platformRootPath = CombineStrictDescendantPath(platformsRootPath, SanitizeStablePathSegment(platformId));
            string configurationRootPath = CombineStrictDescendantPath(platformRootPath, SanitizeStablePathSegment(configuration));
            return CombineStrictDescendantPath(configurationRootPath, SanitizeStablePathSegment(profile));
        }

        /// <summary>
        /// Canonicalizes one directory path while preserving filesystem roots and removing trailing aliases elsewhere.
        /// </summary>
        /// <param name="path">Directory path to canonicalize.</param>
        /// <returns>Absolute canonical directory path.</returns>
        static string ResolveCanonicalDirectoryPath(string path) {
            string fullPath = Path.GetFullPath(path);
            string rootPath = Path.GetPathRoot(fullPath);
            if (fullPath.Length <= rootPath.Length) {
                return rootPath;
            }

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Combines one safe child segment and verifies the result remains below its canonical parent.
        /// </summary>
        /// <param name="parentPath">Canonical or relative parent directory.</param>
        /// <param name="childSegment">Single child path segment.</param>
        /// <returns>Canonical strict descendant path.</returns>
        static string CombineStrictDescendantPath(string parentPath, string childSegment) {
            string canonicalParentPath = ResolveCanonicalDirectoryPath(parentPath);
            string candidatePath = ResolveCanonicalDirectoryPath(Path.Combine(canonicalParentPath, childSegment));
            string parentPrefix = canonicalParentPath.EndsWith(Path.DirectorySeparatorChar)
                ? canonicalParentPath
                : canonicalParentPath + Path.DirectorySeparatorChar;
            if (candidatePath.Equals(canonicalParentPath, StringComparison.OrdinalIgnoreCase)
                || !candidatePath.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Path '{candidatePath}' must be a strict descendant of '{canonicalParentPath}'.", nameof(childSegment));
            }

            return candidatePath;
        }

        /// <summary>
        /// Resolves the base isolation directory, honoring the build host's explicit visible workspace root when provided.
        /// </summary>
        /// <returns>Absolute root directory for isolated build state.</returns>
        static string ResolveIsolationRootPath() {
            string configuredWorkspaceRootPath = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configuredWorkspaceRootPath)) {
                return Path.GetFullPath(configuredWorkspaceRootPath);
            }

            return Path.Combine(Path.GetTempPath(), IsolationFolderName);
        }

        /// <summary>
        /// Determines whether the build host supplied an explicit compact workspace root.
        /// </summary>
        /// <returns><c>true</c> when invocation paths should omit stable project nesting.</returns>
        static bool UsesConfiguredWorkspaceRoot() {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariableName));
        }

        /// <summary>
        /// Replaces filesystem-invalid characters in one path segment with underscores.
        /// </summary>
        /// <param name="value">Untrusted segment value.</param>
        /// <returns>Filesystem-safe segment value.</returns>
        static string SanitizePathSegment(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("Path segment must be provided.", nameof(value));
            }

            StringBuilder builder = new StringBuilder(value.Length);
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int index = 0; index < value.Length; index++) {
                char currentCharacter = value[index];
                builder.Append(Array.IndexOf(invalidCharacters, currentCharacter) >= 0 ? '_' : currentCharacter);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Produces a stable-cache segment while rejecting traversal and Windows directory aliases.
        /// </summary>
        /// <param name="value">Untrusted stable-cache segment value.</param>
        /// <returns>Filesystem-safe stable-cache segment.</returns>
        static string SanitizeStablePathSegment(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("Path segment must be provided.", nameof(value));
            }
            const string windowsInvalidCharacters = "<>:\"/\\|?*";
            for (int index = 0; index < value.Length; index++) {
                char currentCharacter = value[index];
                if (currentCharacter < 0x20 || windowsInvalidCharacters.IndexOf(currentCharacter) >= 0) {
                    throw new ArgumentException($"Path segment '{value}' contains a filesystem separator or invalid character.", nameof(value));
                }
            }
            if (value == "." || value == "..") {
                throw new ArgumentException($"Path segment '{value}' is not allowed.", nameof(value));
            }
            if (!string.IsNullOrEmpty(value)
                && (value.EndsWith(".", StringComparison.Ordinal) || value.EndsWith(" ", StringComparison.Ordinal))) {
                throw new ArgumentException($"Path segment '{value}' has a trailing Windows alias character.", nameof(value));
            }

            string safeSegment = SanitizePathSegment(value);
            if (safeSegment == "." || safeSegment == "..") {
                throw new ArgumentException($"Path segment '{value}' resolves to a traversal segment.", nameof(value));
            }
            if (safeSegment.EndsWith(".", StringComparison.Ordinal) || safeSegment.EndsWith(" ", StringComparison.Ordinal)) {
                throw new ArgumentException($"Path segment '{value}' resolves to a trailing Windows alias character.", nameof(value));
            }

            string deviceBaseName = safeSegment.Split('.')[0];
            bool isReservedDeviceName = deviceBaseName.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || deviceBaseName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || deviceBaseName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || deviceBaseName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || IsNumberedReservedDeviceName(deviceBaseName, "COM")
                || IsNumberedReservedDeviceName(deviceBaseName, "LPT");
            if (isReservedDeviceName) {
                throw new ArgumentException($"Path segment '{value}' uses a reserved Windows device basename.", nameof(value));
            }

            return safeSegment;
        }

        /// <summary>
        /// Determines whether one path basename is a numbered Windows device name from one through nine.
        /// </summary>
        /// <param name="value">Filesystem basename to inspect.</param>
        /// <param name="prefix">Reserved device prefix.</param>
        /// <returns><c>true</c> when the value is a reserved numbered device name.</returns>
        static bool IsNumberedReservedDeviceName(string value, string prefix) {
            return value.Length == prefix.Length + 1
                && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && value[value.Length - 1] >= '1'
                && value[value.Length - 1] <= '9';
        }
    }
}
