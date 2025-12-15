namespace helengine.editor {
    public class DockableEntity : EditorEntity {
        bool isDragging;

        SpriteComponent titleBar;
        SpriteComponent areaSprite;
        InteractableComponent titleBarInteractivity;
        EditorEntity? titleBarText;
        int2 size;

        public int2 Size {
            get { return size; }
            set {
                size = value;
                titleBar.Size = new int2(value.X, 20);
                areaSprite.Size = new int2(value.X, value.Y);
                if (titleBarInteractivity != null) {
                    titleBarInteractivity.Size = new int2(value.X, 20);
                }
            }
        }

        public DockableEntity(FontAsset font) {
            LayerMask = 0b1000000000000000;

            titleBar = new SpriteComponent();
            titleBar.Texture = TextureUtils.PixelTexture;
            titleBar.Color = new byte4(194, 49, 175, 255);
            AddComponent(titleBar);

            titleBarText = new EditorEntity();
            titleBarText.Position = new float3(8, 5, 0);
            titleBarText.LayerMask = LayerMask;
            AddChild(titleBarText);
            TextComponent titleComponent = new TextComponent();
            titleComponent.Font = font;
            titleComponent.Text = "dockable entity";
            titleComponent.Color = new byte4(255, 255, 255, 255);
            titleBarText.AddComponent(titleComponent);

            EditorEntity sceneViewArea = new EditorEntity();
            sceneViewArea.Position = new float3(0, 20, 0);
            sceneViewArea.LayerMask = LayerMask;
            AddChild(sceneViewArea);
            areaSprite = new SpriteComponent();
            areaSprite.Texture = TextureUtils.PixelTexture;
            areaSprite.Color = new byte4(68, 49, 194, 255);
            sceneViewArea.AddComponent(areaSprite);

            titleBarInteractivity = new InteractableComponent();
            titleBarInteractivity.Size = new int2(300, 20);
            titleBarInteractivity.CursorEvent += TitleBarInteractivity_CursorEvent;
            AddComponent(titleBarInteractivity);

            Size = new int2(600, 600);
        }

        private void TitleBarInteractivity_CursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Press) {
                if (!isDragging) {
                    isDragging = true;
                }
            } else if (state == PointerInteraction.Release) {
                isDragging = false;
            } else {
                if (isDragging) {
                    Position += new float3(delta.X, delta.Y, 0);
                }
            }
        }
    }
}
