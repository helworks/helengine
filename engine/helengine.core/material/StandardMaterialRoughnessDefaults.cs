namespace helengine {
    /// <summary>
    /// Stores the shared standard-material roughness constant-buffer contract used by builder, editor, and runtime paths.
    /// </summary>
    public static class StandardMaterialRoughnessDefaults {
        /// <summary>
        /// Stable constant-buffer binding name used by the built-in forward standard shader for authored roughness.
        /// </summary>
        public const string RoughnessBufferName = "RoughnessBuffer";

        /// <summary>
        /// Default authored roughness used when a material omits the field.
        /// </summary>
        public const float DefaultRoughness = 0.4f;

        /// <summary>
        /// Creates one packed float4 constant-buffer payload from the supplied roughness.
        /// </summary>
        /// <param name="roughness">Authored roughness value that will be clamped to the supported zero-to-one range.</param>
        /// <returns>Sixteen-byte packed constant-buffer payload.</returns>
        [NativeOwnedReturn]
        public static byte[] CreateConstantBufferData(float roughness) {
            return StandardMaterialScalarDefaults.CreateConstantBufferData(roughness);
        }

        /// <summary>
        /// Creates one default roughness constant-buffer payload.
        /// </summary>
        /// <returns>Sixteen-byte packed constant-buffer payload for the default roughness.</returns>
        [NativeOwnedReturn]
        public static byte[] CreateDefaultConstantBufferData() {
            return CreateConstantBufferData(DefaultRoughness);
        }
    }
}
