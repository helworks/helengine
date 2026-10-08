using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Selects material cooking consistently for both scene packaging paths, before
    /// schema normalization can discard fields belonging to an authored shader.
    /// </summary>
    public static class PlatformMaterialPackagingPolicy {
        /// <summary>
        /// Determines whether a resolved material goes through the platform cooker.
        /// Only an explicit mixed-format opt-in under a raw shader runtime preserves
        /// shader-owned payloads; classic materials and other platforms keep cooking.
        /// </summary>
        /// <param name="builder">Active material builder, or null for raw material packaging.</param>
        /// <param name="materialAsset">Resolved authored material whose shader identity determines its format.</param>
        /// <returns>True to cook the material, or false to preserve its raw payload.</returns>
        public static bool ShouldCook(IPlatformAssetBuilder builder, ShaderMaterialAsset materialAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (builder == null) {
                return false;
            }
            return !(builder is IPlatformRawShaderMaterialPackagingPolicy policy
                && policy.PreserveAuthoredShaderMaterialPayloads
                && builder.Definition.RuntimeGenerationContract.MaterialResolutionMode == RuntimeMaterialResolutionMode.RawShaderBacked
                && !string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId));
        }
    }
}
