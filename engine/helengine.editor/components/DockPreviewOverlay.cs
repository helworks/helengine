namespace helengine.editor {
    /// <summary>
    /// Displays a translucent overlay to preview docking targets during drag operations.
    /// </summary>
    public class DockPreviewOverlay : EditorEntity {
        readonly SpriteComponent highlight;

        /// <summary>
        /// Initializes the overlay with a highlight sprite and disables it by default.
        /// </summary>
        public DockPreviewOverlay() {
            LayerMask = 0b1000000000000000;
            highlight = new SpriteComponent();
            highlight.Texture = TextureUtils.PixelTexture;
            highlight.Color = new byte4(68, 49, 194, 128); // Semi-transparent accent
            highlight.RenderOrder2D = 240;
            AddComponent(highlight);

            Enabled = false;
        }

        /// <summary>
        /// Shows the overlay at the specified position and size.
        /// </summary>
        /// <param name="position">Top-left position for the overlay.</param>
        /// <param name="size">Size of the overlay highlight.</param>
        public void Show(float3 position, int2 size) {
            Position = position;
            highlight.Size = size;
            if (!Enabled) {
                Enabled = true;
            }
        }

        /// <summary>
        /// Hides the overlay if it is currently visible.
        /// </summary>
        public void Hide() {
            if (Enabled) {
                Enabled = false;
            }
        }
    }
}
