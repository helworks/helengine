namespace helengine.editor {
    /// <summary>
    /// Describes one authored text-component bake request captured during scene packaging.
    /// </summary>
    public sealed class TextComponentSpriteBakeRequest {
        /// <summary>
        /// Initializes one immutable bake request for authored text conversion.
        /// </summary>
        /// <param name="componentIndex">Component index within the serialized entity payload.</param>
        /// <param name="targetPlatformId">Target platform whose build is requesting the bake.</param>
        /// <param name="fontReference">Authored scene font reference resolved from the serialized component.</param>
        /// <param name="text">Authored text string that should be rendered.</param>
        /// <param name="size">Authored text layout box that the generated sprite must preserve.</param>
        /// <param name="color">Authored text color.</param>
        /// <param name="wrapText">True when line wrapping is enabled for the authored text.</param>
        /// <param name="fontScale">Authored font scale applied during rendering.</param>
        /// <param name="alignment">Authored horizontal alignment that must be preserved by the bake.</param>
        /// <param name="rotation">Authored rotation that should carry into the replacement sprite.</param>
        /// <param name="renderOrder2D">Authored 2D render order.</param>
        /// <param name="layerMask">Authored layer mask.</param>
        public TextComponentSpriteBakeRequest(
            int componentIndex,
            string targetPlatformId,
            SceneAssetReference fontReference,
            string text,
            int2 size,
            byte4 color,
            bool wrapText,
            float fontScale,
            TextAlignment alignment,
            float rotation,
            byte renderOrder2D,
            byte layerMask) {
            if (fontReference == null) {
                throw new ArgumentNullException(nameof(fontReference));
            }
            if (string.IsNullOrWhiteSpace(targetPlatformId)) {
                throw new ArgumentException("Target platform id must be provided.", nameof(targetPlatformId));
            }

            ComponentIndex = componentIndex;
            TargetPlatformId = targetPlatformId;
            FontReference = fontReference;
            Text = text ?? string.Empty;
            Size = size;
            Color = color;
            WrapText = wrapText;
            FontScale = fontScale;
            Alignment = alignment;
            Rotation = rotation;
            RenderOrder2D = renderOrder2D;
            LayerMask = layerMask;
        }

        /// <summary>
        /// Gets the component index within the authored scene payload.
        /// </summary>
        public int ComponentIndex { get; }

        /// <summary>
        /// Gets the target platform requesting the bake.
        /// </summary>
        public string TargetPlatformId { get; }

        /// <summary>
        /// Gets the authored scene font reference resolved from the serialized text component.
        /// </summary>
        public SceneAssetReference FontReference { get; }

        /// <summary>
        /// Gets the authored text string that should be rendered.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the authored text layout box that the generated sprite must preserve.
        /// </summary>
        public int2 Size { get; }

        /// <summary>
        /// Gets the authored text color.
        /// </summary>
        public byte4 Color { get; }

        /// <summary>
        /// Gets whether the authored text should wrap.
        /// </summary>
        public bool WrapText { get; }

        /// <summary>
        /// Gets the authored font scale applied during rendering.
        /// </summary>
        public float FontScale { get; }

        /// <summary>
        /// Gets the authored horizontal alignment.
        /// </summary>
        public TextAlignment Alignment { get; }

        /// <summary>
        /// Gets the authored rotation carried into the replacement sprite.
        /// </summary>
        public float Rotation { get; }

        /// <summary>
        /// Gets the authored 2D render order.
        /// </summary>
        public byte RenderOrder2D { get; }

        /// <summary>
        /// Gets the authored layer mask.
        /// </summary>
        public byte LayerMask { get; }
    }
}
