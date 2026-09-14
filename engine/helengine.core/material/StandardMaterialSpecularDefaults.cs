namespace helengine {
    /// <summary>
    /// Stores the shared standard-material specular constant-buffer contract used by builder, editor, and runtime paths.
    /// </summary>
    public static class StandardMaterialSpecularDefaults {
        /// <summary>
        /// Stable constant-buffer binding name used by the built-in forward standard shader for authored specular.
        /// </summary>
        public const string SpecularBufferName = "SpecularBuffer";

        /// <summary>
        /// Default authored specular used when a material omits the field.
        /// </summary>
        public const float DefaultSpecular = 0.5f;

        /// <summary>
        /// Creates one packed float4 constant-buffer payload from the supplied specular.
        /// </summary>
        /// <param name="specular">Authored specular value that will be clamped to the supported zero-to-one range.</param>
        /// <returns>Sixteen-byte packed constant-buffer payload.</returns>
        [NativeOwnedReturn]
        public static byte[] CreateConstantBufferData(float specular) {
            return StandardMaterialScalarDefaults.CreateConstantBufferData(specular);
        }

        /// <summary>
        /// Creates one default specular constant-buffer payload.
        /// </summary>
        /// <returns>Sixteen-byte packed constant-buffer payload for the default specular.</returns>
        [NativeOwnedReturn]
        public static byte[] CreateDefaultConstantBufferData() {
            return CreateConstantBufferData(DefaultSpecular);
        }
    }
}
