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
        public PackagedAssetCookContext(
            string projectAssetFullPath,
            string buildRootPath,
            string targetPlatformId,
            string environmentId,
            Action<string, Asset> assetWriter,
            Func<string, SceneAssetReference> packagedReferenceFactory) {
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
        }

        /// <summary>
        /// Serializes one asset into the build root.
        /// </summary>
        /// <param name="buildRelativePath">Canonical build-relative output path.</param>
        /// <param name="asset">Asset to serialize.</param>
        public void WriteAsset(string buildRelativePath, Asset asset) {
            if (string.IsNullOrWhiteSpace(buildRelativePath)) {
                throw new ArgumentException("Build-relative path must be provided.", nameof(buildRelativePath));
            }
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            AssetWriter(Path.Combine(BuildRootPath, buildRelativePath), asset);
        }

        /// <summary>
        /// Creates the scene reference that points at a cooked file in the build root.
        /// </summary>
        /// <param name="buildRelativePath">Canonical build-relative path of the cooked file.</param>
        /// <returns>Packaged scene asset reference.</returns>
        public SceneAssetReference CreatePackagedReference(string buildRelativePath) {
            if (string.IsNullOrWhiteSpace(buildRelativePath)) {
                throw new ArgumentException("Build-relative path must be provided.", nameof(buildRelativePath));
            }

            return PackagedReferenceFactory(buildRelativePath);
        }
    }
}
