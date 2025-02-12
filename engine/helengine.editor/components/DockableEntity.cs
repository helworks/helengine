namespace helengine.editor {
    public class DockableEntity : EditorEntity {
        bool isDragging;

        public DockableEntity() {
            LayerMask = 0b1000000000000000;

            SpriteComponent titleBar = new SpriteComponent();
            titleBar.Texture = TextureUtils.PixelTexture;
            titleBar.Size = new int2(300, 20);
            titleBar.Color = new byte4(194, 49, 175, 255);
            AddComponent(titleBar);

            EditorEntity sceneViewArea = new EditorEntity();
            sceneViewArea.Position = new float3(0, 20, 0);
            sceneViewArea.LayerMask = LayerMask;
            AddChild(sceneViewArea);

            SpriteComponent areaSprite = new SpriteComponent();
            areaSprite.Texture = TextureUtils.PixelTexture;
            areaSprite.Size = new int2(300, 300);
            areaSprite.Color = new byte4(68, 49, 194, 255);
            sceneViewArea.AddComponent(areaSprite);

            InteractableComponent titleBarInteractivity = new InteractableComponent();
            titleBarInteractivity.Size = new int2(300, 20);
            titleBarInteractivity.CursorEvent += TitleBarInteractivity_CursorEvent;
            AddComponent(titleBarInteractivity);
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
