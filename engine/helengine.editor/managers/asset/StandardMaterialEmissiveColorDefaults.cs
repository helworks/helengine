namespace helengine.editor {
    /// <summary>
    /// Stores the shared standard-material emissive-color constant-buffer contract used by editor-side shader and material generation paths.
    /// </summary>
    public static class StandardMaterialEmissiveColorDefaults {
        /// <summary>
        /// Stable constant-buffer binding name used by the built-in forward standard shader for authored emissive color.
        /// </summary>
        public const string EmissiveColorBufferName = "EmissiveColorBuffer";

        /// <summary>
        /// Gets one copied default emissive-color payload that carries no glow until explicitly authored.
        /// </summary>
        /// <returns>Copied sixteen-byte float4 payload representing a white tint with zero emissive strength.</returns>
        public static byte[] CreateDefaultConstantBufferData() {
            return CreateConstantBufferData(new float4(1f, 1f, 1f, 0f));
        }

        /// <summary>
        /// Creates one packed float4 constant-buffer payload from the supplied emissive color.
        /// </summary>
        /// <param name="value">Float4 emissive tint where alpha acts as emissive strength.</param>
        /// <returns>Sixteen-byte packed constant-buffer payload.</returns>
        public static byte[] CreateConstantBufferData(float4 value) {
            byte[] data = new byte[16];
            Array.Copy(BitConverter.GetBytes(value.X), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(value.Y), 0, data, 4, 4);
            Array.Copy(BitConverter.GetBytes(value.Z), 0, data, 8, 4);
            Array.Copy(BitConverter.GetBytes(value.W), 0, data, 12, 4);
            return data;
        }
    }
}
