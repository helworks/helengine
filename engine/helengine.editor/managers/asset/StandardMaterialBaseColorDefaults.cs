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
            Array.Copy(BitConverter.GetBytes(value.X), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(value.Y), 0, data, 4, 4);
            Array.Copy(BitConverter.GetBytes(value.Z), 0, data, 8, 4);
            Array.Copy(BitConverter.GetBytes(value.W), 0, data, 12, 4);
            return data;
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
