namespace helengine.editor {
    /// <summary>
    /// Encodes the constant-buffer payload consumed by the transform-gizmo grid-preview shader.
    /// </summary>
    public static class TransformGizmoGridPreviewParameters {
        /// <summary>
        /// Constant-buffer binding name exposed by the grid-preview shader.
        /// </summary>
        public const string ConstantBufferName = "PreviewParams";
        /// <summary>
        /// Packed constant-buffer size expected by the preview shader.
        /// </summary>
        public const int ConstantBufferSizeInBytes = 16;
        /// <summary>
        /// Mode value used when the preview should render the full grid equally on both axes.
        /// </summary>
        public const float FullGridMode = 0f;
        /// <summary>
        /// Mode value used when the preview should emphasize the dragged axis and fade the companion axis.
        /// </summary>
        public const float SingleAxisFocusMode = 1f;
        /// <summary>
        /// Half-width of the fully visible companion-axis band in local grid units.
        /// </summary>
        public const float CompanionAxisVisibleHalfSpan = 0.5f;
        /// <summary>
        /// Additional falloff distance applied after the visible companion-axis band ends.
        /// </summary>
        public const float CompanionAxisFadeWidth = 0.75f;

        /// <summary>
        /// Builds the constant-buffer payload that renders the full preview grid with no axis deemphasis.
        /// </summary>
        /// <returns>Packed preview-parameter bytes for the full-grid mode.</returns>
        public static byte[] CreateFullGridData() {
            return CreateData(FullGridMode, 0f, 0f, 0f);
        }

        /// <summary>
        /// Builds the constant-buffer payload that emphasizes the dragged axis and fades companion lines away from the origin.
        /// </summary>
        /// <returns>Packed preview-parameter bytes for the focused single-axis mode.</returns>
        public static byte[] CreateSingleAxisFocusData() {
            return CreateData(SingleAxisFocusMode, CompanionAxisVisibleHalfSpan, CompanionAxisFadeWidth, 0f);
        }

        /// <summary>
        /// Writes one full-grid parameter payload to the supplied runtime material.
        /// </summary>
        /// <param name="material">Preview material whose constant-buffer payload should be updated.</param>
        public static void ApplyFullGrid(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            material.Properties.SetConstantBufferData(ConstantBufferName, CreateFullGridData());
        }

        /// <summary>
        /// Writes one focused single-axis payload to the supplied runtime material.
        /// </summary>
        /// <param name="material">Preview material whose constant-buffer payload should be updated.</param>
        public static void ApplySingleAxisFocus(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            material.Properties.SetConstantBufferData(ConstantBufferName, CreateSingleAxisFocusData());
        }

        /// <summary>
        /// Packs four single-precision values into the byte layout expected by the preview shader.
        /// </summary>
        /// <param name="first">First single-precision value.</param>
        /// <param name="second">Second single-precision value.</param>
        /// <param name="third">Third single-precision value.</param>
        /// <param name="fourth">Fourth single-precision value.</param>
        /// <returns>Packed constant-buffer payload.</returns>
        static byte[] CreateData(float first, float second, float third, float fourth) {
            float[] values = new[] { first, second, third, fourth };
            byte[] data = new byte[ConstantBufferSizeInBytes];
            Buffer.BlockCopy(values, 0, data, 0, ConstantBufferSizeInBytes);
            return data;
        }
    }
}
