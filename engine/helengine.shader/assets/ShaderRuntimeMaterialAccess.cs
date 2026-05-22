namespace helengine {
    /// <summary>
    /// Provides explicit validation helpers for code paths that require shader-runtime material binding state.
    /// </summary>
    public static class ShaderRuntimeMaterialAccess {
        /// <summary>
        /// Validates that one runtime material is shader-backed and returns the shader-runtime view required by binding-aware systems.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material instance to validate.</param>
        /// <returns>Shader runtime material view over the supplied material.</returns>
        public static ShaderRuntimeMaterial Require(RuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            if (runtimeMaterial is ShaderRuntimeMaterial shaderRuntimeMaterial) {
                return shaderRuntimeMaterial;
            }

            throw new InvalidOperationException("This code path requires a shader-backed runtime material.");
        }
    }
}
