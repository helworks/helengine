namespace helengine.editor {
    /// <summary>
    /// Stores the shared standard-material base-color constant-buffer contract used by editor-side shader and material generation paths.
    /// </summary>
    public static class StandardMaterialBaseColorDefaults {
        /// <summary>
        /// Stable constant-buffer binding name used by the built-in forward standard shader for authored base color.
        /// </summary>
        public const string BaseColorBufferName = "BaseColorBuffer";

        /// <summary>
        /// Gets one copied white base-color constant-buffer payload.
        /// </summary>
        /// <returns>Copied sixteen-byte float4 payload representing opaque white.</returns>
        public static byte[] CreateWhiteConstantBufferData() {
            return CreateConstantBufferData(new float4(1f, 1f, 1f, 1f));
        }

        /// <summary>
        /// Creates one packed float4 constant-buffer payload from the supplied color.
        /// </summary>
        /// <param name="value">Float4 color value to encode in little-endian order.</param>
        /// <returns>Sixteen-byte packed constant-buffer payload.</returns>
        public static byte[] CreateConstantBufferData(float4 value) {
            byte[] data = new byte[16];
            Array.Copy(BitConverter.GetBytes(value.X.ToSingle()), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(value.Y.ToSingle()), 0, data, 4, 4);
            Array.Copy(BitConverter.GetBytes(value.Z.ToSingle()), 0, data, 8, 4);
            Array.Copy(BitConverter.GetBytes(value.W.ToSingle()), 0, data, 12, 4);
            return data;
        }

        /// <summary>
        /// Creates one material constant-buffer asset for the shared base-color binding from an engine-space colour.
        /// </summary>
        /// <param name="value">Base colour as the engine stores it (gamma/sRGB-encoded channels, straight alpha).</param>
        /// <returns>Material constant-buffer asset for the shared base-color binding.</returns>
        public static MaterialConstantBufferAsset CreateConstantBufferAsset(float4 value) {
            return new MaterialConstantBufferAsset {
                Name = BaseColorBufferName,
                Data = CreateConstantBufferData(value)
            };
        }

        /// <summary>
        /// Reads the colour back out of a packed base-color constant-buffer payload.
        /// </summary>
        /// <param name="data">Sixteen-byte payload written by <see cref="CreateConstantBufferData"/>.</param>
        /// <returns>The colour stored in the payload.</returns>
        public static float4 ReadConstantBufferData(byte[] data) {
            if (data == null || data.Length != 16) {
                throw new ArgumentException("A base-color constant buffer holds exactly sixteen bytes.", nameof(data));
            }

            return new float4(
                BitConverter.ToSingle(data, 0),
                BitConverter.ToSingle(data, 4),
                BitConverter.ToSingle(data, 8),
                BitConverter.ToSingle(data, 12));
        }

        /// <summary>
        /// Converts a linear-light colour (for example a glTF baseColorFactor) to the encoding the engine's base-color buffer uses.
        /// The engine works in gamma space end to end: textures are sampled from UNORM formats, the standard shader multiplies the
        /// sample by the buffer without any transfer function and the swap chain is UNORM, so authored colours (#RRGGBB) are already
        /// sRGB-encoded. A linear colour therefore has to be sRGB-encoded before it is stored; alpha is not colour and stays as is.
        /// </summary>
        /// <param name="linear">Colour with linear red, green and blue and straight alpha.</param>
        /// <returns>Colour with sRGB-encoded red, green and blue and the same alpha.</returns>
        public static float4 LinearToSrgb(float4 linear) {
            return new float4(
                LinearChannelToSrgb(linear.X.ToSingle()),
                LinearChannelToSrgb(linear.Y.ToSingle()),
                LinearChannelToSrgb(linear.Z.ToSingle()),
                linear.W.ToSingle());
        }

        /// <summary>
        /// Applies the IEC 61966-2-1 sRGB transfer function to one linear channel.
        /// </summary>
        /// <param name="linear">Linear channel value; clamped to 0..1.</param>
        /// <returns>sRGB-encoded channel value.</returns>
        static float LinearChannelToSrgb(float linear) {
            float clamped = Math.Min(1f, Math.Max(0f, linear));
            if (clamped <= 0.0031308f) {
                return clamped * 12.92f;
            }

            return 1.055f * (float)Math.Pow(clamped, 1.0 / 2.4) - 0.055f;
        }

        /// <summary>
        /// Creates one authored material constant-buffer asset that seeds the shared standard-material base color to opaque white.
        /// </summary>
        /// <returns>Material constant-buffer asset for the shared base-color binding.</returns>
        public static MaterialConstantBufferAsset CreateWhiteConstantBufferAsset() {
            return new MaterialConstantBufferAsset {
                Name = BaseColorBufferName,
                Data = CreateWhiteConstantBufferData()
            };
        }
    }
}
