namespace helengine.editor {
    /// <summary>
    /// Represents an editor window that can be docked or floated and supports drag interactions.
    /// </summary>
    public class DockableEntity : EditorEntity, IFocusGroup {
        /// <summary>
        /// Height in pixels reserved for the title bar of a dockable entity.
        /// </summary>
        public const int TitleBarHeight = 20;

        /// <summary>
        /// Minimum pointer movement in pixels required before a docked panel undocks.
        /// </summary>
        public const int DragUndockThreshold = 4;
        /// <summary>
        /// Thickness in pixels for the panel outline.
        /// </summary>
        const float PanelOutlineThickness = 1f;
        /// <summary>
        /// Label shown on the panel title-bar menu button.
        /// </summary>
        const string PanelMenuButtonLabel = "...";
        /// <summary>
        /// Semi-transparent near-white color used for dockable panel outlines.
        /// </summary>
        static readonly byte4 PanelOutlineColor = new byte4(255, 255, 255, 72);
        /// <summary>
        /// Shared scaled metrics used to size the dockable chrome.
        /// </summary>
        EditorUiMetrics Metrics;
        /// <summary>
        /// Font used by the dockable title bar.
        /// </summary>
        FontAsset font;
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
        /// <summary>
        /// Outline rendered around the full dockable panel area.
        /// </summary>
        readonly RoundedRectComponent panelOutline;
        /// <summary>
        /// Entity that hosts the dockable title-bar panel menu button.
        /// </summary>
        readonly EditorEntity PanelMenuButtonEntity;
        /// <summary>
        /// Surface rendered behind the panel menu button.
        /// </summary>
        readonly RoundedRectComponent PanelMenuButtonBackground;
        /// <summary>
        /// Entity that hosts the panel menu button label.
        /// </summary>
        readonly EditorEntity PanelMenuButtonTextEntity;
        /// <summary>
        /// Text rendered inside the panel menu button.
        /// </summary>
        readonly TextComponent PanelMenuButtonTextComponent;
        /// <summary>
        /// Interactable used to toggle the panel menu.
        /// </summary>
        readonly InteractableComponent PanelMenuButtonInteractivity;
        /// <summary>
        /// Context menu shown when the panel menu button is activated.
        /// </summary>
        readonly ContextMenu PanelMenu;
        /// <summary>
        /// Items displayed by the panel menu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> PanelMenuItems;
        /// <summary>
        /// Width reserved for the panel menu button.
        /// </summary>
        int PanelMenuButtonWidth;
        /// <summary>
        /// Tracks whether keyboard focus currently marks this dock as the active root group.
        /// </summary>
        bool KeyboardFocusActive;

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
        public DockableEntity(FontAsset font)
            : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new dockable entity with title bar, content area, and interaction handlers using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used to render the title text.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock chrome.</param>
        public DockableEntity(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Metrics = metrics;
            this.font = font;
            backgroundOrder = RenderOrder2D.PanelBackground;
            surfaceOrder = RenderOrder2D.PanelSurface;
            textOrder = RenderOrder2D.PanelForeground;
            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            isDocked = false;
            titleBarInteractableEnabled = true;
            MinSize = new int2(160, 120);

            titleBar = new SpriteComponent();
            titleBar.Texture = TextureUtils.PixelTexture;
            titleBar.Color = new byte4(194, 49, 175, 255);
            titleBar.RenderOrder2D = surfaceOrder;
            AddComponent(titleBar);

            titleBarText = new EditorEntity();
            titleBarText.Position = new float3(Metrics.ScalePixels(8), GetTitleTextTopOffset(), 0);
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
            sceneViewArea.Position = new float3(0, TitleBarHeightPixels, 0);
            sceneViewArea.LayerMask = LayerMask;
            AddChild(sceneViewArea);
            areaSprite = new SpriteComponent();
            areaSprite.Texture = TextureUtils.PixelTexture;
            areaSprite.Color = new byte4(68, 49, 194, 255);
            areaSprite.RenderOrder2D = backgroundOrder;
            sceneViewArea.AddComponent(areaSprite);

            panelOutline = new RoundedRectComponent();
            panelOutline.Radius = 0f;
            panelOutline.FillColor = new byte4(255, 255, 255, 0);
            panelOutline.BorderThickness = PanelOutlineThickness;
            panelOutline.BorderColor = PanelOutlineColor;
            panelOutline.RenderOrder2D = textOrder;
            AddComponent(panelOutline);

            PanelMenuButtonWidth = Math.Max(TitleBarHeightPixels, Metrics.ScalePixels(24));
            PanelMenuButtonEntity = new EditorEntity();
            PanelMenuButtonEntity.LayerMask = LayerMask;
            AddChild(PanelMenuButtonEntity);

            PanelMenuButtonBackground = new RoundedRectComponent();
            PanelMenuButtonBackground.FillColor = new byte4(255, 255, 255, 0);
            PanelMenuButtonBackground.BorderThickness = 0f;
            PanelMenuButtonBackground.BorderColor = new byte4(255, 255, 255, 0);
            PanelMenuButtonBackground.Radius = 0f;
            PanelMenuButtonBackground.RenderOrder2D = surfaceOrder;
            PanelMenuButtonEntity.AddComponent(PanelMenuButtonBackground);

            PanelMenuButtonTextEntity = new EditorEntity();
            PanelMenuButtonTextEntity.LayerMask = LayerMask;
            PanelMenuButtonTextEntity.Position = new float3(Metrics.ScalePixels(5), GetTitleTextTopOffset(), 0f);
            PanelMenuButtonEntity.AddChild(PanelMenuButtonTextEntity);

            PanelMenuButtonTextComponent = new TextComponent();
            PanelMenuButtonTextComponent.Font = font;
            PanelMenuButtonTextComponent.Text = PanelMenuButtonLabel;
            PanelMenuButtonTextComponent.Color = new byte4(255, 255, 255, 255);
            PanelMenuButtonTextComponent.RenderOrder2D = textOrder;
            PanelMenuButtonTextEntity.AddComponent(PanelMenuButtonTextComponent);

            PanelMenuButtonInteractivity = new InteractableComponent();
            PanelMenuButtonInteractivity.CursorEvent += PanelMenuButtonInteractivity_CursorEvent;
            PanelMenuButtonEntity.AddComponent(PanelMenuButtonInteractivity);

            PanelMenu = new ContextMenu(font, LayerMask, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            AddChild(PanelMenu.Entity);
            PanelMenuItems = BuildPanelMenuItems();

            titleBarInteractivity = new InteractableComponent();
            titleBarInteractivity.Size = new int2(300, TitleBarHeightPixels);
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
                titleBar.Size = new int2(value.X, TitleBarHeightPixels);
                areaSprite.Size = new int2(value.X, value.Y);
                panelOutline.Size = new int2(value.X, value.Y + TitleBarHeightPixels);
                PanelMenuButtonEntity.Position = new float3(value.X - PanelMenuButtonWidth, 0f, 0f);
                PanelMenuButtonBackground.Size = new int2(PanelMenuButtonWidth, TitleBarHeightPixels);
                PanelMenuButtonInteractivity.Size = new int2(PanelMenuButtonWidth, TitleBarHeightPixels);
                if (titleBarInteractivity != null) {
                    titleBarInteractivity.Size = titleBarInteractableEnabled
                        ? new int2(GetTitleBarInteractableWidth(), TitleBarHeightPixels)
                        : new int2(0, 0);
                }
                PanelMenu.UpdateLayout(new int2(Math.Max(1, value.X), Math.Max(1, value.Y + TitleBarHeightPixels)));
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
                UpdatePanelOutline();
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
        /// Gets the shared scaled metrics used by the dockable chrome.
        /// </summary>
        public EditorUiMetrics UiMetrics => Metrics;

        /// <summary>
        /// Gets the scaled title-bar height currently used by this dockable instance.
        /// </summary>
        public int TitleBarHeightPixels => Metrics.DockTitleBarHeight;

        /// <summary>
        /// Raised when the user requests to close the current dockable panel.
        /// </summary>
        public event Action CloseRequested;

        /// <summary>
        /// Reapplies the shared dock font and metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated title font used by the dock chrome.</param>
        /// <param name="metrics">Updated scaled editor UI metrics used by the dock chrome.</param>
        public virtual void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            this.font = font;
            Metrics = metrics;

            if (titleTextComponent != null) {
                titleTextComponent.Font = font;
            }
            if (titleBarText != null) {
                titleBarText.Position = new float3(Metrics.ScalePixels(8), GetTitleTextTopOffset(), 0f);
            }
            PanelMenuButtonWidth = Math.Max(TitleBarHeightPixels, Metrics.ScalePixels(24));
            PanelMenuButtonTextEntity.Position = new float3(Metrics.ScalePixels(5), GetTitleTextTopOffset(), 0f);
            PanelMenuButtonTextComponent.Font = font;

            HandleUiMetricsApplied();
            Size = size;
        }

        /// <summary>
        /// Gets the root dock group that owns this group.
        /// </summary>
        public IFocusGroup RootGroup => this;

        /// <summary>
        /// Gets the traversal order within the root dock.
        /// </summary>
        public int GroupOrder => 0;

        /// <summary>
        /// Gets whether this dock may currently participate in keyboard focus.
        /// </summary>
        public bool CanReceiveFocus => Enabled;

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
        /// Allows subclasses to update their own scaled content after the shared dock chrome metrics change.
        /// </summary>
        protected virtual void HandleUiMetricsApplied() {
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
                    ? new int2(GetTitleBarInteractableWidth(), TitleBarHeightPixels)
                    : new int2(0, 0);
            }
        }

