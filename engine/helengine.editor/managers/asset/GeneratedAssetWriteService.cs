namespace helengine.editor {
    /// <summary>
    /// Writes generated native authored assets through the current editor serializer.
    /// </summary>
    public sealed class GeneratedAssetWriteService {
        /// <summary>
        /// Writes one generated native asset beneath the project assets folder.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets directory.</param>
        /// <param name="relativePath">Assets-relative native asset path.</param>
        /// <param name="asset">Asset payload to serialize.</param>
        public void WriteAsset(string projectRootPath, string relativePath, Asset asset) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Asset relative path must not be rooted.", nameof(relativePath));
            }

            string projectRoot = Path.GetFullPath(projectRootPath);
            string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "assets"));
            string fullPath = Path.GetFullPath(Path.Combine(assetsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRootWithSeparator = assetsRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? assetsRoot
                : assetsRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsRootWithSeparator, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Generated assets must be written beneath the project assets folder.");
            }

            if (string.IsNullOrWhiteSpace(asset.AuthoringAssetId)) {
                ReuseExistingEmbeddedIdentity(fullPath, asset);
            }
            if (string.IsNullOrWhiteSpace(asset.AuthoringAssetId)) {
                asset.AuthoringAssetId = Guid.NewGuid().ToString("N");
            }
            asset.FormerAuthoringAssetIds ??= Array.Empty<string>();

            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Generated asset directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    AssetSerializer.Serialize(stream, asset);
                }

                File.Move(temporaryPath, fullPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Reuses identity from an existing current native asset when a fresh definition is
        /// rewriting the same path. Invalid or non-current files are replaced by the current
        /// writer and never interpreted as an older format.
        /// </summary>
        /// <param name="fullPath">Absolute generated asset path.</param>
        /// <param name="asset">Fresh asset definition receiving the existing identity.</param>
        static void ReuseExistingEmbeddedIdentity(string fullPath, Asset asset) {
            if (!File.Exists(fullPath)) {
                return;
            }

            try {
                AssetIdentityMetadataDocument metadata = new AssetIdentityMetadataService().Load(fullPath);
                using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Asset existing = AssetSerializer.Deserialize(stream);
                if (existing == null || existing.GetType() != asset.GetType()) {
                    return;
                }

                asset.AuthoringAssetId = metadata.AssetId;
                asset.FormerAuthoringAssetIds = metadata.FormerAssetIds.ToArray();
            } catch (InvalidOperationException) {
                // The current writer owns replacement of invalid/non-current output. There is
                // intentionally no legacy interpretation or migration fallback here.
            }
        }
    }
}
