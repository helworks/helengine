namespace helengine {
    /// <summary>
    /// Creates sanctioned file-backed scene asset references.
    /// </summary>
    public static class SceneAssetReferenceFactory {
        /// <summary>
        /// Creates one validated file-backed font reference.
        /// </summary>
        /// <param name="relativePath">Project-relative font path.</param>
        /// <returns>Validated file-backed font reference.</returns>
        public static SceneAssetReference CreateFileSystemFont(string relativePath) {
            return CreateFileSystem(relativePath);
        }

        /// <summary>
        /// Creates one validated file-backed texture reference.
        /// </summary>
        /// <param name="relativePath">Project-relative texture path.</param>
        /// <returns>Validated file-backed texture reference.</returns>
        public static SceneAssetReference CreateFileSystemTexture(string relativePath) {
            return CreateFileSystem(relativePath);
        }

        /// <summary>
        /// Creates one validated file-backed model reference.
        /// </summary>
        /// <param name="relativePath">Project-relative model path.</param>
        /// <returns>Validated file-backed model reference.</returns>
        public static SceneAssetReference CreateFileSystemModel(string relativePath) {
            return CreateFileSystem(relativePath);
        }

        /// <summary>
        /// Creates one validated file-backed material reference.
        /// </summary>
        /// <param name="relativePath">Project-relative material path.</param>
        /// <returns>Validated file-backed material reference.</returns>
        public static SceneAssetReference CreateFileSystemMaterial(string relativePath) {
            return CreateFileSystem(relativePath);
        }

        /// <summary>
        /// Creates one validated file-backed animation clip reference.
        /// </summary>
        /// <param name="relativePath">Project-relative animation clip path.</param>
        /// <returns>Validated file-backed animation clip reference.</returns>
        public static SceneAssetReference CreateFileSystemAnimationClip(string relativePath) {
            return CreateFileSystem(relativePath);
        }

        /// <summary>
        /// Creates one validated file-backed audio reference.
        /// </summary>
        /// <param name="relativePath">Project-relative audio path.</param>
        /// <returns>Validated file-backed audio reference.</returns>
        public static SceneAssetReference CreateFileSystemAudio(string relativePath) {
            return CreateFileSystem(relativePath);
        }

        /// <summary>
        /// Reads one optional serialized scene asset reference through the sanctioned construction path.
        /// </summary>
        /// <param name="reader">Reader positioned at the optional reference payload.</param>
        /// <returns>Validated scene asset reference when present; otherwise null.</returns>
        public static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (reader.ReadByte() == 0) {
                return null;
            }

            return ReadRequiredReference(reader);
        }

        /// <summary>
        /// Rehydrates one serialized scene asset reference through the sanctioned construction path.
        /// </summary>
        /// <param name="sourceKind">Serialized source kind.</param>
        /// <param name="relativePath">Serialized relative path.</param>
        /// <param name="providerId">Serialized provider id.</param>
        /// <param name="assetId">Serialized asset id.</param>
        /// <returns>Validated scene asset reference.</returns>
        internal static SceneAssetReference Rehydrate(SceneAssetReferenceSourceKind sourceKind, string relativePath, string providerId, string assetId) {
            if (sourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return CreateFileSystem(relativePath);
            }

            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new InvalidOperationException("Generated scene asset references must include a relative path.");
            }
            if (string.IsNullOrWhiteSpace(providerId)) {
                throw new InvalidOperationException("Generated scene asset references must include a provider id.");
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new InvalidOperationException("Generated scene asset references must include an asset id.");
            }

            return new SceneAssetReference(SceneAssetReferenceSourceKind.Generated, relativePath, providerId, assetId);
        }

        /// <summary>
        /// Reads one required serialized scene asset reference through the sanctioned construction path.
        /// </summary>
        /// <param name="reader">Reader positioned at the required reference payload.</param>
        /// <returns>Validated scene asset reference.</returns>
        internal static SceneAssetReference ReadRequiredReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            EngineBinaryReadContext.CurrentReadStage = "SceneAssetReference:SourceKind";
            SceneAssetReferenceSourceKind sourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32();
            EngineBinaryReadContext.CurrentReadStage = "SceneAssetReference:RelativePath";
            string relativePath = reader.ReadString();
            EngineBinaryReadContext.CurrentReadStage = "SceneAssetReference:ProviderId";
            string providerId = reader.ReadString();
            EngineBinaryReadContext.CurrentReadStage = "SceneAssetReference:AssetId";
            string assetId = reader.ReadString();
            EngineBinaryReadContext.LastCheckpoint = $"SceneAssetReferenceEnd:{relativePath}@{reader.GetStreamPosition()}";

            return Rehydrate(
                sourceKind,
                relativePath,
                providerId,
                assetId);
        }

        /// <summary>
        /// Creates one validated file-backed scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <returns>Validated file-backed scene asset reference.</returns>
        static SceneAssetReference CreateFileSystem(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("File-backed asset references must include a relative path.", nameof(relativePath));
            }

            return new SceneAssetReference(SceneAssetReferenceSourceKind.FileSystem, relativePath, string.Empty, string.Empty);
        }
    }
}
