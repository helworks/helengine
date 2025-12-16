namespace helengine.editor {
    /// <summary>
    /// Represents an editor window that can be docked or floated and supports drag interactions.
    /// </summary>
    public class DockableEntity : EditorEntity {
        /// <summary>
        /// Height in pixels reserved for the title bar of a dockable entity.
        /// </summary>
        public const int TitleBarHeight = 20;

        bool isDragging;
        SpriteComponent titleBar;
        SpriteComponent areaSprite;
        InteractableComponent titleBarInteractivity;
        EditorEntity? titleBarText;
        int2 size;

        /// <summary>
        /// Initializes a new dockable entity with title bar, content area, and interaction handlers.
        /// </summary>
        /// <param name="font">Font used to render the title text.</param>
        public DockableEntity(FontAsset font) {
            LayerMask = 0b1000000000000000;
            Dock = DockRegion.Floating;
            MinSize = new int2(160, 120);

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
            sceneViewArea.Position = new float3(0, TitleBarHeight, 0);
            sceneViewArea.LayerMask = LayerMask;
            AddChild(sceneViewArea);
            areaSprite = new SpriteComponent();
            areaSprite.Texture = TextureUtils.PixelTexture;
            areaSprite.Color = new byte4(68, 49, 194, 255);
            sceneViewArea.AddComponent(areaSprite);

            titleBarInteractivity = new InteractableComponent();
            titleBarInteractivity.Size = new int2(300, TitleBarHeight);
            titleBarInteractivity.CursorEvent += TitleBarInteractivity_CursorEvent;
            AddComponent(titleBarInteractivity);

            Size = new int2(600, 600);
        }

        /// <summary>
        /// Gets or sets the overall size of the dockable entity, updating visuals accordingly.
        /// </summary>
        public int2 Size {
            get { return size; }
            set {
                size = value;
                titleBar.Size = new int2(value.X, TitleBarHeight);
                areaSprite.Size = new int2(value.X, value.Y);
                if (titleBarInteractivity != null) {
                    titleBarInteractivity.Size = new int2(value.X, TitleBarHeight);
                }
                OnSizeChanged();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the entity is currently being dragged.
        /// </summary>
        public bool IsDragging => isDragging;

        /// <summary>
        /// Gets or sets the minimum allowed size for the dockable entity content area.
        /// </summary>
        public int2 MinSize { get; set; }

        /// <summary>
        /// Gets or sets the current docking region for the entity.
        /// </summary>
        public DockRegion Dock { get; set; }

        /// <summary>
        /// Handles pointer events on the title bar to manage dragging state and position.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        private void TitleBarInteractivity_CursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Press) {
                if (Dock != DockRegion.Floating) {
                    Dock = DockRegion.Floating;
                }
                isDragging = true;
            } else if (state == PointerInteraction.Release) {
                isDragging = false;
            } else if (state == PointerInteraction.Hover && isDragging) {
                Position += new float3(delta.X, delta.Y, 0);
            }
        }

        /// <summary>
        /// Invoked after the size changes to allow subclasses to react to layout updates.
        /// </summary>
        protected virtual void OnSizeChanged() {
        }
    }
}
