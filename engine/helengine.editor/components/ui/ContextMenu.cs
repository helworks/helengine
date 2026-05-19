namespace helengine.editor {
    /// <summary>
    /// Provides a reusable context menu UI for editor panels.
    /// </summary>
    public class ContextMenu {
        /// <summary>
        /// Tracks visible context menus so one open submenu does not dismiss another active menu.
        /// </summary>
        static readonly List<ContextMenu> VisibleMenus = new List<ContextMenu>();

        /// <summary>
        /// Backing value for the shared submenu indicator appended to rows that open another menu.
        /// </summary>
        static string SubmenuIndicatorValue = "v";

        /// <summary>
        /// Horizontal gap preserved between the row label and the right-aligned submenu indicator.
        /// </summary>
        const int SubmenuIndicatorGap = 12;

        /// <summary>
        /// Height of each menu row in pixels.
        /// </summary>
        public const int RowHeight = 24;
        /// <summary>
        /// Spacing between rows in pixels.
        /// </summary>
        public const int RowSpacing = 0;
        /// <summary>
        /// Horizontal padding inside the menu.
        /// </summary>
        public const int PaddingX = 12;
        /// <summary>
        /// Vertical padding inside the menu.
        /// </summary>
        public const int PaddingY = 8;
        /// <summary>
        /// Minimum menu width in pixels.
        /// </summary>
        public const int MinWidth = 160;
        /// <summary>
        /// Maximum menu width in pixels.
        /// </summary>
        public const int MaxWidth = 360;
        /// <summary>
        /// Radius applied to the menu background.
        /// </summary>
        const float BackgroundRadius = 4f;
        /// <summary>
        /// Border thickness applied to the menu background.
        /// </summary>
        const float BackgroundBorderThickness = 1f;

        /// <summary>
        /// Font used for menu labels.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity for the context menu.
        /// </summary>
        readonly EditorEntity Root;
        /// <summary>
        /// Background shape for the menu.
        /// </summary>
        readonly RoundedRectComponent Background;
        /// <summary>
        /// Entity that blocks pointer events from leaking through menu padding and row spacing.
        /// </summary>
        readonly EditorEntity BackgroundBlockerEntity;
        /// <summary>
        /// Transparent surface that keeps the background blocker in the menu overlay band without outranking the row content.
        /// </summary>
        readonly SpriteComponent BackgroundBlockerSurface;
        /// <summary>
        /// Interactable that blocks pointer events from leaking through menu padding and row spacing.
        /// </summary>
        readonly InteractableComponent BackgroundBlockerInteractable;
        /// <summary>
        /// Render order used for menu text.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Row pool used to display menu items.
        /// </summary>
        readonly List<ContextMenuRow> Rows;
        /// <summary>
        /// Active items assigned to the menu.
        /// </summary>
        readonly List<ContextMenuItem> ActiveItems;
        /// <summary>
        /// Scroll controller used when the menu contains more items than the host can display at once.
        /// </summary>
        readonly ScrollComponent ScrollComponent;
        /// <summary>
        /// Cached menu size.
        /// </summary>
        int2 MenuSize;
        /// <summary>
        /// Cached menu position relative to the parent.
        /// </summary>
        int2 MenuPosition;
        /// <summary>
        /// Cached host size used for clamping.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// Tracks whether the menu has finished initialization.
        /// </summary>
        bool IsInitialized;
        /// <summary>
        /// Index of the first active item currently rendered by the visible row pool.
        /// </summary>
        int FirstVisibleItemIndex;
        /// <summary>
        /// Tracks whether the menu is already forcing a disable operation.
        /// </summary>
        bool IsForcingDisabled;

        /// <summary>
        /// Initializes a new context menu using the provided font and render orders.
        /// </summary>
        /// <param name="font">Font used for menu labels.</param>
        /// <param name="layerMask">Layer mask for menu entities.</param>
        /// <param name="backgroundOrder">Render order for menu backgrounds.</param>
        /// <param name="textOrder">Render order for menu text.</param>
        public ContextMenu(FontAsset font, ushort layerMask, byte backgroundOrder, byte textOrder) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            TextOrder = textOrder;
            Rows = new List<ContextMenuRow>(8);
            ActiveItems = new List<ContextMenuItem>(8);
            ScrollComponent = new ScrollComponent();
            MenuSize = new int2(0, 0);
            MenuPosition = new int2(0, 0);
            HostSize = new int2(1, 1);

            Root = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask,
                Position = float3.Zero,
                Enabled = false
            };

            Background = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = BackgroundBorderThickness,
                Radius = BackgroundRadius,
                RenderOrder2D = backgroundOrder,
                Size = new int2(0, 0)
            };
            Root.AddComponent(Background);

            byte blockerOrder = backgroundOrder > 0 ? (byte)(backgroundOrder - 1) : backgroundOrder;
            BackgroundBlockerEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask,
                Position = new float3(0f, 0f, 0.05f)
            };
            Root.AddChild(BackgroundBlockerEntity);

            BackgroundBlockerSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(255, 255, 255, 0),
                Size = new int2(0, 0),
                RenderOrder2D = blockerOrder
            };
            BackgroundBlockerEntity.AddComponent(BackgroundBlockerSurface);

            BackgroundBlockerInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            BackgroundBlockerEntity.AddComponent(BackgroundBlockerInteractable);

            ScrollComponent.Size = new int2(0, 0);
            ScrollComponent.ItemExtent = Math.Max(1, RowHeight + RowSpacing);
            ScrollComponent.VisibleItemCount = 1;
            ScrollComponent.ScrollOffsetChanged += HandleScrollOffsetChanged;
            Root.AddComponent(ScrollComponent);

            Root.AddComponent(new ContextMenuUpdater(this));
            IsInitialized = true;
        }

        /// <summary>
        /// Gets the root entity for the context menu.
        /// </summary>
        public EditorEntity Entity => Root;

        /// <summary>
        /// Gets or sets the shared text appended to rows that open another menu.
        /// </summary>
        public static string SubmenuIndicator {
            get { return SubmenuIndicatorValue; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                SubmenuIndicatorValue = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the menu is currently visible.
        /// </summary>
        public bool IsVisible => Root.Enabled;
        /// <summary>
        /// Gets the last computed menu position relative to the parent.
        /// </summary>
        public int2 Position => MenuPosition;
        /// <summary>
        /// Gets the last computed menu size.
        /// </summary>
        public int2 Size => MenuSize;

        /// <summary>
        /// Shows the menu with the provided items at the given position.
        /// </summary>
        /// <param name="items">Items to display.</param>
        /// <param name="position">Position relative to the parent.</param>
        /// <param name="hostSize">Size of the host region for clamping.</param>
        public void Show(IReadOnlyList<ContextMenuItem> items, int2 position, int2 hostSize) {
            if (!IsInitialized) {
                return;
            }
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0) {
                Hide();
                return;
            }

            ActiveItems.Clear();
            for (int i = 0; i < items.Count; i++) {
                ContextMenuItem item = items[i];
                if (item == null) {
                    continue;
                }
                ActiveItems.Add(item);
            }

            if (ActiveItems.Count == 0) {
                Hide();
                return;
            }

            HostSize = hostSize;
            MenuPosition = position;
            ScrollComponent.ResetScrollOffset();
            FirstVisibleItemIndex = 0;
            UpdateLayoutInternal();
            Root.Enabled = true;
            RegisterVisibleMenu();
            UpdateInputBlocker();
        }

        /// <summary>
        /// Hides the menu and clears its input blocker.
        /// </summary>
        public void Hide() {
            Root.Enabled = false;
            UnregisterVisibleMenu();
            EditorInputCaptureService.ClearBlocker(this);
        }

        /// <summary>
        /// Updates the menu layout using the provided host size.
        /// </summary>
        /// <param name="hostSize">Size of the host region.</param>
        public void UpdateLayout(int2 hostSize) {
            if (!IsInitialized) {
                return;
            }

            HostSize = hostSize;
            if (!Root.Enabled) {
                return;
            }

            UpdateLayoutInternal();
            UpdateInputBlocker();
        }

        /// <summary>
        /// Updates the menu each frame to handle dismissal input.
        /// </summary>
        public void Update() {
            if (!Root.Enabled) {
                return;
            }

            UpdateInputBlocker();

            InputSystem input = Core.Instance.Input;
            bool leftPressed = input.WasMouseLeftButtonPressed();
            bool rightPressed = input.WasMouseRightButtonPressed();
            if (!leftPressed && !rightPressed) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (!IsPointerInside(pointer) && !IsPointerInsideAnyVisibleMenu(pointer)) {
                Hide();
            }
        }

        /// <summary>
        /// Forces the menu to disable without re-entering disable logic.
        /// </summary>
        public void ForceDisable() {
            if (IsForcingDisabled) {
                return;
            }

            IsForcingDisabled = true;
            try {
                Root.Enabled = false;
            } finally {
                IsForcingDisabled = false;
            }

            UnregisterVisibleMenu();
            EditorInputCaptureService.ClearBlocker(this);
        }

        /// <summary>
        /// Ensures the row pool can display the requested number of items.
        /// </summary>
        /// <param name="count">Number of rows required.</param>
        void EnsureRowCount(int count) {
            for (int i = Rows.Count; i < count; i++) {
                Rows.Add(CreateRow());
            }
        }

        /// <summary>
        /// Creates a new row and wires its activation event.
        /// </summary>
        /// <returns>New context menu row.</returns>
        ContextMenuRow CreateRow() {
            var rowEntity = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = float3.Zero
            };
            Root.AddChild(rowEntity);

            var background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = Background.RenderOrder2D
            };
            rowEntity.AddComponent(background);

            var labelHost = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = float3.Zero
            };
            rowEntity.AddChild(labelHost);

            var label = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
                RenderOrder2D = TextOrder
            };
            labelHost.AddComponent(label);

            var indicatorHost = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = float3.Zero
            };
            rowEntity.AddChild(indicatorHost);

            var indicator = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
                RenderOrder2D = TextOrder
            };
            indicatorHost.AddComponent(indicator);

            var interactable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            rowEntity.AddComponent(interactable);

            var row = new ContextMenuRow(rowEntity, background, labelHost, label, indicatorHost, indicator, interactable);
            row.Activated += HandleRowActivated;
            row.Hovered += HandleRowHovered;
            return row;
        }

        /// <summary>
        /// Applies active item data to the row pool.
        /// </summary>
        void ApplyItems() {
            for (int i = 0; i < Rows.Count; i++) {
                ContextMenuRow row = Rows[i];
                int itemIndex = FirstVisibleItemIndex + i;
                if (itemIndex >= ActiveItems.Count) {
                    row.Entity.Enabled = false;
                    row.Item = null;
                    continue;
                }

                ContextMenuItem item = ActiveItems[itemIndex];
                bool itemChanged = !ReferenceEquals(row.Item, item);
                row.Entity.Enabled = true;
                row.Item = item;
                row.Label.Text = item.Label;
                row.Indicator.Text = GetIndicatorLabel(item);
                row.IndicatorHost.Enabled = !string.IsNullOrEmpty(row.Indicator.Text);
                if (itemChanged) {
                    row.ResetState();
                } else {
                    row.UpdateBackground();
                }
            }
        }

        /// <summary>
        /// Updates size, position, and row layout for the menu.
        /// </summary>
        void UpdateLayoutInternal() {
            int width = ComputeMenuWidth();
            int visibleRowCount = ResolveVisibleRowCount();
            EnsureRowCount(visibleRowCount);
            EditorScrollComponentLayout.ConfigureExplicitVisibleItems(
                ScrollComponent,
                new int2(width, ComputeMenuHeight(visibleRowCount)),
                Math.Max(1, RowHeight + RowSpacing),
                ActiveItems.Count,
                visibleRowCount);
            ScrollComponent.ClampScrollOffset();
            FirstVisibleItemIndex = ScrollComponent.ScrollOffset;
            MenuSize = new int2(width, ComputeMenuHeight(visibleRowCount));
            MenuPosition = ClampPosition(MenuPosition, MenuSize, HostSize);
            Root.Position = new float3(MenuPosition.X, MenuPosition.Y, 0.2f);

            Background.Size = MenuSize;
            BackgroundBlockerSurface.Size = MenuSize;
            BackgroundBlockerInteractable.Size = MenuSize;
            ApplyItems();
            LayoutRows();
        }

        /// <summary>
        /// Lays out rows within the menu based on the current size.
        /// </summary>
        void LayoutRows() {
            int y = PaddingY;
            for (int i = 0; i < Rows.Count; i++) {
                ContextMenuRow row = Rows[i];
                if (!row.Entity.Enabled) {
                    continue;
                }

                row.Entity.Position = new float3(0, y, 0.1f);
                row.Background.Size = new int2(MenuSize.X, RowHeight);
                row.Interactable.Size = new int2(MenuSize.X, RowHeight);

                var labelMetrics = Font.MeasureTight(row.Label.Text ?? string.Empty);
                float labelY = GetTextTopOffset(RowHeight, labelMetrics);
                row.LabelHost.Position = new float3(PaddingX, labelY, 0.2f);
                int labelWidth = Math.Max(0, MenuSize.X - PaddingX * 2);
                if (row.IndicatorHost.Enabled) {
                    var indicatorMetrics = Font.MeasureTight(row.Indicator.Text ?? string.Empty);
                    double indicatorX = MenuSize.X - PaddingX - indicatorMetrics.Width;
                    float indicatorY = GetTextTopOffset(RowHeight, indicatorMetrics);
                    row.IndicatorHost.Position = new float3((float)indicatorX, indicatorY, 0.2f);
                    row.Indicator.Size = new int2((int)Math.Ceiling(indicatorMetrics.Width), (int)Math.Ceiling(indicatorMetrics.Height));
                    labelWidth = Math.Max(0, (int)Math.Floor(indicatorX - PaddingX - SubmenuIndicatorGap));
                } else {
                    row.IndicatorHost.Position = new float3(MenuSize.X - PaddingX, labelY, 0.2f);
                    row.Indicator.Size = new int2(0, 0);
                }

                row.Label.Size = new int2(labelWidth, (int)Math.Ceiling(labelMetrics.Height));

                y += RowHeight + RowSpacing;
            }
        }

        /// <summary>
        /// Computes the menu size based on the current items.
        /// </summary>
        /// <returns>Calculated menu size.</returns>
        int ComputeMenuWidth() {
            int width = MinWidth;
            for (int i = 0; i < ActiveItems.Count; i++) {
                string label = ActiveItems[i].Label ?? string.Empty;
                var labelMetrics = Font.MeasureTight(label);
                int required = (int)Math.Ceiling(labelMetrics.Width) + (PaddingX * 2);
                string indicator = GetIndicatorLabel(ActiveItems[i]);
                if (!string.IsNullOrEmpty(indicator)) {
                    var indicatorMetrics = Font.MeasureTight(indicator);
                    required += SubmenuIndicatorGap + (int)Math.Ceiling(indicatorMetrics.Width);
                }
                if (required > width) {
                    width = required;
                }
            }

            if (width > MaxWidth) {
                width = MaxWidth;
            }

            return width;
        }

        /// <summary>
        /// Computes the height required for the supplied number of visible rows.
        /// </summary>
        /// <param name="visibleRowCount">Number of visible rows to include in the menu body.</param>
        /// <returns>Calculated menu height.</returns>
        int ComputeMenuHeight(int visibleRowCount) {
            int height = PaddingY * 2;
            if (visibleRowCount > 0) {
                height += (RowHeight * visibleRowCount) + (RowSpacing * (visibleRowCount - 1));
            }

            if (height < RowHeight + (PaddingY * 2)) {
                height = RowHeight + (PaddingY * 2);
            }

            return height;
        }

        /// <summary>
        /// Resolves the number of visible rows that fit inside the current host height.
        /// </summary>
        /// <returns>Visible row count for the current menu host.</returns>
        int ResolveVisibleRowCount() {
            if (ActiveItems.Count <= 0) {
                return 0;
            }

            int availableHeight = Math.Max(0, HostSize.Y - (PaddingY * 2));
            int rowStride = RowHeight + RowSpacing;
            int visibleRowCount = rowStride <= 0 ? ActiveItems.Count : (availableHeight + rowStride - 1) / rowStride;
            if (visibleRowCount < 1) {
                visibleRowCount = 1;
            }
            if (visibleRowCount > ActiveItems.Count) {
                visibleRowCount = ActiveItems.Count;
            }

            return visibleRowCount;
        }

        /// <summary>
        /// Gets the rendered submenu indicator text for one menu item.
        /// </summary>
        /// <param name="item">Menu item whose display label should be built.</param>
        /// <returns>Rendered submenu indicator text for the row.</returns>
        string GetIndicatorLabel(ContextMenuItem item) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            if (!item.OpensSubmenu || string.IsNullOrEmpty(SubmenuIndicator)) {
                return string.Empty;
            }

            return SubmenuIndicator;
        }

        /// <summary>
        /// Clamps a menu position to remain within the host bounds.
        /// </summary>
        /// <param name="position">Proposed position.</param>
        /// <param name="size">Menu size.</param>
        /// <param name="hostSize">Host size.</param>
        /// <returns>Clamped position.</returns>
        int2 ClampPosition(int2 position, int2 size, int2 hostSize) {
            int maxX = Math.Max(0, hostSize.X - size.X);
            int maxY = Math.Max(0, hostSize.Y - size.Y);

            int clampedX = position.X;
            if (clampedX < 0) {
                clampedX = 0;
            } else if (clampedX > maxX) {
                clampedX = maxX;
            }

            int clampedY = position.Y;
            if (clampedY < 0) {
                clampedY = 0;
            } else if (clampedY > maxY) {
                clampedY = maxY;
            }

            return new int2(clampedX, clampedY);
        }

        /// <summary>
        /// Handles activation events from menu rows.
        /// </summary>
        /// <param name="row">Row that was activated.</param>
        void HandleRowActivated(ContextMenuRow row) {
            if (row == null || row.Item == null) {
                return;
            }

            ContextMenuItem item = row.Item;
            if (item.CloseOnActivate) {
                Hide();
            }
            item.Action();
        }

        /// <summary>
        /// Handles hover events from menu rows.
        /// </summary>
        /// <param name="row">Row that is hovered.</param>
        void HandleRowHovered(ContextMenuRow row) {
            if (row == null || row.Item == null) {
                return;
            }

            ContextMenuItem item = row.Item;
            if (item.HoverAction != null) {
                item.HoverAction();
            }
        }

        /// <summary>
        /// Rebinds the visible row window when the menu scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll controller that raised the event.</param>
        /// <param name="scrollOffset">Current item-window offset.</param>
        void HandleScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            FirstVisibleItemIndex = scrollOffset;
            ApplyItems();
            LayoutRows();
        }

        /// <summary>
        /// Updates the input blocker to match the menu bounds.
        /// </summary>
        void UpdateInputBlocker() {
            if (!Root.Enabled) {
                EditorInputCaptureService.ClearBlocker(this);
                return;
            }

            int2 worldPosition = new int2(
                (int)Math.Round(Root.Position.X),
                (int)Math.Round(Root.Position.Y));
            if (MenuSize.X <= 0 || MenuSize.Y <= 0) {
                EditorInputCaptureService.ClearBlocker(this);
                return;
            }

            EditorInputCaptureService.SetBlocker(this, worldPosition, MenuSize);
        }

        /// <summary>
        /// Determines whether the pointer is inside the menu bounds.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>True when the pointer is inside the menu bounds.</returns>
        bool IsPointerInside(int2 pointer) {
            int2 worldPosition = new int2(
                (int)Math.Round(Root.Position.X),
                (int)Math.Round(Root.Position.Y));

            int left = worldPosition.X;
            int top = worldPosition.Y;
            int right = left + MenuSize.X;
            int bottom = top + MenuSize.Y;

            return pointer.X >= left &&
                   pointer.X < right &&
                   pointer.Y >= top &&
                   pointer.Y < bottom;
        }

        /// <summary>
        /// Determines whether the pointer is inside any other visible context menu.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>True when another visible menu already owns the click point.</returns>
        bool IsPointerInsideAnyVisibleMenu(int2 pointer) {
            for (int i = 0; i < VisibleMenus.Count; i++) {
                ContextMenu menu = VisibleMenus[i];
                if (menu == null || ReferenceEquals(menu, this) || !menu.IsVisible) {
                    continue;
                }

                if (menu.IsPointerInside(pointer)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds this menu to the shared visible-menu set when it opens.
        /// </summary>
        void RegisterVisibleMenu() {
            if (VisibleMenus.Contains(this)) {
                return;
            }

            VisibleMenus.Add(this);
        }

        /// <summary>
        /// Removes this menu from the shared visible-menu set when it closes.
        /// </summary>
        void UnregisterVisibleMenu() {
            VisibleMenus.Remove(this);
        }

        /// <summary>
        /// Computes the vertical offset needed to center text.
        /// </summary>
        /// <param name="containerHeight">Height of the row.</param>
        /// <param name="metrics">Tight font metrics.</param>
        /// <returns>Top offset for the label.</returns>
        float GetTextTopOffset(int containerHeight, FontTightMetrics metrics) {
            double height = Math.Max(containerHeight, 1);
            double offset = (height - metrics.Height) * 0.5 - metrics.MinTop;
            return (float)Math.Round(offset);
        }
    }
}


