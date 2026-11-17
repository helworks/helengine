namespace helengine.editor {
    /// <summary>
    /// Gives a packaged asset cooker the paths and write services of the packaging operation that invoked it.
    /// </summary>
    public sealed class PackagedAssetCookContext {
        /// <summary>
        /// Writes one asset to an absolute output path through the packager, so the output is tracked as cooked scene output.
        /// </summary>
        readonly Action<string, Asset> AssetWriter;

        /// <summary>
        /// Writes validated binary payloads through the packager's cooked-output bookkeeping.
        /// </summary>
        readonly Action<string, byte[]> ByteWriter;

        /// <summary>
        /// Creates the packaged reference for a build-relative path through the packager.
        /// </summary>
        readonly Func<string, SceneAssetReference> PackagedReferenceFactory;

        /// <summary>
        /// Gets the absolute path of the authored project asset being cooked.
        /// </summary>
        public string ProjectAssetFullPath { get; }

        /// <summary>
        /// Gets the absolute build root that receives packaged assets.
        /// </summary>
        public string BuildRootPath { get; }

        /// <summary>
        /// Gets the target platform id of the current packaging operation.
        /// </summary>
        public string TargetPlatformId { get; }

        /// <summary>
        /// Gets the selected environment id of the current packaging operation, or an empty string when none is selected.
        /// </summary>
        public string EnvironmentId { get; }

        /// <summary>
        /// Creates a cook context for one asset.
        /// </summary>
        /// <param name="projectAssetFullPath">Absolute path of the authored asset.</param>
        /// <param name="buildRootPath">Absolute build root path.</param>
        /// <param name="targetPlatformId">Target platform id.</param>
        /// <param name="environmentId">Selected environment id, empty when none.</param>
        /// <param name="assetWriter">Packager callback that writes an asset to an absolute path.</param>
        /// <param name="packagedReferenceFactory">Packager callback that creates a packaged reference for a build-relative path.</param>
        /// <param name="byteWriter">Optional packager callback that writes raw bytes and tracks the cooked output.</param>
        public PackagedAssetCookContext(
            string projectAssetFullPath,
            string buildRootPath,
            string targetPlatformId,
            string environmentId,
            Action<string, Asset> assetWriter,
            Func<string, SceneAssetReference> packagedReferenceFactory,
            Action<string, byte[]> byteWriter = null) {
            if (string.IsNullOrWhiteSpace(projectAssetFullPath)) {
                throw new ArgumentException("Project asset path must be provided.", nameof(projectAssetFullPath));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (string.IsNullOrWhiteSpace(targetPlatformId)) {
                throw new ArgumentException("Target platform id must be provided.", nameof(targetPlatformId));
            }

            ProjectAssetFullPath = projectAssetFullPath;
            BuildRootPath = buildRootPath;
            TargetPlatformId = targetPlatformId;
            EnvironmentId = environmentId ?? throw new ArgumentNullException(nameof(environmentId));
            AssetWriter = assetWriter ?? throw new ArgumentNullException(nameof(assetWriter));
            PackagedReferenceFactory = packagedReferenceFactory ?? throw new ArgumentNullException(nameof(packagedReferenceFactory));
            ByteWriter = byteWriter;
        }

        /// <summary>
        /// Serializes one asset into the build root.
        /// </summary>
        /// <param name="buildRelativePath">Canonical build-relative output path.</param>
        /// <param name="asset">Asset to serialize.</param>
        public void WriteAsset(string buildRelativePath, Asset asset) {
            string outputPath = ResolveConfinedOutputPath(buildRelativePath);
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            AssetWriter(outputPath, asset);
        }

        /// <summary>
        /// Writes an already validated binary payload beneath the build root and records it as cooked output.
        /// </summary>
        /// <param name="buildRelativePath">Canonical build-relative output path.</param>
        /// <param name="bytes">Complete binary payload to preserve unchanged.</param>
        public void WriteBytes(string buildRelativePath, byte[] bytes) {
            string outputPath = ResolveConfinedOutputPath(buildRelativePath);
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            } else if (ByteWriter == null) {
                throw new InvalidOperationException("This packaging context has no binary output writer.");
            }

            ByteWriter(outputPath, bytes);
        }

        /// <summary>
        /// Creates the scene reference that points at a cooked file in the build root.
        /// </summary>
        /// <param name="buildRelativePath">Canonical build-relative path of the cooked file.</param>
        /// <returns>Packaged scene asset reference.</returns>
        public SceneAssetReference CreatePackagedReference(string buildRelativePath) {
            ResolveConfinedOutputPath(buildRelativePath);
            return PackagedReferenceFactory(buildRelativePath);
        }

        /// <summary>
        /// Resolves a cooker-supplied build-relative path to its absolute location, rejecting rooted paths (including drive-relative and
        /// UNC paths) and paths whose ".." segments leave the build root, so a third-party cooker cannot write or reference files outside the build.
        /// </summary>
        /// <param name="buildRelativePath">Build-relative path supplied by the cooker.</param>
        /// <returns>Absolute path beneath the build root.</returns>
        string ResolveConfinedOutputPath(string buildRelativePath) {
            if (string.IsNullOrWhiteSpace(buildRelativePath)) {
                throw new ArgumentException("Build-relative path must be provided.", nameof(buildRelativePath));
            } else if (Path.IsPathRooted(buildRelativePath) || buildRelativePath.StartsWith("/", StringComparison.Ordinal)
                || buildRelativePath.StartsWith("\\", StringComparison.Ordinal) || buildRelativePath.Contains(':')) {
                throw new ArgumentException($"Cooked output path '{buildRelativePath}' must be relative to the build root.", nameof(buildRelativePath));
            }

            string root = Path.GetFullPath(BuildRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, buildRelativePath.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar)));
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison)) {
                throw new ArgumentException($"Cooked output path '{buildRelativePath}' leaves the build root '{BuildRootPath}'.", nameof(buildRelativePath));
            }

            return candidate;
        }
    }
}
