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
        public static byte[] CreateConstantBufferData(float roughness) {
            float normalized = Math.Clamp(roughness, 0f, 1f);
            byte[] data = new byte[16];
            WriteSingle(data, 0, normalized);
            WriteSingle(data, 4, normalized);
            WriteSingle(data, 8, normalized);
            WriteSingle(data, 12, normalized);
            return data;
        }

        /// <summary>
        /// Creates one default roughness constant-buffer payload.
        /// </summary>
        /// <returns>Sixteen-byte packed constant-buffer payload for the default roughness.</returns>
        public static byte[] CreateDefaultConstantBufferData() {
            return CreateConstantBufferData(DefaultRoughness);
        }

        /// <summary>
        /// Writes one single-precision floating-point value into the supplied byte array using little-endian layout.
        /// </summary>
        /// <param name="data">Destination byte array.</param>
        /// <param name="offset">Destination byte offset.</param>
        /// <param name="value">Single-precision value to encode.</param>
        static void WriteSingle(byte[] data, int offset, float value) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            } else if (offset < 0 || offset > data.Length - 4) {
                throw new ArgumentOutOfRangeException(nameof(offset), "Single-precision values require four writable bytes.");
            }

            int bits = BitConverter.SingleToInt32Bits(value);
            data[offset] = (byte)bits;
            data[offset + 1] = (byte)(bits >> 8);
            data[offset + 2] = (byte)(bits >> 16);
            data[offset + 3] = (byte)(bits >> 24);
        }

    }
}
