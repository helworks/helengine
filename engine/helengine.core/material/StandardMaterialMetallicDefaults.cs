namespace helengine {
    /// <summary>
    /// Stores the shared standard-material metallic constant-buffer contract used by builder, editor, and runtime paths.
    /// </summary>
    public static class StandardMaterialMetallicDefaults {
        /// <summary>
        /// Stable constant-buffer binding name used by the built-in forward standard shader for authored metallic.
        /// </summary>
        public const string MetallicBufferName = "MetallicBuffer";

        /// <summary>
        /// Default authored metallic used when a material omits the field.
        /// </summary>
        public const float DefaultMetallic = 0f;

        /// <summary>
        /// Creates one packed float4 constant-buffer payload from the supplied metallic.
        /// </summary>
        /// <param name="metallic">Authored metallic value that will be clamped to the supported zero-to-one range.</param>
        /// <returns>Sixteen-byte packed constant-buffer payload.</returns>
        [NativeOwnedReturn]
        public static byte[] CreateConstantBufferData(float metallic) {
            return StandardMaterialScalarDefaults.CreateConstantBufferData(metallic);
        }

        /// <summary>
        /// Creates one default metallic constant-buffer payload.
        /// </summary>
        /// <returns>Sixteen-byte packed constant-buffer payload for the default metallic.</returns>
        [NativeOwnedReturn]
        public static byte[] CreateDefaultConstantBufferData() {
            return CreateConstantBufferData(DefaultMetallic);
        }
    }
}
