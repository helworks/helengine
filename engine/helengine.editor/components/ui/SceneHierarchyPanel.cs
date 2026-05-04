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
        const int RowPaddingLeft = 8;
        const int ArrowSlotWidth = 12;
        const int ArrowLabelSpacing = 4;

        readonly FontAsset font;
        readonly EditorEntity contentRoot;
        readonly List<SceneHierarchyRow> rows;
        readonly List<NodeInfo> nodes;
        /// <summary>
        /// Expanded-state map for parent scene entities represented in the hierarchy.
        /// </summary>
        readonly Dictionary<Entity, bool> expandedEntities;
        /// <summary>
        /// Scratch set containing scene entities that currently have visible scene children.
        /// </summary>
        readonly HashSet<Entity> parentEntities;
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
            Title = "Scene";
            MinSize = new int2(220, 160);

            rowBackgroundOrder = RenderOrder2D.PanelSurface;
            rowTextOrder = RenderOrder2D.PanelForeground;

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            rows = new List<SceneHierarchyRow>(32);
            nodes = new List<NodeInfo>(64);
            expandedEntities = new Dictionary<Entity, bool>();
            parentEntities = new HashSet<Entity>();
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
            UpdateExpandedEntities(all);
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
            InputSystem input = Core.Instance.Input;
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
        /// Synchronizes tracked expanded entities with the current scene graph.
        /// </summary>
        /// <param name="all">All entities registered in the object manager.</param>
        void UpdateExpandedEntities(List<Entity> all) {
            parentEntities.Clear();

            for (int entityIndex = 0; entityIndex < all.Count; entityIndex++) {
                Entity entity = all[entityIndex];
                if (!IsSceneEntity(entity) || !HasSceneChildren(entity)) {
                    continue;
                }

                parentEntities.Add(entity);
                if (!expandedEntities.ContainsKey(entity)) {
                    expandedEntities.Add(entity, true);
                }
            }

            List<Entity> staleEntities = new List<Entity>();
            foreach (KeyValuePair<Entity, bool> entry in expandedEntities) {
                if (!parentEntities.Contains(entry.Key)) {
                    staleEntities.Add(entry.Key);
                }
            }

            for (int staleIndex = 0; staleIndex < staleEntities.Count; staleIndex++) {
                expandedEntities.Remove(staleEntities[staleIndex]);
            }
        }

        /// <summary>
        /// Recursively flattens the scene hierarchy into the node list.
        /// </summary>
        /// <param name="entity">Current entity being visited.</param>
        /// <param name="depth">Depth in the hierarchy.</param>
        void AppendHierarchy(Entity entity, int depth) {
            bool hasChildren = parentEntities.Contains(entity);
            bool isExpanded = !hasChildren || IsEntityExpanded(entity);
            nodes.Add(new NodeInfo(entity, depth, hasChildren, isExpanded));

            if (entity.Children == null || !hasChildren || !isExpanded) {
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
                    row.HasChildren = false;
                    row.IsExpanded = false;
                    row.IsHovering = false;
                    row.IsPressed = false;
                    row.IsArrowPressed = false;
                    row.IsSelected = false;
                    row.IsSelectable = false;
                    row.IsSceneRoot = false;
                    row.Arrow.Text = string.Empty;
                    row.ArrowHitLeft = 0;
                    row.ArrowHitWidth = 0;
                    row.FocusTarget.SetTargetFocused(false);
                    UpdateRowBackground(row, ThemeManager.Colors.SurfacePrimary);
                    continue;
                }

                NodeInfo node = nodes[i];
                row.Entity.Enabled = true;
                row.NodeEntity = node.Entity;
                row.HasChildren = node.HasChildren;
                row.IsExpanded = node.IsExpanded;
                row.IsSelectable = true;
                row.IsSceneRoot = false;
                row.Entity.Position = new float3(0, i * RowHeight, 0.1f);
                row.IsSelected = node.Entity == EditorSelectionService.SelectedEntity;

                bool alternate = i % 2 == 1;
                byte4 baseColor = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                row.BaseColor = baseColor;
                UpdateRowBackground(row, baseColor);

                int rowWidth = Math.Max(Size.X, MinSize.X);

                row.Background.Size = new int2(rowWidth, RowHeight);
                row.Interactable.Size = new int2(rowWidth, RowHeight);

                int arrowLeft = RowPaddingLeft + node.Depth * RowIndent;
                row.ArrowHitLeft = arrowLeft;
                row.ArrowHitWidth = ArrowSlotWidth;
                row.ArrowHost.Position = new float3(arrowLeft, MathF.Round((RowHeight - lineHeight) * 0.5f), 0.2f);
                row.Arrow.Text = node.HasChildren
                    ? (node.IsExpanded ? "v" : ">")
                    : string.Empty;
                row.Arrow.Size = new int2(ArrowSlotWidth, (int)MathF.Ceiling(lineHeight));
                row.Arrow.Color = ThemeManager.Colors.InputForegroundPrimary;

                float indent = arrowLeft + ArrowSlotWidth + ArrowLabelSpacing;
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

            var arrowHost = new EditorEntity();
            arrowHost.LayerMask = LayerMask;
            arrowHost.Position = new float3(RowPaddingLeft, 2, 0.2f);
            rowEntity.AddChild(arrowHost);

            var arrow = new TextComponent();
            arrow.Font = font;
            arrow.Text = string.Empty;
            arrow.Color = ThemeManager.Colors.InputForegroundPrimary;
            arrow.Size = new int2(ArrowSlotWidth, RowHeight);
            arrow.RenderOrder2D = rowTextOrder;
            arrowHost.AddComponent(arrow);

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
                key => key == Keys.Enter ||
                       key == Keys.Up ||
                       key == Keys.Down ||
                       key == Keys.Left ||
                       key == Keys.Right,
                key => HandleRowActivationKey(row, key));
            row = new SceneHierarchyRow(
                rowEntity,
                background,
                arrowHost,
                arrow,
                labelHost,
                text,
                interactable,
                focusTarget);

            EditorKeyboardFocusService.RegisterTarget(row.FocusTarget);
            interactable.CursorEvent += (pos, delta, state) => HandleRowCursor(row, pos, state);
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
        /// Returns true when the provided entity has at least one visible scene child.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when one child should appear in the hierarchy.</returns>
        bool HasSceneChildren(Entity entity) {
            if (entity.Children == null) {
                return false;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                Entity child = entity.Children[childIndex];
                if (IsSceneEntity(child)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the provided parent entity is currently expanded in the hierarchy.
        /// </summary>
        /// <param name="entity">Parent entity to evaluate.</param>
        /// <returns>True when the parent should display its descendants.</returns>
        bool IsEntityExpanded(Entity entity) {
            if (expandedEntities.TryGetValue(entity, out bool isExpanded)) {
                return isExpanded;
            }

            return true;
        }

        /// <summary>
        /// Handles pointer interactions for a row to update its visual state.
        /// </summary>
        /// <param name="row">Row receiving the event.</param>
        /// <param name="point">Pointer position in row-local coordinates.</param>
        /// <param name="state">Interaction state.</param>
        void HandleRowCursor(SceneHierarchyRow row, int2 point, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    row.IsHovering = true;
                    break;
                case PointerInteraction.Press:
                    row.IsPressed = true;
                    row.IsArrowPressed = row.ContainsArrowPoint(point);
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = row.IsPressed && row.IsHovering;
                    bool shouldToggleExpanded = shouldActivate &&
                                                row.IsArrowPressed &&
                                                row.ContainsArrowPoint(point);
                    row.IsPressed = false;
                    row.IsArrowPressed = false;
                    if (shouldToggleExpanded) {
                        ToggleExpanded(row);
                    } else if (shouldActivate) {
                        ActivateRow(row);
                    }
                    break;
                case PointerInteraction.Leave:
                    row.IsHovering = false;
                    row.IsPressed = false;
                    row.IsArrowPressed = false;
                    break;
                default:
                    break;
            }

            UpdateRowBackground(row, row.BaseColor);
        }

        /// <summary>
        /// Toggles the expanded state for the provided hierarchy row and refreshes the visible branch list.
        /// </summary>
        /// <param name="row">Row whose represented branch should toggle.</param>
        void ToggleExpanded(SceneHierarchyRow row) {
            if (row == null || row.NodeEntity == null || !row.HasChildren) {
                return;
            }

            expandedEntities[row.NodeEntity] = !IsEntityExpanded(row.NodeEntity);
            RefreshHierarchy();
        }

        /// <summary>
        /// Routes keyboard activation for one hierarchy row.
        /// </summary>
        /// <param name="row">Focused row receiving the key.</param>
        /// <param name="key">Activation key that was pressed.</param>
        void HandleRowActivationKey(SceneHierarchyRow row, Keys key) {
            if (row == null || row.NodeEntity == null) {
                return;
            }

            if (key == Keys.Enter) {
                ActivateRow(row);
            } else if (key == Keys.Up) {
                FocusAdjacentRow(row, -1);
            } else if (key == Keys.Down) {
                FocusAdjacentRow(row, 1);
            } else if (key == Keys.Left) {
                CollapseRow(row);
            } else if (key == Keys.Right) {
                ExpandRow(row);
            }
        }

        /// <summary>
        /// Moves keyboard focus to the previous or next visible hierarchy row.
        /// </summary>
        /// <param name="row">Currently focused row.</param>
        /// <param name="offset">Visible-row offset to apply.</param>
        void FocusAdjacentRow(SceneHierarchyRow row, int offset) {
            int rowIndex = rows.IndexOf(row);
            if (rowIndex < 0) {
                return;
            }

            int adjacentIndex = rowIndex + offset;
            if (adjacentIndex < 0 || adjacentIndex >= nodes.Count) {
                return;
            }

            SceneHierarchyRow adjacentRow = rows[adjacentIndex];
            if (!adjacentRow.Entity.Enabled || adjacentRow.NodeEntity == null) {
                return;
            }

            EditorKeyboardFocusService.SetFocusedTarget(adjacentRow.FocusTarget);
        }

        /// <summary>
        /// Expands one focused parent row when it currently has collapsed visible children.
        /// </summary>
        /// <param name="row">Focused row whose branch should expand.</param>
        void ExpandRow(SceneHierarchyRow row) {
            if (row == null || row.NodeEntity == null || !row.HasChildren || row.IsExpanded) {
                return;
            }

            SetExpandedState(row, true);
        }

        /// <summary>
        /// Collapses one focused parent row when it currently displays visible descendants.
        /// </summary>
        /// <param name="row">Focused row whose branch should collapse.</param>
        void CollapseRow(SceneHierarchyRow row) {
            if (row == null || row.NodeEntity == null || !row.HasChildren || !row.IsExpanded) {
                return;
            }

            SetExpandedState(row, false);
        }

        /// <summary>
        /// Applies one explicit expanded state and preserves keyboard focus on the same entity after refresh.
        /// </summary>
        /// <param name="row">Row whose represented entity should change expanded state.</param>
        /// <param name="isExpanded">Expanded state to apply.</param>
        void SetExpandedState(SceneHierarchyRow row, bool isExpanded) {
            Entity entity = row.NodeEntity;
            expandedEntities[entity] = isExpanded;
            RefreshHierarchy();

            SceneHierarchyRow refreshedRow = FindVisibleRow(entity);
            if (refreshedRow != null) {
                EditorKeyboardFocusService.SetFocusedTarget(refreshedRow.FocusTarget);
            }
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
        /// Finds one currently visible row for the provided entity.
        /// </summary>
        /// <param name="entity">Entity represented by the desired row.</param>
        /// <returns>Visible row when present; otherwise null.</returns>
        SceneHierarchyRow FindVisibleRow(Entity entity) {
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (!row.Entity.Enabled || row.NodeEntity == null) {
                    continue;
                }

                if (ReferenceEquals(row.NodeEntity, entity)) {
                    return row;
                }
            }

            return null;
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
            /// <param name="hasChildren">True when the entity has visible scene children.</param>
            /// <param name="isExpanded">True when the entity branch is currently expanded.</param>
            public NodeInfo(Entity entity, int depth, bool hasChildren, bool isExpanded) {
                Entity = entity;
                Depth = depth;
                HasChildren = hasChildren;
                IsExpanded = isExpanded;
            }

            /// <summary>
            /// Gets the referenced entity.
            /// </summary>
            public Entity Entity { get; }

            /// <summary>
            /// Gets the depth within the hierarchy.
            /// </summary>
            public int Depth { get; }

            /// <summary>
            /// Gets whether the entity currently has visible scene children.
            /// </summary>
            public bool HasChildren { get; }

            /// <summary>
            /// Gets whether the entity branch is currently expanded.
            /// </summary>
            public bool IsExpanded { get; }
        }
    }
}


