using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>Resolves packaged runtime paths for imported textures from published cook capability policy.</summary>
    static class ImportedTextureRuntimePathResolver {
        const string ImportedTextureDirectoryName = "cooked/imported";

        /// <summary>Builds one packaged path using the capability's naming and extension policy.</summary>
        public static string BuildCookedRelativePath(PlatformAssetCookCapabilityDefinition capability, string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }
            string extension = string.IsNullOrWhiteSpace(capability?.OutputFileExtension) ? string.Empty : NormalizeExtension(capability.OutputFileExtension);
            string fileName = capability?.NamingPolicy == PlatformAssetNamingPolicy.RuntimeAssetIdHex16
                ? RuntimeAssetIdGenerator.Generate(assetId).ToString("x16")
                : assetId;
            return CanonicalPackagedAssetPath.Normalize(string.Concat(ImportedTextureDirectoryName, "/", fileName, extension));
        }

        /// <summary>Builds the default runtime path when no builder capability is available.</summary>
        public static string BuildCookedRelativePath(string targetPlatformId, string assetId) {
            _ = targetPlatformId;
            return BuildCookedRelativePath((PlatformAssetCookCapabilityDefinition)null, assetId);
        }
        /// <summary>Determines whether a cooked path matches the capability-derived imported texture path.</summary>
        public static bool PathMatchesAssetId(PlatformAssetCookCapabilityDefinition capability, string cookedRelativePath, string assetId) {
            if (string.IsNullOrWhiteSpace(cookedRelativePath) || string.IsNullOrWhiteSpace(assetId)) {
                return false;
            }
            return string.Equals(CanonicalPackagedAssetPath.Normalize(cookedRelativePath), BuildCookedRelativePath(capability, assetId), StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeExtension(string extension) {
            string normalized = extension.Trim();
            return normalized.StartsWith(".", StringComparison.Ordinal) ? normalized : "." + normalized;
        }
    }
}