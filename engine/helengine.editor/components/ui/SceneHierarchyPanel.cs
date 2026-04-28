namespace helengine.editor {
    /// <summary>
    /// Displays a dockable scene hierarchy list for the current scene graph.
    /// </summary>
    public class SceneHierarchyPanel : DockableEntity {
        /// <summary>
        /// Height of each row in the hierarchy list.
        /// </summary>
        public const int RowHeight = 22;

        const int RowIndent = 14;

        readonly FontAsset font;
        readonly EditorEntity contentRoot;
        readonly List<SceneHierarchyRow> rows;
        readonly List<NodeInfo> nodes;
        /// <summary>
        /// Context menu shown for one hierarchy row.
        /// </summary>
        readonly ContextMenu hierarchyContextMenu;
        /// <summary>
        /// Menu items available for the currently right-clicked row.
        /// </summary>
        readonly List<ContextMenuItem> rowContextMenuItems;
        /// <summary>
        /// Render order used for row backgrounds.
        /// </summary>
        readonly byte rowBackgroundOrder;
        /// <summary>
        /// Render order used for row text.
        /// </summary>
        readonly byte rowTextOrder;
        /// <summary>
        /// Row that owns the currently visible context menu.
        /// </summary>
        SceneHierarchyRow contextMenuRow;
        /// <summary>
        /// Tracks whether the panel has completed initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Raised when one hierarchy row requests the reparent workflow.
        /// </summary>
        public event Action<Entity> ReparentRequested;

        /// <summary>
        /// Initializes a new scene hierarchy panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        public SceneHierarchyPanel(FontAsset font) : base(font) {
            this.font = font;
            Title = "Scene Hierarchy";
            MinSize = new int2(220, 160);

            rowBackgroundOrder = RenderOrder2D.PanelSurface;
            rowTextOrder = RenderOrder2D.PanelForeground;

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            rows = new List<SceneHierarchyRow>(32);
            nodes = new List<NodeInfo>(64);
            hierarchyContextMenu = new ContextMenu(font, LayerMask, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            AddChild(hierarchyContextMenu.Entity);
            rowContextMenuItems = new List<ContextMenuItem> {
                new ContextMenuItem("Reparent", HandleReparentRequested)
            };

            EditorSelectionService.SelectionChanged += args => RefreshHierarchy();
            AddComponent(new SceneHierarchyPanelUpdater(this));
            isInitialized = true;
            RefreshHierarchy();
        }

        /// <summary>
        /// Rebuilds the hierarchy view from the current object manager state.
        /// </summary>
        public void RefreshHierarchy() {
            if (nodes == null) {
                return;
            }

            var manager = Core.Instance?.ObjectManager;
            if (manager == null) {
                return;
            }

            nodes.Clear();

            List<Entity> all = manager.Entities;
            for (int i = 0; i < all.Count; i++) {
                Entity entity = all[i];
                if (!IsSceneEntity(entity)) {
                    continue;
                }

                if (entity.Parent == null || !IsSceneEntity(entity.Parent)) {
                    AppendHierarchy(entity, 0);
                }
            }

            LayoutRows();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized || font == null || nodes == null || rows == null) {
                return;
            }

            LayoutRows();
            hierarchyContextMenu.UpdateLayout(GetContextMenuHostSize());
        }

        /// <summary>
        /// Updates hierarchy context-menu input each frame.
        /// </summary>
        internal void UpdateContextMenuInput() {
            InputManager input = Core.Instance.InputManager;
            if (!input.WasMouseRightButtonPressed()) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                return;
            }
            if (!IsPointerInsideContent(pointer)) {
                hierarchyContextMenu.Hide();
                return;
            }

            if (!TryGetRowAtScreenPoint(pointer, out SceneHierarchyRow row)) {
                hierarchyContextMenu.Hide();
                return;
            }

            contextMenuRow = row;
            ActivateRow(row);

            int2 localPosition = new int2(
                pointer.X - (int)Math.Round(Position.X),
                pointer.Y - (int)Math.Round(Position.Y));
            hierarchyContextMenu.Show(rowContextMenuItems, localPosition, GetContextMenuHostSize());
        }

        /// <summary>
        /// Recursively flattens the scene hierarchy into the node list.
        /// </summary>
        /// <param name="entity">Current entity being visited.</param>
        /// <param name="depth">Depth in the hierarchy.</param>
        void AppendHierarchy(Entity entity, int depth) {
            nodes.Add(new NodeInfo(entity, depth));

            if (entity.Children == null) {
                return;
            }

            for (int i = 0; i < entity.Children.Count; i++) {
                Entity child = entity.Children[i];
                if (IsSceneEntity(child)) {
                    AppendHierarchy(child, depth + 1);
                }
            }
        }

        /// <summary>
        /// Lays out all rows based on the current node list and panel size.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(nodes.Count);

            float lineHeight = MathF.Max(font.LineHeight, 1f);

            for (int i = 0; i < rows.Count; i++) {
                SceneHierarchyRow row = rows[i];
                row.FocusTarget.TabIndex = i;
                if (i >= nodes.Count) {
                    row.Entity.Enabled = false;
                    row.NodeEntity = null;
                    row.IsHovering = false;
                    row.IsPressed = false;
                    row.IsSelected = false;
                    row.FocusTarget.SetTargetFocused(false);
                    UpdateRowBackground(row, ThemeManager.Colors.SurfacePrimary);
                    continue;
                }

                NodeInfo node = nodes[i];
                row.Entity.Enabled = true;
                row.NodeEntity = node.Entity;
                row.Entity.Position = new float3(0, i * RowHeight, 0.1f);
                row.IsSelected = node.Entity == EditorSelectionService.SelectedEntity;

                bool alternate = i % 2 == 1;
                byte4 baseColor = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                row.BaseColor = baseColor;
                UpdateRowBackground(row, baseColor);

                int rowWidth = Math.Max(Size.X, MinSize.X);

                row.Background.Size = new int2(rowWidth, RowHeight);
                row.Interactable.Size = new int2(rowWidth, RowHeight);

                float indent = 8 + node.Depth * RowIndent;
                row.LabelHost.Position = new float3(indent, MathF.Round((RowHeight - lineHeight) * 0.5f), 0.2f);

                string label = node.Entity is EditorEntity editorEntity ? editorEntity.Name : node.Entity.GetType().Name;
                row.Label.Text = label;
                row.Label.Size = new int2(Math.Max(0, rowWidth - (int)indent), (int)MathF.Ceiling(lineHeight));
                row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            }
        }

        /// <summary>
        /// Ensures the row pool has enough entries to render the current hierarchy.
        /// </summary>
        /// <param name="count">Required row count.</param>
        void EnsureRowCount(int count) {
            bool created = false;
            for (int i = rows.Count; i < count; i++) {
                rows.Add(CreateRow());
                created = true;
            }
            if (created) {
                RefreshRenderOrderBias();
            }
        }

        /// <summary>
        /// Creates a single row entity with background, label, and hover handling.
        /// </summary>
        /// <returns>Newly created row elements.</returns>
        SceneHierarchyRow CreateRow() {
            var rowEntity = new EditorEntity();
            rowEntity.LayerMask = LayerMask;
            rowEntity.Position = float3.Zero;

            var background = new SpriteComponent();
            background.Texture = TextureUtils.PixelTexture;
            background.Color = ThemeManager.Colors.SurfacePrimary;
            background.RenderOrder2D = rowBackgroundOrder;
            rowEntity.AddComponent(background);

            var interactable = new InteractableComponent();
            interactable.Size = new int2(Size.X, RowHeight);
            rowEntity.AddComponent(interactable);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = LayerMask;
            labelHost.Position = new float3(8, 2, 0.2f);
            rowEntity.AddChild(labelHost);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(100, RowHeight);
            text.RenderOrder2D = rowTextOrder;
            labelHost.AddComponent(text);

            SceneHierarchyRow row = null;
            EditorFocusTarget focusTarget = new EditorFocusTarget(
                this,
                0,
                false,
                () => row.Entity.Enabled && row.NodeEntity != null,
                point => ContainsHierarchyRowPoint(row, point),
                isFocused => {
                    row.IsKeyboardFocused = isFocused;
                    UpdateRowBackground(row, row.BaseColor);
                },
                key => key == Keys.Enter,
                key => ActivateRow(row));
            row = new SceneHierarchyRow(
                rowEntity,
                background,
                labelHost,
                text,
                interactable,
                focusTarget);

            EditorKeyboardFocusService.RegisterTarget(row.FocusTarget);
            interactable.CursorEvent += (pos, delta, state) => HandleRowCursor(row, state);
            contentRoot.AddChild(rowEntity);
            return row;
        }

        /// <summary>
        /// Determines whether an entity should appear in the hierarchy.
        /// </summary>
        /// <param name="entity">Entity to evaluate.</param>
        /// <returns>True when the entity is not marked as internal.</returns>
        bool IsSceneEntity(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (current is EditorEntity editorEntity && editorEntity.InternalEntity) {
                    return false;
                }
                current = current.Parent;
            }

            return true;
        }

        /// <summary>
        /// Handles pointer interactions for a row to update its visual state.
        /// </summary>
        /// <param name="row">Row receiving the event.</param>
        /// <param name="state">Interaction state.</param>
        void HandleRowCursor(SceneHierarchyRow row, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    row.IsHovering = true;
                    break;
                case PointerInteraction.Press:
                    row.IsPressed = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = row.IsPressed && row.IsHovering;
                    row.IsPressed = false;
                    if (shouldActivate) {
                        ActivateRow(row);
                    }
                    break;
                case PointerInteraction.Leave:
                    row.IsHovering = false;
                    row.IsPressed = false;
                    break;
                default:
                    break;
            }

            UpdateRowBackground(row, row.BaseColor);
        }

        /// <summary>
        /// Updates a row background color based on hover and press state.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="baseColor">Base color for the row.</param>
        void UpdateRowBackground(SceneHierarchyRow row, byte4 baseColor) {
            if (row.IsSelected) {
                row.Background.Color = ThemeManager.Colors.AccentSecondary;
                return;
            }

            if (row.IsPressed) {
                row.Background.Color = ThemeManager.Colors.AccentPrimary;
                return;
            }

            if (row.IsHovering) {
                row.Background.Color = ThemeManager.Colors.AccentSecondary;
                return;
            }

            if (row.IsKeyboardFocused) {
                row.Background.Color = ThemeManager.Colors.AccentTertiary;
                return;
            }

            row.Background.Color = baseColor;
        }

        /// <summary>
        /// Hides the context menu when the panel is disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        protected override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                hierarchyContextMenu.Hide();
                contextMenuRow = null;
            }
        }

        /// <summary>
        /// Activates one hierarchy row by selecting its represented entity.
        /// </summary>
        /// <param name="row">Row to activate.</param>
        void ActivateRow(SceneHierarchyRow row) {
            if (row == null || row.NodeEntity == null) {
                return;
            }

            EditorSelectionService.SetSelectedEntity(row.NodeEntity);
        }

        /// <summary>
        /// Resolves the host region used to clamp hierarchy context menus.
        /// </summary>
        /// <returns>Host size for context-menu layout.</returns>
        int2 GetContextMenuHostSize() {
            return new int2(
                Math.Max(Size.X, MinSize.X),
                Math.Max(Size.Y + TitleBarHeight, MinSize.Y + TitleBarHeight));
        }

        /// <summary>
        /// Returns true when the provided pointer lies inside the scrollable hierarchy content area.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <returns>True when the pointer lies inside the hierarchy content region.</returns>
        bool IsPointerInsideContent(int2 pointer) {
            int panelX = (int)Math.Round(Position.X);
            int panelY = (int)Math.Round(Position.Y);
            int panelWidth = Math.Max(Size.X, MinSize.X);
            int panelHeight = Math.Max(Size.Y, MinSize.Y);

            return pointer.X >= panelX &&
                   pointer.X < panelX + panelWidth &&
                   pointer.Y >= panelY + TitleBarHeight &&
                   pointer.Y < panelY + TitleBarHeight + panelHeight;
        }

        /// <summary>
        /// Tries to resolve one visible row for the provided screen-space pointer.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <param name="row">Resolved row when one is found.</param>
        /// <returns>True when a visible hierarchy row was found.</returns>
        bool TryGetRowAtScreenPoint(int2 pointer, out SceneHierarchyRow row) {
            for (int i = 0; i < rows.Count; i++) {
                SceneHierarchyRow candidate = rows[i];
                if (!candidate.Entity.Enabled || candidate.NodeEntity == null) {
                    continue;
                }

                float3 rowPosition = candidate.Entity.Position;
                int rowWidth = Math.Max(Size.X, MinSize.X);
                if (pointer.X >= rowPosition.X &&
                    pointer.X < rowPosition.X + rowWidth &&
                    pointer.Y >= rowPosition.Y &&
                    pointer.Y < rowPosition.Y + RowHeight) {
                    row = candidate;
                    return true;
                }
            }

            row = null;
            return false;
        }

        /// <summary>
        /// Raises one reparent request for the row that opened the context menu.
        /// </summary>
        void HandleReparentRequested() {
            if (contextMenuRow == null || contextMenuRow.NodeEntity == null) {
                return;
            }

            if (ReparentRequested != null) {
                ReparentRequested(contextMenuRow.NodeEntity);
            }
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside one hierarchy row.
        /// </summary>
        /// <param name="row">Row to evaluate.</param>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the row bounds.</returns>
        bool ContainsHierarchyRowPoint(SceneHierarchyRow row, int2 point) {
            float3 position = row.Entity.Position;
            int rowWidth = Math.Max(Size.X, MinSize.X);
            return point.X >= position.X &&
                   point.X < position.X + rowWidth &&
                   point.Y >= position.Y &&
                   point.Y < position.Y + RowHeight;
        }

        /// <summary>
        /// Captures a flattened hierarchy node and its depth.
        /// </summary>
        readonly struct NodeInfo {
            /// <summary>
            /// Initializes a new node info instance.
            /// </summary>
            /// <param name="entity">Referenced entity.</param>
            /// <param name="depth">Depth within the hierarchy.</param>
            public NodeInfo(Entity entity, int depth) {
                Entity = entity;
                Depth = depth;
            }

            /// <summary>
            /// Gets the referenced entity.
            /// </summary>
            public Entity Entity { get; }

            /// <summary>
            /// Gets the depth within the hierarchy.
            /// </summary>
            public int Depth { get; }
        }
    }
}
