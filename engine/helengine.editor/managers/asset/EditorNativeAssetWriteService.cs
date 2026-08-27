namespace helengine.editor {
    /// <summary>
    /// Writes current native asset payloads with stable embedded identity and byte-level idempotence.
    /// </summary>
    public sealed class EditorNativeAssetWriteService {
        /// <summary>
        /// Canonical assets root owned by this writer.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Session identity index updated after each successful write.
        /// </summary>
        readonly EditorAssetIdentityIndex IdentityIndex;

        /// <summary>
        /// Session hash cache used for the result recovery hash.
        /// </summary>
        readonly EditorAssetHashCache HashCache;

        /// <summary>
        /// Embedded identity reader for existing native destinations.
        /// </summary>
        readonly AssetIdentityMetadataService MetadataService;

        /// <summary>
        /// Initializes one native writer over the session-owned identity graph.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="identityIndex">Session-owned identity index.</param>
        /// <param name="hashCache">Session-owned hash cache.</param>
        public EditorNativeAssetWriteService(
            string projectRootPath,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetHashCache hashCache) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            AssetsRootPath = Path.Combine(Path.GetFullPath(projectRootPath), "assets");
            IdentityIndex = identityIndex ?? throw new ArgumentNullException(nameof(identityIndex));
            HashCache = hashCache ?? throw new ArgumentNullException(nameof(hashCache));
            MetadataService = new AssetIdentityMetadataService();
        }

        /// <summary>
        /// Writes one current native asset beneath the canonical assets root.
        /// </summary>
        /// <param name="relativePath">Assets-relative native destination path.</param>
        /// <param name="asset">Native asset payload to serialize.</param>
        /// <returns>Disposition and canonical identity data for the destination.</returns>
        public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            string fullPath = ResolveDestination(relativePath, out string normalizedRelativePath);
            bool destinationExists = File.Exists(fullPath);
            bool preservedExistingIdentity = false;
            if (destinationExists) {
                CopyExistingIdentity(fullPath, asset);
                preservedExistingIdentity = true;
            } else {
                AssignNewDestinationIdentity(asset);
            }
            asset.FormerAuthoringAssetIds ??= Array.Empty<string>();

            byte[] serializedBytes = AssetSerializer.SerializeToBytes(asset);
            EditorAssetWriteDisposition disposition = destinationExists
                ? EditorAssetWriteDisposition.Changed
                : EditorAssetWriteDisposition.Created;
            if (destinationExists && File.ReadAllBytes(fullPath).AsSpan().SequenceEqual(serializedBytes)) {
                disposition = EditorAssetWriteDisposition.Unchanged;
            } else {
                WriteAtomically(fullPath, serializedBytes);
            }

            IdentityIndex.RegisterOrUpdate(fullPath);
            string contentHash = HashCache.GetContentHash(fullPath);
            return new EditorAssetWriteResult(
                normalizedRelativePath,
                fullPath,
                asset.AuthoringAssetId,
                contentHash,
                disposition,
                preservedExistingIdentity);
        }

        /// <summary>
        /// Resolves and validates an assets-relative destination without touching the filesystem.
        /// </summary>
        /// <param name="relativePath">Candidate relative destination.</param>
        /// <param name="normalizedRelativePath">Canonical slash-separated relative path.</param>
        /// <returns>Canonical absolute destination.</returns>
        string ResolveDestination(string relativePath, out string normalizedRelativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            }
            if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Asset relative path must not be rooted.", nameof(relativePath));
            }

            string canonicalAssetsRoot = Path.GetFullPath(AssetsRootPath);
            string fullPath = Path.GetFullPath(Path.Combine(
                canonicalAssetsRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsPrefix = canonicalAssetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Native asset destinations must remain beneath the project assets root.");
            }
            if (string.Equals(fullPath, canonicalAssetsRoot, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Native asset destinations must identify a file beneath the project assets root.");
            }

            normalizedRelativePath = Path.GetRelativePath(canonicalAssetsRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Trim('/');
            return fullPath;
        }

        /// <summary>
        /// Copies identity from a valid current native destination into the new payload.
        /// </summary>
        /// <param name="fullPath">Existing native destination.</param>
        /// <param name="asset">Incoming payload receiving identity.</param>
        void CopyExistingIdentity(string fullPath, Asset asset) {
            AssetIdentityMetadataDocument identity = MetadataService.Load(fullPath);
            asset.AuthoringAssetId = identity.AssetId;
            asset.FormerAuthoringAssetIds = identity.FormerAssetIds.ToArray();
        }

        /// <summary>
        /// Accepts an unowned valid caller identity or mints a fresh current identity.
        /// </summary>
        /// <param name="asset">New payload receiving the destination identity.</param>
        void AssignNewDestinationIdentity(Asset asset) {
            if (IsValidAssetId(asset.AuthoringAssetId)) {
                if (IdentityIndex.IsCurrentAssetIdOwned(asset.AuthoringAssetId)) {
                    throw new InvalidOperationException($"Native asset identity '{asset.AuthoringAssetId}' is already owned by another current asset.");
                }
                return;
            }

            string assetId;
            do {
                assetId = Guid.NewGuid().ToString("N");
            } while (IdentityIndex.IsCurrentAssetIdOwned(assetId));
            asset.AuthoringAssetId = assetId;
        }

        /// <summary>
        /// Writes serialized bytes through a temporary file and one destination replacement.
        /// </summary>
        /// <param name="fullPath">Canonical destination path.</param>
        /// <param name="serializedBytes">Complete current-format bytes.</param>
        static void WriteAtomically(string fullPath, byte[] serializedBytes) {
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Native asset destination does not include a writable directory.");
            }

            Directory.CreateDirectory(directoryPath);
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllBytes(temporaryPath, serializedBytes);
                File.Move(temporaryPath, fullPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Determines whether an identity is a valid lowercase 32-character hexadecimal value.
        /// </summary>
        /// <param name="assetId">Candidate identity.</param>
        /// <returns>True when the identity is valid.</returns>
        static bool IsValidAssetId(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId) || assetId.Length != 32) {
                return false;
            }
            for (int index = 0; index < assetId.Length; index++) {
                char character = assetId[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }
    }
}
