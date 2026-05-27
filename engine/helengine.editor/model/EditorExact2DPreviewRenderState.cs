namespace helengine.editor {
    /// <summary>
    /// Captures only the texture-affecting state for one editor-only exact 2D world preview.
    /// </summary>
    public sealed class EditorExact2DPreviewRenderState {
        /// <summary>
        /// Initializes one render-state snapshot for the supplied preview source type.
        /// </summary>
        /// <param name="previewSourceTypeName">Stable source type name used to guard cross-type comparisons.</param>
        public EditorExact2DPreviewRenderState(string previewSourceTypeName) {
            if (string.IsNullOrWhiteSpace(previewSourceTypeName)) {
                throw new ArgumentException("Preview source type name is required.", nameof(previewSourceTypeName));
            }

            PreviewSourceTypeName = previewSourceTypeName;
            Text = string.Empty;
        }

        /// <summary>
        /// Gets the stable source type name for this snapshot.
        /// </summary>
        public string PreviewSourceTypeName { get; }

        /// <summary>
        /// Gets or sets the backing runtime texture that participates in preview generation when present.
        /// </summary>
        public RuntimeTexture Texture { get; set; }

        /// <summary>
        /// Gets or sets the font asset used by text preview rendering.
        /// </summary>
        public FontAsset Font { get; set; }

        /// <summary>
        /// Gets or sets the rendered text content.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the texture source rectangle used by the preview.
        /// </summary>
        public float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the preview texture width and height in local 2D units.
        /// </summary>
        public int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the primary tint color applied during preview rendering.
        /// </summary>
        public byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets whether text wrapping participates in preview generation.
        /// </summary>
        public bool WrapText { get; set; }

        /// <summary>
        /// Gets or sets the font scale applied during text preview rendering.
        /// </summary>
        public float FontScale { get; set; }

        /// <summary>
        /// Gets or sets the horizontal alignment applied during text preview rendering.
        /// </summary>
        public TextAlignment Alignment { get; set; }

        /// <summary>
        /// Gets or sets the local component rotation baked into the preview texture.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Gets or sets the rounded-rectangle corner radius.
        /// </summary>
        public float Radius { get; set; }

        /// <summary>
        /// Gets or sets the rounded-rectangle border thickness.
        /// </summary>
        public float BorderThickness { get; set; }

        /// <summary>
        /// Gets or sets the rounded-rectangle fill color.
        /// </summary>
        public byte4 FillColor { get; set; }

        /// <summary>
        /// Gets or sets the rounded-rectangle border color.
        /// </summary>
        public byte4 BorderColor { get; set; }

        /// <summary>
        /// Gets or sets the rounded-rectangle corner mask.
        /// </summary>
        public RoundedRectCorners Corners { get; set; }
    }
}
