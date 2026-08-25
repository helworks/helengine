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
        /// Creates one canonical file-backed authored reference with stable identity and content hash.
        /// </summary>
        /// <param name="assetId">Stable lowercase hexadecimal UUID without separators.</param>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <param name="contentHash">Lowercase SHA-256 content hash prefixed with <c>sha256:</c>.</param>
        /// <returns>Validated canonical file-backed authored reference.</returns>
        public static SceneAssetReference CreateFileSystemReference(string assetId, string relativePath, string contentHash) {
            ValidateStableAssetId(assetId);
            ValidateContentHash(contentHash);
            ValidateRelativePath(relativePath);
            return new SceneAssetReference(SceneAssetReferenceSourceKind.FileSystem, relativePath, string.Empty, assetId, contentHash);
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

        /// <summary>Reads one optional current editor-authored reference and rejects path-only filesystem payloads.</summary>
        internal static SceneAssetReference ReadOptionalCurrentReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (reader.ReadByte() == 0) {
                return null;
            }

            return ReadRequiredCurrentReference(reader);
        }

        /// <summary>
        /// Rehydrates one generated scene asset reference through the sanctioned construction path.
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
            if (sourceKind != SceneAssetReferenceSourceKind.Generated) {
                throw new InvalidOperationException($"Unsupported scene asset reference source kind '{sourceKind}'.");
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

            return new SceneAssetReference(SceneAssetReferenceSourceKind.Generated, relativePath, providerId, assetId, string.Empty);
        }

        /// <summary>
        /// Rehydrates one serialized reference using the versioned file-reference layout.
        /// </summary>
        /// <param name="sourceKind">Serialized source kind.</param>
        /// <param name="relativePath">Serialized relative path.</param>
        /// <param name="providerId">Serialized provider id.</param>
        /// <param name="assetId">Serialized asset id.</param>
        /// <param name="contentHash">Serialized content hash when present.</param>
        /// <returns>Validated scene asset reference.</returns>
        internal static SceneAssetReference Rehydrate(SceneAssetReferenceSourceKind sourceKind, string relativePath, string providerId, string assetId, string contentHash) {
            if (sourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return CreateFileSystemReference(assetId, relativePath, contentHash);
            }

            return Rehydrate(sourceKind, relativePath, providerId, assetId);
        }

        /// <summary>Rehydrates one packaged runtime reference, whose filesystem contract is path-only.</summary>
        internal static SceneAssetReference RehydratePackaged(SceneAssetReferenceSourceKind sourceKind, string relativePath, string providerId, string assetId, string contentHash) {
            if (sourceKind == SceneAssetReferenceSourceKind.FileSystem &&
                string.IsNullOrWhiteSpace(assetId) && string.IsNullOrWhiteSpace(contentHash)) {
                return CreateFileSystem(relativePath);
            }
            return Rehydrate(sourceKind, relativePath, providerId, assetId, contentHash);
        }

        /// <summary>
        /// Reads one required serialized scene asset reference through the sanctioned construction path.
        /// </summary>
        /// <param name="reader">Reader positioned at the required reference payload.</param>
        /// <returns>Validated scene asset reference.</returns>
        internal static SceneAssetReference ReadRequiredReference(EngineBinaryReader reader) {
            return ReadRequiredReference(reader, true);
        }

        /// <summary>Reads one current editor-authored reference and rejects path-only filesystem payloads.</summary>
        internal static SceneAssetReference ReadRequiredCurrentReference(EngineBinaryReader reader) {
            return ReadRequiredReference(reader, false);
        }

        /// <summary>Reads one serialized reference for the selected persistence contract.</summary>
        static SceneAssetReference ReadRequiredReference(EngineBinaryReader reader, bool packagedRuntime) {
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
            EngineBinaryReadContext.CurrentReadStage = "SceneAssetReference:ContentHash";
            string contentHash = reader.ReadString();
            EngineBinaryReadContext.LastCheckpoint = $"SceneAssetReferenceEnd:{relativePath}@{reader.GetStreamPosition()}";

            return packagedRuntime
                ? RehydratePackaged(sourceKind, relativePath, providerId, assetId, contentHash)
                : Rehydrate(sourceKind, relativePath, providerId, assetId, contentHash);
        }

        /// <summary>
        /// Creates one validated file-backed scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <returns>Validated file-backed scene asset reference.</returns>
        static SceneAssetReference CreateFileSystem(string relativePath) {
            ValidateRelativePath(relativePath);
            return new SceneAssetReference(SceneAssetReferenceSourceKind.FileSystem, relativePath, string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// Validates one authored asset UUID.
        /// </summary>
        /// <param name="assetId">Candidate stable asset UUID.</param>
        static void ValidateStableAssetId(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId) || assetId.Length != 32 || !IsLowerHex(assetId)) {
                throw new ArgumentException("File-backed asset references require a lowercase 32-character hexadecimal asset id.", nameof(assetId));
            }
        }

        /// <summary>
        /// Validates one authored content hash.
        /// </summary>
        /// <param name="contentHash">Candidate content hash.</param>
        static void ValidateContentHash(string contentHash) {
            if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 71 || !contentHash.StartsWith("sha256:", StringComparison.Ordinal) || !IsLowerHex(contentHash.Substring(7))) {
                throw new ArgumentException("File-backed asset references require a sha256: hash with 64 lowercase hexadecimal characters.", nameof(contentHash));
            }
        }

        /// <summary>
        /// Validates one project-relative path.
        /// </summary>
        /// <param name="relativePath">Candidate project-relative path.</param>
        static void ValidateRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("File-backed asset references must include a relative path.", nameof(relativePath));
            }
        }

        /// <summary>
        /// Determines whether all characters in a value are lowercase hexadecimal digits.
        /// </summary>
        /// <param name="value">Value to inspect.</param>
        /// <returns>True when the value contains only lowercase hexadecimal digits.</returns>
        static bool IsLowerHex(string value) {
            for (int index = 0; index < value.Length; index++) {
                char character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }
    }
}
