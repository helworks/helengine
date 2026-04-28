namespace helengine.editor {
    /// <summary>
    /// Renders a lightweight scene-hierarchy picker that supports disabled rows for invalid reparent targets.
    /// </summary>
    public class SceneHierarchyPickerView {
        /// <summary>
        /// Indentation applied for each child depth in the picker hierarchy.
        /// </summary>
        const int RowIndent = 14;
        /// <summary>
        /// Left padding applied before the row arrow slot.
        /// </summary>
        const int RowPaddingLeft = 8;
        /// <summary>
        /// Width reserved for the expand-collapse glyph.
        /// </summary>
        const int ArrowSlotWidth = 12;
        /// <summary>
        /// Horizontal spacing between the arrow slot and the label.
        /// </summary>
        const int ArrowLabelSpacing = 4;

        /// <summary>
        /// Font used for row labels and expand-collapse glyphs.
        /// </summary>
        readonly FontAsset font;
        /// <summary>
        /// Root entity hosting all picker row visuals.
        /// </summary>
        readonly EditorEntity rootEntity;
        /// <summary>
        /// Pool of row visuals reused across picker refreshes.
        /// </summary>
        readonly List<SceneHierarchyRow> rows;
        /// <summary>
        /// Flattened visible hierarchy nodes shown by the picker.
        /// </summary>
        readonly List<NodeInfo> nodes;
        /// <summary>
        /// Scene entities that should appear in the hierarchy, including invalid descendants.
        /// </summary>
        readonly List<Entity> visibleEntities;
        /// <summary>
        /// Lookup set for entities currently included in the picker hierarchy.
        /// </summary>
        readonly HashSet<Entity> visibleEntitySet;
        /// <summary>
        /// Scratch set containing entities that currently have visible picker children.
        /// </summary>
        readonly HashSet<Entity> parentEntities;
        /// <summary>
        /// Expanded-state map for entities that own visible picker children.
        /// </summary>
        readonly Dictionary<Entity, bool> expandedEntities;
        /// <summary>
        /// Render order used for row background sprites.
        /// </summary>
        readonly byte rowBackgroundOrder;
        /// <summary>
        /// Render order used for row text.
        /// </summary>
        readonly byte rowTextOrder;
        /// <summary>
        /// Entity currently being reparented.
        /// </summary>
        Entity TargetEntity;
        /// <summary>
        /// Parent entity currently selected in the picker, or null for the scene root.
        /// </summary>
        Entity SelectedParentEntity;
        /// <summary>
        /// Cached picker size used during row layout.
        /// </summary>
        int2 Size;

        /// <summary>
        /// Raised when the user selects one valid parent entity in the picker.
        /// </summary>
        public event Action<Entity> ParentEntitySelected;

        /// <summary>
        /// Initializes a new scene-hierarchy picker view.
        /// </summary>
        /// <param name="font">Font used for labels and glyphs.</param>
        /// <param name="layerMask">Layer mask applied to the picker visuals.</param>
        /// <param name="rowBackgroundOrder">Render order used for row background sprites.</param>
        /// <param name="rowTextOrder">Render order used for row labels and glyphs.</param>
        public SceneHierarchyPickerView(FontAsset font, ushort layerMask, byte rowBackgroundOrder, byte rowTextOrder) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            this.font = font;
            this.rowBackgroundOrder = rowBackgroundOrder;
            this.rowTextOrder = rowTextOrder;

            rootEntity = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                Enabled = false
            };

            rows = new List<SceneHierarchyRow>(16);
            nodes = new List<NodeInfo>(32);
            visibleEntities = new List<Entity>(32);
            visibleEntitySet = new HashSet<Entity>();
            parentEntities = new HashSet<Entity>();
            expandedEntities = new Dictionary<Entity, bool>();
            Size = new int2(1, 1);
        }

        /// <summary>
        /// Gets the root entity that hosts the picker visuals.
        /// </summary>
        public EditorEntity Entity => rootEntity;

        /// <summary>
        /// Gets the parent entity currently selected in the picker, or null for the scene root.
        /// </summary>
        public Entity SelectedEntity => SelectedParentEntity;

        /// <summary>
        /// Rebuilds the picker from the provided target entity and visible scene hierarchy.
        /// </summary>
        /// <param name="targetEntity">Entity currently being reparented.</param>
        /// <param name="parentEntities">Visible scene entities that should appear in the picker.</param>
        /// <param name="selectedParentEntity">Initial parent selection.</param>
        public void Show(Entity targetEntity, IReadOnlyList<Entity> parentEntities, Entity selectedParentEntity) {
            if (targetEntity == null) {
                throw new ArgumentNullException(nameof(targetEntity));
            }
            if (parentEntities == null) {
                throw new ArgumentNullException(nameof(parentEntities));
            }

            TargetEntity = targetEntity;
            SelectedParentEntity = selectedParentEntity;

            CopyVisibleEntities(parentEntities);
            RebuildHierarchy();
            rootEntity.Enabled = true;
        }

        /// <summary>
        /// Clears picker state and hides all visible rows.
        /// </summary>
        public void Hide() {
            TargetEntity = null;
            SelectedParentEntity = null;
            visibleEntities.Clear();
            visibleEntitySet.Clear();
            parentEntities.Clear();
            nodes.Clear();
            rootEntity.Enabled = false;
            LayoutRows();
        }

        /// <summary>
        /// Updates the picker layout to the provided content size.
        /// </summary>
        /// <param name="width">Available picker width.</param>
        /// <param name="height">Available picker height.</param>
        public void UpdateLayout(int width, int height) {
            Size = new int2(Math.Max(1, width), Math.Max(1, height));
            LayoutRows();
        }

        /// <summary>
        /// Copies the visible hierarchy entities into picker-owned state.
        /// </summary>
        /// <param name="parentEntities">Visible scene entities that should appear in the picker.</param>
        void CopyVisibleEntities(IReadOnlyList<Entity> parentEntities) {
            visibleEntities.Clear();
            visibleEntitySet.Clear();

            for (int entityIndex = 0; entityIndex < parentEntities.Count; entityIndex++) {
                Entity entity = parentEntities[entityIndex];
                if (entity == null) {
                    continue;
                }

                visibleEntities.Add(entity);
                visibleEntitySet.Add(entity);
            }
        }

        /// <summary>
        /// Rebuilds the flattened visible hierarchy nodes from the copied scene entities.
        /// </summary>
        void RebuildHierarchy() {
            UpdateExpandedEntities();
            nodes.Clear();
            nodes.Add(new NodeInfo(null, "Scene Root", 0, false, false, true, true));

            for (int entityIndex = 0; entityIndex < visibleEntities.Count; entityIndex++) {
                Entity entity = visibleEntities[entityIndex];
                if (entity.Parent == null || !visibleEntitySet.Contains(entity.Parent)) {
                    AppendHierarchy(entity, 1);
                }
            }

            LayoutRows();
        }

        /// <summary>
        /// Synchronizes expanded-state tracking with entities that currently have visible picker children.
        /// </summary>
        void UpdateExpandedEntities() {
            parentEntities.Clear();

            for (int entityIndex = 0; entityIndex < visibleEntities.Count; entityIndex++) {
                Entity entity = visibleEntities[entityIndex];
                if (!HasVisibleChildren(entity)) {
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
        /// Appends one visible entity branch into the flattened picker node list.
        /// </summary>
        /// <param name="entity">Current entity to append.</param>
        /// <param name="depth">Hierarchy depth used for indentation.</param>
        void AppendHierarchy(Entity entity, int depth) {
            bool hasChildren = parentEntities.Contains(entity);
            bool isExpanded = !hasChildren || IsExpanded(entity);
            nodes.Add(new NodeInfo(
                entity,
                GetEntityDisplayName(entity),
                depth,
                hasChildren,
                isExpanded,
                !IsInvalidParent(entity),
                false));

            if (entity.Children == null || !hasChildren || !isExpanded) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                Entity child = entity.Children[childIndex];
                if (visibleEntitySet.Contains(child)) {
                    AppendHierarchy(child, depth + 1);
                }
            }
        }

        /// <summary>
        /// Ensures the row pool contains enough visuals for the current flattened hierarchy.
        /// </summary>
        /// <param name="count">Required visible row count.</param>
        void EnsureRowCount(int count) {
            for (int rowIndex = rows.Count; rowIndex < count; rowIndex++) {
                rows.Add(CreateRow());
            }
        }

        /// <summary>
        /// Lays out all rows and refreshes their current visual state.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(nodes.Count);

            float lineHeight = MathF.Max(font.LineHeight, 1f);
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (rowIndex >= nodes.Count) {
                    DisableRow(row);
                    continue;
                }

                NodeInfo node = nodes[rowIndex];
                row.Entity.Enabled = rootEntity.Enabled;
                row.NodeEntity = node.Entity;
                row.IsSceneRoot = node.IsSceneRoot;
                row.IsSelectable = node.IsSelectable;
                row.HasChildren = node.HasChildren;
                row.IsExpanded = node.IsExpanded;
                row.IsSelected = IsRowSelected(node);
                row.IsHovering = false;
                row.IsPressed = false;
                row.IsArrowPressed = false;
                row.IsKeyboardFocused = false;
                row.Entity.Position = new float3(0, rowIndex * SceneHierarchyPanel.RowHeight, 0.1f);

                bool alternate = rowIndex % 2 == 1;
                byte4 baseColor = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                row.BaseColor = baseColor;
                UpdateRowBackground(row, baseColor);

                row.Background.Size = new int2(Size.X, SceneHierarchyPanel.RowHeight);
                row.Interactable.Size = new int2(Size.X, SceneHierarchyPanel.RowHeight);

                int arrowLeft = RowPaddingLeft + node.Depth * RowIndent;
                row.ArrowHitLeft = arrowLeft;
                row.ArrowHitWidth = ArrowSlotWidth;
                row.ArrowHost.Position = new float3(arrowLeft, MathF.Round((SceneHierarchyPanel.RowHeight - lineHeight) * 0.5f), 0.2f);
                row.Arrow.Text = node.HasChildren
                    ? (node.IsExpanded ? "v" : ">")
                    : string.Empty;
                row.Arrow.Size = new int2(ArrowSlotWidth, (int)MathF.Ceiling(lineHeight));
                row.Arrow.Color = node.IsSelectable
                    ? ThemeManager.Colors.InputForegroundPrimary
                    : ThemeManager.Colors.AccentQuaternary;

                float indent = arrowLeft + ArrowSlotWidth + ArrowLabelSpacing;
                row.LabelHost.Position = new float3(indent, MathF.Round((SceneHierarchyPanel.RowHeight - lineHeight) * 0.5f), 0.2f);
                row.Label.Text = node.Label;
                row.Label.Size = new int2(Math.Max(0, Size.X - (int)indent), (int)MathF.Ceiling(lineHeight));
                row.Label.Color = node.IsSelectable
                    ? ThemeManager.Colors.InputForegroundPrimary
                    : ThemeManager.Colors.AccentQuaternary;
            }
        }

        /// <summary>
        /// Resets one pooled row that is no longer needed by the current visible hierarchy.
        /// </summary>
        /// <param name="row">Row to reset.</param>
        void DisableRow(SceneHierarchyRow row) {
            row.Entity.Enabled = false;
            row.NodeEntity = null;
            row.IsSceneRoot = false;
            row.IsSelectable = false;
            row.HasChildren = false;
            row.IsExpanded = false;
            row.IsSelected = false;
            row.IsHovering = false;
            row.IsPressed = false;
            row.IsArrowPressed = false;
            row.IsKeyboardFocused = false;
            row.Arrow.Text = string.Empty;
            row.ArrowHitLeft = 0;
            row.ArrowHitWidth = 0;
            row.Label.Text = string.Empty;
            UpdateRowBackground(row, ThemeManager.Colors.SurfacePrimary);
        }

        /// <summary>
        /// Creates one pooled row with background, label, arrow, and pointer input.
        /// </summary>
        /// <returns>Newly created row.</returns>
        SceneHierarchyRow CreateRow() {
            EditorEntity rowEntity = new EditorEntity {
                LayerMask = rootEntity.LayerMask,
                Position = float3.Zero,
                Enabled = false
            };

            SpriteComponent background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = rowBackgroundOrder
            };
            rowEntity.AddComponent(background);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(Size.X, SceneHierarchyPanel.RowHeight)
            };
            rowEntity.AddComponent(interactable);

            EditorEntity arrowHost = new EditorEntity {
                LayerMask = rootEntity.LayerMask,
                Position = new float3(RowPaddingLeft, 2, 0.2f)
            };
            rowEntity.AddChild(arrowHost);

            TextComponent arrow = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(ArrowSlotWidth, SceneHierarchyPanel.RowHeight),
                RenderOrder2D = rowTextOrder
            };
            arrowHost.AddComponent(arrow);

            EditorEntity labelHost = new EditorEntity {
                LayerMask = rootEntity.LayerMask,
                Position = new float3(8, 2, 0.2f)
            };
            rowEntity.AddChild(labelHost);

            TextComponent label = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(100, SceneHierarchyPanel.RowHeight),
                RenderOrder2D = rowTextOrder
            };
            labelHost.AddComponent(label);

            SceneHierarchyRow row = null;
            EditorFocusTarget focusTarget = new EditorFocusTarget(
                null,
                0,
                false,
                () => false,
                point => false,
                isFocused => {
                },
                key => false,
                key => {
                });
            row = new SceneHierarchyRow(rowEntity, background, arrowHost, arrow, labelHost, label, interactable, focusTarget);
            interactable.CursorEvent += (position, delta, state) => HandleRowCursor(row, position, state);
            rootEntity.AddChild(rowEntity);
            return row;
        }

        /// <summary>
        /// Updates row hover, press, and selection behavior for pointer interaction.
        /// </summary>
        /// <param name="row">Row receiving the pointer event.</param>
        /// <param name="point">Pointer position in row-local coordinates.</param>
        /// <param name="state">Pointer interaction state.</param>
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
        /// Applies one explicit expanded-state toggle to the provided row and refreshes the visible hierarchy.
        /// </summary>
        /// <param name="row">Row whose branch should toggle.</param>
        void ToggleExpanded(SceneHierarchyRow row) {
            if (row == null || row.NodeEntity == null || !row.HasChildren) {
                return;
            }

            expandedEntities[row.NodeEntity] = !IsExpanded(row.NodeEntity);
            RebuildHierarchy();
        }

        /// <summary>
        /// Selects one valid parent entity represented by the provided row.
        /// </summary>
        /// <param name="row">Row to activate.</param>
        void ActivateRow(SceneHierarchyRow row) {
            if (row == null || !row.IsSelectable) {
                return;
            }

            SelectedParentEntity = row.IsSceneRoot ? null : row.NodeEntity;
            LayoutRows();

            if (ParentEntitySelected != null) {
                ParentEntitySelected(SelectedParentEntity);
            }
        }

        /// <summary>
        /// Returns true when the provided entity currently has visible children inside the picker.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when at least one visible child is present.</returns>
        bool HasVisibleChildren(Entity entity) {
            if (entity.Children == null) {
                return false;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (visibleEntitySet.Contains(entity.Children[childIndex])) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the provided entity is currently expanded in the picker.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when the entity branch should display descendants.</returns>
        bool IsExpanded(Entity entity) {
            if (expandedEntities.TryGetValue(entity, out bool isExpanded)) {
                return isExpanded;
            }

            return true;
        }

        /// <summary>
        /// Returns true when one entity is not a valid new parent for the current reparent target.
        /// </summary>
        /// <param name="entity">Entity being evaluated as a destination parent.</param>
        /// <returns>True when the entity matches the target or lies inside the target descendant chain.</returns>
        bool IsInvalidParent(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (ReferenceEquals(current, TargetEntity)) {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Returns whether the provided node matches the currently selected parent entity.
        /// </summary>
        /// <param name="node">Node being evaluated.</param>
        /// <returns>True when the node represents the selected parent entity.</returns>
        bool IsRowSelected(NodeInfo node) {
            if (node.IsSceneRoot) {
                return SelectedParentEntity == null;
            }

            return ReferenceEquals(node.Entity, SelectedParentEntity);
        }

        /// <summary>
        /// Applies the correct background color for one picker row based on selection and pointer state.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        /// <param name="baseColor">Base alternating color used when the row is idle.</param>
        void UpdateRowBackground(SceneHierarchyRow row, byte4 baseColor) {
            if (row.IsSelected) {
                row.Background.Color = ThemeManager.Colors.AccentSecondary;
                return;
            }

            if (row.IsPressed && row.IsSelectable) {
                row.Background.Color = ThemeManager.Colors.AccentPrimary;
                return;
            }

            if (row.IsHovering && row.IsSelectable) {
                row.Background.Color = ThemeManager.Colors.AccentSecondary;
                return;
            }

            row.Background.Color = baseColor;
        }

        /// <summary>
        /// Resolves the display label used for one picker entity row.
        /// </summary>
        /// <param name="entity">Entity whose row label should be resolved.</param>
        /// <returns>Display label shown for the entity.</returns>
        string GetEntityDisplayName(Entity entity) {
            if (entity is EditorEntity editorEntity && !string.IsNullOrWhiteSpace(editorEntity.Name)) {
                return editorEntity.Name;
            }

            return entity.GetType().Name;
        }

        /// <summary>
        /// Captures one flattened visible picker row.
        /// </summary>
        readonly struct NodeInfo {
            /// <summary>
            /// Initializes one picker node description.
            /// </summary>
            /// <param name="entity">Scene entity represented by the node, or null for the synthetic scene root row.</param>
            /// <param name="label">Text label shown for the node.</param>
            /// <param name="depth">Hierarchy depth used for indentation.</param>
            /// <param name="hasChildren">True when the row has visible descendants.</param>
            /// <param name="isExpanded">True when the row currently displays its descendants.</param>
            /// <param name="isSelectable">True when the row may currently be activated as a valid reparent target.</param>
            /// <param name="isSceneRoot">True when the row represents the synthetic scene root entry.</param>
            public NodeInfo(Entity entity, string label, int depth, bool hasChildren, bool isExpanded, bool isSelectable, bool isSceneRoot) {
                Entity = entity;
                Label = label;
                Depth = depth;
                HasChildren = hasChildren;
                IsExpanded = isExpanded;
                IsSelectable = isSelectable;
                IsSceneRoot = isSceneRoot;
            }

            /// <summary>
            /// Gets the represented scene entity, or null for the synthetic scene root row.
            /// </summary>
            public Entity Entity { get; }

            /// <summary>
            /// Gets the text label shown for the node.
            /// </summary>
            public string Label { get; }

            /// <summary>
            /// Gets the hierarchy depth used for indentation.
            /// </summary>
            public int Depth { get; }

            /// <summary>
            /// Gets whether the node currently has visible descendants.
            /// </summary>
            public bool HasChildren { get; }

            /// <summary>
            /// Gets whether the node currently displays its descendants.
            /// </summary>
            public bool IsExpanded { get; }

            /// <summary>
            /// Gets whether the node may currently be selected as a valid parent.
            /// </summary>
            public bool IsSelectable { get; }

            /// <summary>
            /// Gets whether the node represents the synthetic scene root row.
            /// </summary>
            public bool IsSceneRoot { get; }
        }
    }
}
