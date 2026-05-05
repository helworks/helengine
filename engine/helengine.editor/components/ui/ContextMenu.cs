namespace helengine.editor {
    /// <summary>
    /// Provides a reusable context menu UI for editor panels.
    /// </summary>
    public class ContextMenu {
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
                RenderOrder2D = backgroundOrder
            };
            BackgroundBlockerEntity.AddComponent(BackgroundBlockerSurface);

            BackgroundBlockerInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            BackgroundBlockerEntity.AddComponent(BackgroundBlockerInteractable);

            Root.AddComponent(new ContextMenuUpdater(this));
            IsInitialized = true;
        }

        /// <summary>
        /// Gets the root entity for the context menu.
        /// </summary>
        public EditorEntity Entity => Root;

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
            EnsureRowCount(ActiveItems.Count);
            ApplyItems();
            UpdateLayoutInternal();
            Root.Enabled = true;
            UpdateInputBlocker();
        }

        /// <summary>
        /// Hides the menu and clears its input blocker.
        /// </summary>
        public void Hide() {
            Root.Enabled = false;
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
            if (!IsPointerInside(pointer)) {
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

            var interactable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            rowEntity.AddComponent(interactable);

            var row = new ContextMenuRow(rowEntity, background, labelHost, label, interactable);
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
                if (i >= ActiveItems.Count) {
                    row.Entity.Enabled = false;
                    row.Item = null;
                    continue;
                }

                ContextMenuItem item = ActiveItems[i];
                row.Entity.Enabled = true;
                row.Item = item;
                row.Label.Text = item.Label ?? string.Empty;
                row.ResetState();
            }
        }

        /// <summary>
        /// Updates size, position, and row layout for the menu.
        /// </summary>
        void UpdateLayoutInternal() {
            MenuSize = ComputeMenuSize();
            MenuPosition = ClampPosition(MenuPosition, MenuSize, HostSize);
            Root.Position = new float3(MenuPosition.X, MenuPosition.Y, 0.2f);

            Background.Size = MenuSize;
            BackgroundBlockerSurface.Size = MenuSize;
            BackgroundBlockerInteractable.Size = MenuSize;
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

                var metrics = Font.MeasureTight(row.Label.Text ?? string.Empty);
                float labelY = GetTextTopOffset(RowHeight, metrics);
                row.LabelHost.Position = new float3(PaddingX, labelY, 0.2f);
                row.Label.Size = new int2(Math.Max(0, MenuSize.X - PaddingX * 2), (int)Math.Ceiling(metrics.Height));

                y += RowHeight + RowSpacing;
            }
        }

        /// <summary>
        /// Computes the menu size based on the current items.
        /// </summary>
        /// <returns>Calculated menu size.</returns>
        int2 ComputeMenuSize() {
            int width = MinWidth;
            for (int i = 0; i < ActiveItems.Count; i++) {
                string label = ActiveItems[i].Label ?? string.Empty;
                var metrics = Font.MeasureTight(label);
                int required = (int)Math.Ceiling(metrics.Width) + (PaddingX * 2);
                if (required > width) {
                    width = required;
                }
            }

            if (width > MaxWidth) {
                width = MaxWidth;
            }

            int rowCount = ActiveItems.Count;
            int height = PaddingY * 2;
            if (rowCount > 0) {
                height += (RowHeight * rowCount) + (RowSpacing * (rowCount - 1));
            }

            if (height < RowHeight + (PaddingY * 2)) {
                height = RowHeight + (PaddingY * 2);
            }

            return new int2(width, height);
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


