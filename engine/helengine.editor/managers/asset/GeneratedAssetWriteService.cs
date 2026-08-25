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
    }
}
