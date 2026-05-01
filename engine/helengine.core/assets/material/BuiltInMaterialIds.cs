namespace helengine {
    /// <summary>
    /// Identifies built-in material shader layouts that the engine treats specially during rendering.
    /// </summary>
    public static class BuiltInMaterialIds {
        /// <summary>
        /// Stable shader asset id used by the generated standard mesh material.
        /// </summary>
        public const string StandardMaterialShaderAssetId = "engine:material:standard";

        /// <summary>
        /// Stable runtime material asset id used by the built-in standard mesh material.
        /// </summary>
        public const string StandardRuntimeMaterialAssetId = "Engine.Materials.Standard.material";

        /// <summary>
        /// Determines whether a runtime material id should receive the shared standard-mesh transform payload.
        /// </summary>
        /// <param name="materialId">Runtime material asset id to evaluate.</param>
        /// <returns>True when the runtime material uses the built-in standard mesh layout; otherwise false.</returns>
        public static bool UsesStandardMeshTransform(string materialId) {
            return string.Equals(materialId, StandardRuntimeMaterialAssetId, StringComparison.Ordinal);
        }
    }
}
