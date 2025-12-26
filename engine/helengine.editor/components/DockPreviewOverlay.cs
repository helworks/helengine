namespace helengine.editor {
    /// <summary>
    /// Displays a translucent overlay to preview docking targets during drag operations.
    /// </summary>
    public class DockPreviewOverlay : EditorEntity {
        readonly RoundedRectComponent highlight;

        /// <summary>
        /// Initializes the overlay with a highlight outline and disables it by default.
        /// </summary>
        public DockPreviewOverlay() {
            LayerMask = 0b1000000000000000;
            highlight = new RoundedRectComponent();
            highlight.FillColor = new byte4(0, 0, 0, 0);
            highlight.BorderColor = new byte4(64, 200, 255, 220);
            highlight.BorderThickness = 2f;
            highlight.Radius = 4f;
            highlight.RenderOrder2D = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(3);
            AddComponent(highlight);

            Enabled = false;
        }

        /// <summary>
        /// Shows the overlay at the specified position and size.
        /// </summary>
        /// <param name="position">Top-left position for the overlay.</param>
        /// <param name="size">Size of the overlay highlight.</param>
        public void Show(float3 position, int2 size) {
            Position = new float3(position.X, position.Y, position.Z + 1000f);
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
