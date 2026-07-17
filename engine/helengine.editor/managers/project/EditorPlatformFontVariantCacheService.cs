namespace helengine.editor {
    /// <summary>
    /// Generates and reuses editor-side cached platform font variants stored beneath the project cache root.
    /// </summary>
    public sealed class EditorPlatformFontVariantCacheService {
        /// <summary>
        /// Folder name used for generated cache content owned by the editor.
        /// </summary>
        const string GeneratedFolderName = "generated";

        /// <summary>
        /// Cache-format version included in the font-variant settings hash so import-pipeline fixes can invalidate stale cached atlases.
        /// </summary>
        const string CacheFormatVersion = "font-variant-cache-v4";

        /// <summary>
        /// Folder name used for cached per-platform font variants.
        /// </summary>
        const string PlatformFontsFolderName = "platform-fonts";

        /// <summary>
        /// Asset import manager that loads source fonts and applies per-platform texture settings.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Initializes one editor-side platform font cache service.
        /// </summary>
        /// <param name="assetImportManager">Asset import manager used to build platform-specific font variants.</param>
        public EditorPlatformFontVariantCacheService(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Resolves one cached platform font variant, generating the font and atlas cache files when they do not already exist.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the authored source font file.</param>
        /// <param name="targetPlatformId">Target platform identifier whose texture settings should drive generation.</param>
        /// <returns>Resolved cached platform font variant paths and cache-hit metadata.</returns>
        public EditorPlatformFontVariantCacheResult ResolveVariant(string sourcePath, string targetPlatformId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (string.IsNullOrWhiteSpace(targetPlatformId)) {
                throw new ArgumentException("Target platform id must be provided.", nameof(targetPlatformId));
            }

            AssetImportSettings settings = AssetImportManager.LoadOrCreateImportSettings(sourcePath);
            EditorPlatformFontVariantCacheKey cacheKey = BuildCacheKey(settings, targetPlatformId);
            string variantDirectoryPath = GetVariantDirectoryPath(cacheKey);
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string cachedFontAssetPath = Path.Combine(variantDirectoryPath, fileName + ".hefont");
            string cachedAtlasTextureAssetPath = Path.Combine(variantDirectoryPath, fileName + ".hetex");

            if (File.Exists(cachedFontAssetPath) && File.Exists(cachedAtlasTextureAssetPath)) {
                return new EditorPlatformFontVariantCacheResult(cachedFontAssetPath, cachedAtlasTextureAssetPath, true);
            }

            FontAsset cachedFontAsset = AssetImportManager.BuildFontAssetForPlatform(sourcePath, targetPlatformId);
            if (cachedFontAsset.SourceTextureAsset == null) {
                throw new InvalidOperationException("Platform font variants require one processed atlas texture asset.");
            }

            TextureAsset cachedAtlasTextureAsset = cachedFontAsset.SourceTextureAsset;
            string cachedAtlasTextureAssetId = cacheKey.VariantId + "#atlas";
            cachedAtlasTextureAsset.Id = cachedAtlasTextureAssetId;
            cachedAtlasTextureAsset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(cachedAtlasTextureAssetId);

            Directory.CreateDirectory(variantDirectoryPath);
            using (FileStream atlasStream = new FileStream(cachedAtlasTextureAssetPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(atlasStream, cachedAtlasTextureAsset);
            }

            cachedFontAsset.CookedAtlasTextureRelativePath = BuildCachedAtlasTextureRelativePath(cachedAtlasTextureAssetPath);
            cachedFontAsset.SourceTextureAsset = null;
            using (FileStream fontStream = new FileStream(cachedFontAssetPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                helengine.files.FontAssetBinarySerializer.Serialize(fontStream, cachedFontAsset);
            }

            return new EditorPlatformFontVariantCacheResult(cachedFontAssetPath, cachedAtlasTextureAssetPath, false);
        }

        /// <summary>
        /// Builds one cache key from the current authored import settings and target platform identifier.
        /// </summary>
        /// <param name="settings">Resolved authored import settings for the source font.</param>
        /// <param name="targetPlatformId">Target platform identifier whose texture settings should drive generation.</param>
        /// <returns>Deterministic cache key for the requested platform font variant.</returns>
        EditorPlatformFontVariantCacheKey BuildCacheKey(AssetImportSettings settings, string targetPlatformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Font variant cache generation requires importer settings.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.SourceChecksum)) {
                throw new InvalidOperationException("Font variant cache generation requires a source checksum.");
            }

            string settingsChecksum = ComputeSettingsChecksum(settings);
            return new EditorPlatformFontVariantCacheKey(targetPlatformId, settings.Importer.SourceChecksum, settingsChecksum);
        }

        /// <summary>
        /// Serializes the current authored import settings and hashes the payload so cache invalidation follows any settings change.
        /// </summary>
        /// <param name="settings">Resolved authored import settings for the source font.</param>
        /// <returns>Checksum of the serialized authored import settings.</returns>
        string ComputeSettingsChecksum(AssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            using MemoryStream stream = new MemoryStream();
            byte[] versionBytes = System.Text.Encoding.UTF8.GetBytes(CacheFormatVersion);
            stream.Write(versionBytes, 0, versionBytes.Length);
            AssetImportSettingsBinarySerializer.Serialize(stream, settings);
            return ComputeChecksum(stream.ToArray());
        }

        /// <summary>
        /// Builds the absolute cache directory path that owns one platform font variant.
        /// </summary>
        /// <param name="cacheKey">Cache key that identifies the requested variant.</param>
        /// <returns>Absolute directory path for the variant cache files.</returns>
        string GetVariantDirectoryPath(EditorPlatformFontVariantCacheKey cacheKey) {
            if (cacheKey == null) {
                throw new ArgumentNullException(nameof(cacheKey));
            }

            return Path.Combine(
                AssetImportManager.ImportRootPath,
                GeneratedFolderName,
                PlatformFontsFolderName,
                cacheKey.TargetPlatformId,
                cacheKey.VariantId);
        }

        /// <summary>
        /// Converts one absolute cached atlas path into a cache-root-relative path suitable for font serialization.
        /// </summary>
        /// <param name="cachedAtlasTextureAssetPath">Absolute cached atlas texture asset path.</param>
        /// <returns>Import-root-relative atlas path with normalized separators.</returns>
        string BuildCachedAtlasTextureRelativePath(string cachedAtlasTextureAssetPath) {
            if (string.IsNullOrWhiteSpace(cachedAtlasTextureAssetPath)) {
                throw new ArgumentException("Cached atlas texture asset path must be provided.", nameof(cachedAtlasTextureAssetPath));
            }

            string relativePath = Path.GetRelativePath(AssetImportManager.ImportRootPath, cachedAtlasTextureAssetPath);
            return relativePath
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        /// <summary>
        /// Computes one lowercase SHA-256 checksum for the supplied bytes.
        /// </summary>
        /// <param name="data">Payload bytes to hash.</param>
        /// <returns>Lowercase hexadecimal checksum string.</returns>
        string ComputeChecksum(byte[] data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