        /// <summary>
        /// Activates one panel menu action for test coverage without simulating pointer input.
        /// </summary>
        /// <param name="action">Panel menu action to activate.</param>
        internal void ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction action) {
            if (action == DockableEntityPanelMenuAction.Close) {
                RaiseCloseRequested();
                return;
            }

            throw new InvalidOperationException("The requested panel menu action is not supported.");
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
        /// Returns true when the provided screen point lies inside the dock bounds including the title bar.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the dock bounds.</returns>
        public bool ContainsScreenPoint(int2 point) {
            int left = (int)Math.Round(Position.X);
            int top = (int)Math.Round(Position.Y);
            int width = Size.X;
            int height = Size.Y + TitleBarHeightPixels;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Computes the top offset needed to center the dock title text inside the scaled title bar.
        /// </summary>
        /// <returns>Scaled top offset for the dock title text.</returns>
        float GetTitleTextTopOffset() {
            float lineHeight = Math.Max(font.LineHeight, 1f);
            return (TitleBarHeightPixels - lineHeight) * 0.5f;
        }

        /// <summary>
        /// Gets the width the title-bar drag area may occupy once the panel menu button is reserved.
        /// </summary>
        /// <returns>Title-bar drag width that leaves room for the panel menu button.</returns>
        int GetTitleBarInteractableWidth() {
            return Math.Max(0, size.X - PanelMenuButtonWidth);
        }

        /// <summary>
        /// Creates the items displayed by the dockable panel menu.
        /// </summary>
        /// <returns>Immutable collection of panel menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildPanelMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Close", RaiseCloseRequested)
            };
        }

