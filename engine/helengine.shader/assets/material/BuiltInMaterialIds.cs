namespace helengine {
    /// <summary>
    /// Identifies shared shader-runtime material and program conventions used by shader-capable backends.
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
        /// Stable shader asset id used by the built-in forward standard material pipeline.
        /// </summary>
        public const string StandardForwardShaderAssetId = "ForwardStandardShader";

        /// <summary>
        /// Stable vertex program name used by the built-in forward standard material pipeline.
        /// </summary>
        public const string StandardForwardVertexProgramName = "ForwardStandardShader.vs";

        /// <summary>
        /// Stable pixel program name used by the built-in forward standard material pipeline.
        /// </summary>
        public const string StandardForwardPixelProgramName = "ForwardStandardShader.ps";

        /// <summary>
        /// Determines whether a runtime material id should receive the shared standard-mesh transform payload.
        /// </summary>
        /// <param name="materialId">Runtime material asset id to evaluate.</param>
        /// <returns>True when the runtime material uses the built-in standard mesh layout; otherwise false.</returns>
        public static bool UsesStandardMeshTransform(string materialId) {
            return string.Equals(materialId, StandardRuntimeMaterialAssetId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether one material selection uses the shared standard-mesh transform payload based on either the runtime material id or the selected built-in forward shader programs.
        /// </summary>
        /// <param name="materialId">Runtime material asset id to evaluate.</param>
        /// <param name="shaderAssetId">Shader asset id selected by the material layout.</param>
        /// <param name="vertexProgram">Vertex program selected by the material layout.</param>
        /// <param name="pixelProgram">Pixel program selected by the material layout.</param>
        /// <returns>True when the material selection uses the built-in standard mesh layout; otherwise false.</returns>
        public static bool UsesStandardMeshTransform(
            string materialId,
            string shaderAssetId,
            string vertexProgram,
            string pixelProgram) {
            if (UsesStandardMeshTransform(materialId)) {
                return true;
            }

            return string.Equals(shaderAssetId, StandardForwardShaderAssetId, StringComparison.Ordinal)
                && string.Equals(vertexProgram, StandardForwardVertexProgramName, StringComparison.Ordinal)
                && string.Equals(pixelProgram, StandardForwardPixelProgramName, StringComparison.Ordinal);
        }
    }
}
