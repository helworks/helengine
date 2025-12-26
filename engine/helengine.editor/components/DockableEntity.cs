namespace helengine.editor {
    /// <summary>
    /// Represents an editor window that can be docked or floated and supports drag interactions.
    /// </summary>
    public class DockableEntity : EditorEntity {
        /// <summary>
        /// Height in pixels reserved for the title bar of a dockable entity.
        /// </summary>
        public const int TitleBarHeight = 20;

        /// <summary>
        /// Minimum pointer movement in pixels required before a docked panel undocks.
        /// </summary>
        public const int DragUndockThreshold = 4;
        /// <summary>
        /// Font used by the dockable title bar.
        /// </summary>
        readonly FontAsset font;
        /// <summary>
        /// Render order for panel background surfaces.
        /// </summary>
        readonly byte backgroundOrder;
        /// <summary>
        /// Render order for raised surfaces like title bars.
        /// </summary>
        readonly byte surfaceOrder;
        /// <summary>
        /// Render order for text labels.
        /// </summary>
        readonly byte textOrder;

        bool isDragging;
        bool isPointerDown;
        bool isUndockArmed;
        bool undockRequested;
        /// <summary>
        /// Tracks whether the panel is currently docked.
        /// </summary>
        bool isDocked;
        /// <summary>
        /// Tracks whether the title bar interactable is enabled for drag interactions.
        /// </summary>
        bool titleBarInteractableEnabled;
        int2 pressStartPointer;
        SpriteComponent titleBar;
        SpriteComponent areaSprite;
        InteractableComponent titleBarInteractivity;
        EditorEntity? titleBarText;
        TextComponent? titleTextComponent;
        int2 size;
        /// <summary>
        /// Stores baseline render orders for drawables in this dockable hierarchy.
        /// </summary>
        readonly Dictionary<IDrawable2D, byte> renderOrderBaseline = new Dictionary<IDrawable2D, byte>();

        /// <summary>
        /// Initializes a new dockable entity with title bar, content area, and interaction handlers.
        /// </summary>
        /// <param name="font">Font used to render the title text.</param>
        public DockableEntity(FontAsset font) {
            this.font = font;
            backgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(0);
            surfaceOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);
            LayerMask = 0b1000000000000000;
            isDocked = false;
            titleBarInteractableEnabled = true;
            MinSize = new int2(160, 120);

            titleBar = new SpriteComponent();
            titleBar.Texture = TextureUtils.PixelTexture;
            titleBar.Color = new byte4(194, 49, 175, 255);
            titleBar.RenderOrder2D = surfaceOrder;
            AddComponent(titleBar);

            titleBarText = new EditorEntity();
            titleBarText.Position = new float3(8, 5, 0);
            titleBarText.LayerMask = LayerMask;
            AddChild(titleBarText);
            TextComponent titleComponent = new TextComponent();
            titleComponent.Font = font;
            titleComponent.Text = "dockable entity";
            titleComponent.Color = new byte4(255, 255, 255, 255);
            titleComponent.RenderOrder2D = textOrder;
            titleBarText.AddComponent(titleComponent);
            titleTextComponent = titleComponent;

            EditorEntity sceneViewArea = new EditorEntity();
            sceneViewArea.Position = new float3(0, TitleBarHeight, 0);
            sceneViewArea.LayerMask = LayerMask;
            AddChild(sceneViewArea);
            areaSprite = new SpriteComponent();
            areaSprite.Texture = TextureUtils.PixelTexture;
            areaSprite.Color = new byte4(68, 49, 194, 255);
            areaSprite.RenderOrder2D = backgroundOrder;
            sceneViewArea.AddComponent(areaSprite);

            titleBarInteractivity = new InteractableComponent();
            titleBarInteractivity.Size = new int2(300, TitleBarHeight);
            titleBarInteractivity.CursorEvent += TitleBarInteractivity_CursorEvent;
            AddComponent(titleBarInteractivity);

            Size = new int2(600, 600);
            RefreshRenderOrderBias();
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
                    titleBarInteractivity.Size = titleBarInteractableEnabled
                        ? new int2(value.X, TitleBarHeight)
                        : new int2(0, 0);
                }
                OnSizeChanged();
            }
        }

        /// <summary>
        /// Gets or sets the title shown in the dockable entity title bar.
        /// </summary>
        public string Title {
            get { return titleTextComponent?.Text ?? string.Empty; }
            set {
                if (titleTextComponent != null) {
                    titleTextComponent.Text = value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the entity is currently being dragged.
        /// </summary>
        public bool IsDragging => isDragging;

        /// <summary>
        /// Gets a value indicating whether the entity is currently docked.
        /// </summary>
        public bool IsDocked {
            get { return isDocked; }
            internal set {
                if (isDocked == value) {
                    return;
                }

                isDocked = value;
                ApplyRenderOrderBias();
            }
        }

        /// <summary>
        /// Gets or sets the minimum allowed size for the dockable entity content area.
        /// </summary>
        public int2 MinSize { get; set; }

        /// <summary>
        /// Gets the font used by the dockable title bar.
        /// </summary>
        public FontAsset TitleFont => font;

        /// <summary>
        /// Consumes and clears a pending undock request generated by drag threshold movement.
        /// </summary>
        /// <returns>True if an undock should occur; otherwise false.</returns>
        public bool ConsumeUndockRequest() {
            if (!undockRequested) {
                return false;
            }

            undockRequested = false;
            return true;
        }

        /// <summary>
        /// Handles pointer events on the title bar to manage dragging state and position.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        private void TitleBarInteractivity_CursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Press) {
                isPointerDown = true;
                isUndockArmed = IsDocked;
                pressStartPointer = pos;
                isDragging = !isUndockArmed;
            } else if (state == PointerInteraction.Release || state == PointerInteraction.Leave) {
                isPointerDown = false;
                isUndockArmed = false;
                isDragging = false;
            } else if (state == PointerInteraction.Hover && isPointerDown) {
                if (isDragging) {
                    Position += new float3(delta.X, delta.Y, 0);
                } else if (isUndockArmed) {
                    int dx = pos.X - pressStartPointer.X;
                    int dy = pos.Y - pressStartPointer.Y;
                    int distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= DragUndockThreshold * DragUndockThreshold) {
                        undockRequested = true;
                        IsDocked = false;
                        isUndockArmed = false;
                        isDragging = true;
                        Position += new float3(delta.X, delta.Y, 0);
                    }
                }
            }
        }

        /// <summary>
        /// Invoked after the size changes to allow subclasses to react to layout updates.
        /// </summary>
        protected virtual void OnSizeChanged() {
        }

        /// <summary>
        /// Updates the content background color for the dockable panel body.
        /// </summary>
        /// <param name="color">Color to apply to the content background.</param>
        protected void SetContentBackgroundColor(byte4 color) {
            if (areaSprite != null) {
                areaSprite.Color = color;
            }
        }

        /// <summary>
        /// Shows or hides the title text without changing the stored title.
        /// </summary>
        /// <param name="visible">True to show the title text; false to hide it.</param>
        public void SetTitleTextVisible(bool visible) {
            if (titleBarText != null) {
                titleBarText.Enabled = visible;
            }
        }

        /// <summary>
        /// Enables or disables title bar interaction for drag behavior.
        /// </summary>
        /// <param name="enabled">True to enable title bar hit testing; false to disable it.</param>
        public void SetTitleBarInteractableEnabled(bool enabled) {
            titleBarInteractableEnabled = enabled;
            if (titleBarInteractivity != null) {
                titleBarInteractivity.Size = enabled
                    ? new int2(size.X, TitleBarHeight)
                    : new int2(0, 0);
            }
        }

        /// <summary>
        /// Starts an external drag operation (such as from a tab strip) and requests undocking.
        /// </summary>
        public void BeginExternalDrag() {
            isPointerDown = true;
            isUndockArmed = false;
            undockRequested = true;
            IsDocked = false;
            isDragging = true;
        }

        /// <summary>
        /// Applies a drag delta during an external drag operation.
        /// </summary>
        /// <param name="delta">Pointer delta in pixels.</param>
        public void UpdateExternalDrag(int2 delta) {
            if (!isDragging) {
                return;
            }

            Position += new float3(delta.X, delta.Y, 0);
        }

        /// <summary>
        /// Ends an external drag operation and clears drag state.
        /// </summary>
        public void EndExternalDrag() {
            isPointerDown = false;
            isUndockArmed = false;
            isDragging = false;
        }

        /// <summary>
        /// Refreshes the cached render-order baselines and reapplies docking bias.
        /// </summary>
        protected void RefreshRenderOrderBias() {
            RegisterRenderOrderBaseline(this);
            ApplyRenderOrderBias();
        }

        /// <summary>
        /// Registers baseline render orders for drawables in the provided entity tree.
        /// </summary>
        /// <param name="entity">Root entity to scan.</param>
        void RegisterRenderOrderBaseline(Entity entity) {
            if (entity.Components != null) {
                for (int i = 0; i < entity.Components.Count; i++) {
                    var component = entity.Components[i];
                    if (component is IDrawable2D drawable) {
                        if (!renderOrderBaseline.ContainsKey(drawable)) {
                            renderOrderBaseline[drawable] = drawable.RenderOrder2D;
                        }
                    }
                }
            }

            if (entity.Children != null) {
                for (int i = 0; i < entity.Children.Count; i++) {
                    RegisterRenderOrderBaseline(entity.Children[i]);
                }
            }
        }

        /// <summary>
        /// Applies the docked or undocked render-order bias to registered drawables.
        /// </summary>
        void ApplyRenderOrderBias() {
            if (renderOrderBaseline.Count == 0) {
                RegisterRenderOrderBaseline(this);
            }

            int boost = 0;
            if (!isDocked) {
                boost = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(Core.Instance.ObjectManager.RenderOrderLayers2D - 1);
            }
            foreach (var entry in renderOrderBaseline) {
                int adjusted = entry.Value + boost;
                if (adjusted > byte.MaxValue) {
                    adjusted = byte.MaxValue;
                }
                entry.Key.RenderOrder2D = (byte)adjusted;
            }
        }
    }
}