        /// <summary>
        /// Handles pointer interaction on the panel menu button.
        /// </summary>
        /// <param name="pos">Pointer position relative to the button.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void PanelMenuButtonInteractivity_CursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state != PointerInteraction.Press) {
                return;
            }

            TogglePanelMenu();
        }

        /// <summary>
        /// Shows or hides the panel menu anchored beneath the panel menu button.
        /// </summary>
        void TogglePanelMenu() {
            if (PanelMenu.IsVisible) {
                PanelMenu.Hide();
                return;
            }

            PanelMenu.Show(PanelMenuItems, GetPanelMenuPosition(), new int2(Math.Max(1, size.X), Math.Max(1, size.Y + TitleBarHeightPixels)));
        }

        /// <summary>
        /// Computes the top-left position used to open the panel menu.
        /// </summary>
        /// <returns>Panel menu position relative to the dock root.</returns>
        int2 GetPanelMenuPosition() {
            int x = Math.Max(0, size.X - PanelMenuButtonWidth);
            return new int2(x, TitleBarHeightPixels);
        }

        /// <summary>
        /// Raises the close request event after hiding the panel menu.
        /// </summary>
        void RaiseCloseRequested() {
            PanelMenu.Hide();
            if (CloseRequested != null) {
                CloseRequested();
            }
        }

        /// <summary>
        /// Applies the keyboard-focus active-state visual for this dock.
        /// </summary>
        /// <param name="isActive">True when the dock should appear active.</param>
        public void SetGroupActive(bool isActive) {
            KeyboardFocusActive = isActive;
            UpdatePanelOutline();
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
                boost = RenderOrder2D.FloatingPanelBias;
            }
            foreach (var entry in renderOrderBaseline) {
                int adjusted = entry.Value + boost;
                if (adjusted > byte.MaxValue) {
                    adjusted = byte.MaxValue;
                }
                entry.Key.RenderOrder2D = (byte)adjusted;
            }
        }

        /// <summary>
        /// Updates the panel outline to reflect docking state and keyboard-focus activation.
        /// </summary>
        void UpdatePanelOutline() {
            if (panelOutline == null) {
                return;
            }

            panelOutline.BorderThickness = KeyboardFocusActive || !isDocked
                ? PanelOutlineThickness
                : 0f;
            panelOutline.BorderColor = KeyboardFocusActive
                ? ThemeManager.Colors.AccentPrimary
                : PanelOutlineColor;
        }
    }
}
