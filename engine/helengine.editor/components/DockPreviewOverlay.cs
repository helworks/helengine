namespace helengine.editor {
    public class DockPreviewOverlay : EditorEntity {
        readonly SpriteComponent highlight;

        public DockPreviewOverlay() {
            LayerMask = 0b1000000000000000;
            highlight = new SpriteComponent();
            highlight.Texture = TextureUtils.PixelTexture;
            highlight.Color = new byte4(68, 49, 194, 128); // Semi-transparent accent
            highlight.RenderOrder2D = 240;
            AddComponent(highlight);

            Enabled = false;
        }

        public void Show(float3 position, int2 size) {
            Position = position;
            highlight.Size = size;
            if (!Enabled) {
                Enabled = true;
            }
        }

        public void Hide() {
            if (Enabled) {
                Enabled = false;
            }
        }
    }
}
