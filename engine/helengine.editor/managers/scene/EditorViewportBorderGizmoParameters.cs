namespace helengine.editor {
    /// <summary>
    /// Encodes the constant-buffer payload consumed by the editor viewport-border gizmo shader.
    /// </summary>
    public static class EditorViewportBorderGizmoParameters {
        /// <summary>
        /// Constant-buffer binding name exposed by the viewport-border shader.
        /// </summary>
        public const string ConstantBufferName = "BorderParams";

        /// <summary>
        /// Packed constant-buffer size expected by the viewport-border shader.
        /// </summary>
        public const int ConstantBufferSizeInBytes = 32;

        /// <summary>
        /// Fixed border thickness in world units used by editor viewport gizmos.
        /// </summary>
        public const double BorderThicknessWorldUnits = 2d;

        /// <summary>
        /// Color used by authored viewport border gizmos in scene view.
        /// </summary>
        public static readonly float4 BorderColor = new float4(0.9843137f, 0.8156863f, 0.23137255f, 1f);

        /// <summary>
        /// Writes one viewport-border parameter payload to the supplied runtime material.
        /// </summary>
        /// <param name="material">Material whose constant-buffer data should be updated.</param>
        /// <param name="width">Resolved viewport width in world units.</param>
        /// <param name="height">Resolved viewport height in world units.</param>
        public static void Apply(RuntimeMaterial material, int width, int height) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            } else if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width));
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            material.Properties.SetConstantBufferData(ConstantBufferName, CreateData(width, height));
        }

        /// <summary>
        /// Packs the viewport-border payload bytes for the supplied resolved viewport size.
        /// </summary>
        /// <param name="width">Resolved viewport width in world units.</param>
        /// <param name="height">Resolved viewport height in world units.</param>
        /// <returns>Packed constant-buffer payload expected by the viewport-border shader.</returns>
        public static byte[] CreateData(int width, int height) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width));
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            float thicknessU = (float)Math.Min(0.5d, BorderThicknessWorldUnits / width);
            float thicknessV = (float)Math.Min(0.5d, BorderThicknessWorldUnits / height);
            byte[] data = new byte[ConstantBufferSizeInBytes];
            WriteFloat(data, 0, BorderColor.X);
            WriteFloat(data, 4, BorderColor.Y);
            WriteFloat(data, 8, BorderColor.Z);
            WriteFloat(data, 12, BorderColor.W);
            WriteFloat(data, 16, thicknessU);
            WriteFloat(data, 20, thicknessV);
            WriteFloat(data, 24, 0f);
            WriteFloat(data, 28, 0f);
            return data;
        }

        /// <summary>
        /// Writes one single-precision value into the supplied byte array at the requested offset.
        /// </summary>
        /// <param name="buffer">Destination byte array that stores the packed constant-buffer data.</param>
        /// <param name="offset">Byte offset where the floating-point value should be written.</param>
        /// <param name="value">Single-precision value to pack.</param>
        static void WriteFloat(byte[] buffer, int offset, float value) {
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }
    }
}
