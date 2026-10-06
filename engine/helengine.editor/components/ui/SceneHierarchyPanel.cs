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
        const int ArrowSlotWidth = 20;
        const int ArrowLabelSpacing = 4;
        const int VisibilityIconSize = 14;
        /// <summary>Thickness of the horizontal separators above and below each hierarchy row.</summary>
        const int RowSeparatorThickness = 1;
        /// <summary>Button width and height for hierarchy branch disclosure.</summary>
        const int DisclosureButtonSize = ArrowSlotWidth;
        /// <summary>Rendered width and height for the imported branch chevron.</summary>
        const int DisclosureIconSize = 14;

        FontAsset font;
        /// <summary>Imported chevron texture shared by the pooled disclosure controls.</summary>
        readonly RuntimeTexture DisclosureIconTexture;
        /// <summary>Imported SVG-derived icon textures used for visible and hidden entities.</summary>
        readonly RuntimeTexture VisibilityIconTexture;
        readonly RuntimeTexture HiddenVisibilityIconTexture;
        readonly EditorEntity contentRoot;
        /// <summary>
        /// Hidden entity that owns the Scene Hierarchy content camera.
        /// </summary>
        readonly EditorEntity contentCameraEntity;
        /// <summary>Stationary hit target behind the hierarchy rows that clears selection on empty-space clicks.</summary>
        readonly EditorEntity backgroundHitEntity;
        /// <summary>Pointer target covering the visible body area beneath the title bar.</summary>
        readonly InteractableComponent backgroundInteractable;
        /// <summary>
        /// Camera that renders only the Scene Hierarchy row layer inside the panel body viewport.
        /// </summary>
        readonly CameraComponent contentCameraComponent;
        /// <summary>
        /// Root entity that owns scrollable hierarchy row content rendered by the content camera.
        /// </summary>
        readonly EditorEntity scrollContentRoot;
        /// <summary>
        /// Scroll controller that pages through hierarchy rows inside the panel viewport.
        /// </summary>
        readonly ScrollComponent scrollComponent;
        /// <summary>
        /// Object manager owned by the editor session hosting this panel.
        /// </summary>
        ObjectManager objectManager;
        InputSystem Input;
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
        /// Snapshot of the node list accepted by the most recent row relayout, used to skip redundant
        /// relayouts while the per-frame hierarchy refresh finds no structural changes.
        /// </summary>
        readonly List<NodeInfo> lastLayoutNodes;
        /// <summary>
        /// Snapshot of the row label sources accepted by the most recent row relayout.
        /// </summary>
        readonly List<string> lastLayoutNodeNames;
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
        /// <summary>
        /// Initializes a new scene hierarchy panel with the provided font and shared metrics source.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock chrome and rows.</param>
        public SceneHierarchyPanel(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font)
            : this(ownerCore, interactionServices, font, EditorUiMetrics.Default) { }

        /// <summary>Initializes the hierarchy panel using the host-provided SVG-derived chevron texture.</summary>
        /// <param name="ownerCore">Core that owns this panel's UI entities.</param>
        /// <param name="interactionServices">Session interaction state used for selection and focus.</param>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size rows and controls.</param>
        /// <param name="disclosureIconTexture">PNG texture imported from the editor disclosure SVG.</param>
        public SceneHierarchyPanel(
            Core ownerCore,
            EditorSessionInteractionServices interactionServices,
            FontAsset font,
            EditorUiMetrics metrics,
            RuntimeTexture disclosureIconTexture,
            RuntimeTexture visibilityIconTexture = null,
            RuntimeTexture hiddenVisibilityIconTexture = null)
            : base(ownerCore, interactionServices, font, metrics) {
            this.font = font;
            DisclosureIconTexture = disclosureIconTexture ?? OwnerCore.RenderManager2D.PixelTexture;
            VisibilityIconTexture = visibilityIconTexture ?? OwnerCore.RenderManager2D.PixelTexture;
            HiddenVisibilityIconTexture = hiddenVisibilityIconTexture ?? VisibilityIconTexture;
            Title = "Scene";
            MinSize = new int2(metrics.ScalePixels(220), metrics.ScalePixels(160));

            contentRoot = new EditorEntity(OwnerCore, InteractionServices);
            // Keep the scrollbar in the same clipped camera pass as the rows so its depth places it above their separators.
            contentRoot.LayerMask = EditorLayerMasks.SceneHierarchyContent;
            contentRoot.Position = new float3(0, TitleBarHeightPixels, 0.05f);
            AddChild(contentRoot);

            backgroundHitEntity = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                Position = float3.Zero
            };
            backgroundInteractable = new InteractableComponent();
            backgroundInteractable.CursorEvent += HandleBackgroundCursor;
            backgroundHitEntity.AddComponent(backgroundInteractable);
            contentRoot.AddChild(backgroundHitEntity);

            contentCameraEntity = new EditorEntity(OwnerCore, InteractionServices) {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneHierarchyContent
            };
            contentCameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                CameraDrawOrder = EditorUiCameraDrawOrders.PanelContent,
                ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0)
            };
            contentCameraEntity.AddComponent(contentCameraComponent);

            scrollContentRoot = new EditorEntity(OwnerCore, InteractionServices);
            scrollContentRoot.LayerMask = EditorLayerMasks.SceneHierarchyContent;
            scrollContentRoot.Position = float3.Zero;
            contentRoot.AddChild(scrollContentRoot);

            scrollComponent = new EditorScrollComponent();
            scrollComponent.ShowsPartialTrailingItem = true;
            scrollComponent.ScrollOffsetChanged += HandleScrollOffsetChanged;
            contentRoot.AddComponent(scrollComponent);

            rows = new List<SceneHierarchyRow>(32);
            nodes = new List<NodeInfo>(64);
            lastLayoutNodes = new List<NodeInfo>(64);
            lastLayoutNodeNames = new List<string>(64);
            expandedEntities = new Dictionary<Entity, bool>();
            parentEntities = new HashSet<Entity>();
            hierarchyContextMenu = new ContextMenu(OwnerCore, font, LayerMask, InteractionServices);
            AddChild(hierarchyContextMenu.Entity);
            rowContextMenuItems = new List<ContextMenuItem> {
                new ContextMenuItem("Reparent", HandleReparentRequested)
            };

            EditorSessionInteractionServices.From(this).Selection.SelectionChanged += HandleEditorSelectionChanged;
            AddComponent(new SceneHierarchyPanelUpdater(this));
            isInitialized = true;
            RefreshHierarchy();
            UpdateContentViewport();
            UpdateScrollContentPosition();
        }

        public SceneHierarchyPanel(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font, EditorUiMetrics metrics)
            : this(ownerCore, interactionServices, font, metrics, null) { }

        /// <summary>
        /// Binds hierarchy refresh and scrolling to the owning session's object manager.
        /// </summary>
        internal void SetObjectManager(ObjectManager manager) {
            objectManager = manager ?? throw new ArgumentNullException(nameof(manager));
            scrollComponent.UpdateOrder = objectManager.GetUpdateOrderForLayer(1);
        }

        /// <summary>
        /// Binds hierarchy menu interaction to the owning session's input source.
        /// </summary>
        internal new void SetInput(InputSystem input) {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            hierarchyContextMenu.SetInput(Input);
            base.SetInput(input);
        }

        /// <summary>
        /// Rebuilds the hierarchy view from the current object manager state.
        /// </summary>
        public void RefreshHierarchy() {
            if (nodes == null) {
                return;
            }

            var manager = objectManager;
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

            // This refresh runs every frame; only re-run the row relayout when the node structure or the
            // displayed names actually changed. The viewport and scroll translation still update every
            // frame because they follow the panel position, which changes without any node change.
            if (HasNodeContentChanged()) {
                CaptureNodeLayoutSnapshot();
                scrollComponent.ItemCount = nodes.Count;
                scrollComponent.ClampScrollOffset();
                LayoutRows();
            }
            UpdateContentViewport();
            UpdateScrollContentPosition();
        }

        /// <summary>
        /// Returns whether the freshly rebuilt node list differs from the one accepted by the last relayout.
        /// </summary>
        /// <returns>True when the rows need a relayout.</returns>
        bool HasNodeContentChanged() {
            if (nodes.Count != lastLayoutNodes.Count) {
                return true;
            }

            for (int index = 0; index < nodes.Count; index++) {
                NodeInfo node = nodes[index];
                NodeInfo lastNode = lastLayoutNodes[index];
                if (!ReferenceEquals(node.Entity, lastNode.Entity)
                    || node.Depth != lastNode.Depth
                    || node.HasChildren != lastNode.HasChildren
                    || node.IsExpanded != lastNode.IsExpanded
                    || !string.Equals(ResolveNodeLabelSource(node.Entity), lastLayoutNodeNames[index], StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Captures the node list and label sources accepted by the current relayout.
        /// </summary>
        void CaptureNodeLayoutSnapshot() {
            lastLayoutNodes.Clear();
            lastLayoutNodeNames.Clear();
            for (int index = 0; index < nodes.Count; index++) {
                lastLayoutNodes.Add(nodes[index]);
                lastLayoutNodeNames.Add(ResolveNodeLabelSource(nodes[index].Entity));
            }
        }

        /// <summary>
        /// Resolves the raw label text source for one hierarchy entity.
        /// </summary>
        /// <param name="entity">Entity whose label source should be resolved.</param>
        /// <returns>Label source string for change detection.</returns>
        static string ResolveNodeLabelSource(Entity entity) {
            return entity is EditorEntity editorEntity ? editorEntity.Name : entity.GetType().Name;
        }

        /// <summary>
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title and hierarchy row font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            this.font = font;
            base.ApplyUiMetrics(font, metrics);

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                rows[rowIndex].Arrow.Font = font;
                rows[rowIndex].Label.Font = font;
            }

            RefreshHierarchy();
        }

        /// <summary>
        /// Detaches editor-global event and focus registrations owned by the panel.
        /// </summary>
        public void Detach() {
            EditorSessionInteractionServices.From(this).Selection.SelectionChanged -= HandleEditorSelectionChanged;
            for (int i = 0; i < rows.Count; i++) {
                EditorSessionInteractionServices.From(this).KeyboardFocus.UnregisterTarget(rows[i].FocusTarget);
            }
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
            UpdateContentViewport();
        }

        /// <summary>
        /// Updates scaled hierarchy content offsets after the shared dock chrome metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            MinSize = new int2(UiMetrics.ScalePixels(220), UiMetrics.ScalePixels(160));
            contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
            UpdateContentViewport();
            UpdateScrollContentPosition();
        }

        /// <summary>
        /// Updates hierarchy context-menu input each frame.
        /// </summary>
        internal void UpdateContextMenuInput() {
            InputSystem input = Input;
            if (input == null) {
                return;
            }
            if (!input.WasMouseRightButtonPressed()) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorSessionInteractionServices.From(this).InputCapture.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
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
        /// Refreshes the hierarchy when editor-global entity selection changes.
        /// </summary>
        /// <param name="args">Selection-change payload.</param>
        void HandleEditorSelectionChanged(EditorSelectionChangedEventArgs args) {
            RefreshHierarchy();
            // The refresh skips row relayout when the node structure is unchanged, but a selection change
            // must always repaint the row highlight states.
            LayoutRows();
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
            int rowHeight = GetRowHeightPixels();
            int rowWidth = Math.Max(Size.X, MinSize.X);
            int contentHeight = GetContentHeightPixels();
            int scrollOffset = scrollComponent.ScrollOffset;

            EditorScrollComponentLayout.ConfigureAutomaticVisibleItems(
                scrollComponent,
                new int2(rowWidth, contentHeight),
                rowHeight,
                nodes.Count);
            int visibleRowCount = scrollComponent.VisibleItemCount;

            EnsureRowCount(visibleRowCount);

            float lineHeight = MathF.Max(font.LineHeight, 1f);

            for (int i = 0; i < rows.Count; i++) {
                SceneHierarchyRow row = rows[i];
                row.FocusTarget.TabIndex = i;
                if (i >= visibleRowCount) {
                    DisableRow(row);
                    continue;
                }

                int nodeIndex = scrollOffset + i;
                if (nodeIndex >= nodes.Count) {
                    DisableRow(row);
                    continue;
                }

                NodeInfo node = nodes[nodeIndex];
                row.Entity.Enabled = true;
                row.NodeIndex = nodeIndex;
                row.NodeEntity = node.Entity;
                row.HasChildren = node.HasChildren;
                row.IsExpanded = node.IsExpanded;
                row.IsSelectable = true;
                row.IsSceneRoot = false;
                row.Entity.Position = new float3(0, nodeIndex * rowHeight, 0.1f);
                row.IsSelected = node.Entity == EditorSessionInteractionServices.From(this).Selection.SelectedEntity;

                UpdateRowVisualState(row);

                row.Background.Size = new int2(rowWidth, rowHeight);
                int rowSeparatorThickness = UiMetrics.ScalePixels(RowSeparatorThickness);
                row.TopBorderEntity.Enabled = true;
                row.BottomBorderEntity.Enabled = i == visibleRowCount - 1;
                row.TopBorder.Size = new int2(rowWidth, rowSeparatorThickness);
                row.TopBorderEntity.Position = new float3(0f, 0f, 0.3f);
                row.BottomBorder.Size = new int2(rowWidth, rowSeparatorThickness);
                row.BottomBorderEntity.Position = new float3(0f, rowHeight - rowSeparatorThickness, 0.3f);
                // The camera only clips rendering; the trailing partial row's hit area must not extend past
                // the panel body into whatever sits below it.
                int visibleRowHeight = Math.Min(rowHeight, contentHeight - (i * rowHeight));
                row.Interactable.Size = new int2(rowWidth, Math.Max(0, visibleRowHeight));

                int visibilityLeft = GetRowPaddingLeftPixels();
                int arrowLeft = visibilityLeft + GetArrowSlotWidthPixels() + node.Depth * GetRowIndentPixels();
                LayoutDepthGuides(row, node.Depth, rowHeight, visibilityLeft);
                int disclosureButtonSize = GetDisclosureButtonSizePixels();
                row.VisibilityHitLeft = visibilityLeft;
                row.VisibilityHitWidth = disclosureButtonSize;
                row.VisibilityHitTop = (rowHeight - disclosureButtonSize) / 2;
                row.VisibilityHitHeight = disclosureButtonSize;
                row.VisibilityHost.Position = new float3(
                    visibilityLeft,
                    MathF.Round((rowHeight - disclosureButtonSize) * 0.5f), 0.2f);
                row.VisibilityInteractable.Size = new int2(disclosureButtonSize, disclosureButtonSize);
                row.VisibilityIconEntity.LocalPosition = new float3(
                    (disclosureButtonSize - GetVisibilityIconSizePixels()) * 0.5f,
                    (disclosureButtonSize - GetVisibilityIconSizePixels()) * 0.5f, 0.1f);
                row.VisibilityIcon.Size = new int2(GetVisibilityIconSizePixels(), GetVisibilityIconSizePixels());
                UpdateVisibilityAppearance(row);

                row.ArrowHitLeft = arrowLeft;
                row.ArrowHitWidth = disclosureButtonSize;
                row.ArrowHitTop = (rowHeight - disclosureButtonSize) / 2;
                row.ArrowHitHeight = disclosureButtonSize;
                row.ArrowHost.Position = new float3(
                    arrowLeft,
                    MathF.Round((rowHeight - disclosureButtonSize) * 0.5f), 0.2f);
                row.ArrowHost.Enabled = node.HasChildren;
                row.DisclosureInteractable.Size = new int2(disclosureButtonSize, disclosureButtonSize);
                row.DisclosureBackground.Size = new int2(disclosureButtonSize, disclosureButtonSize);
                row.DisclosureBackground.BorderThickness = UiMetrics.ScalePixels(1);
                row.DisclosureIcon.Size = new int2(GetDisclosureIconSizePixels(), GetDisclosureIconSizePixels());
                row.DisclosureIconEntity.LocalPosition = new float3(
                    (disclosureButtonSize - GetDisclosureIconSizePixels()) * 0.5f,
                    (disclosureButtonSize - GetDisclosureIconSizePixels()) * 0.5f, 0.1f);
                float4 disclosureOrientation;
                float3 disclosureAxis = new float3(0f, 0f, 1f);
                float disclosureAngle = node.IsExpanded ? -MathF.PI * 0.5f : 0f;
                float4.CreateFromAxisAngle(ref disclosureAxis, disclosureAngle, out disclosureOrientation);
                row.DisclosureIconEntity.LocalOrientation = disclosureOrientation;
                UpdateDisclosureAppearance(row);

                float indent = arrowLeft + GetArrowSlotWidthPixels() + GetArrowLabelSpacingPixels();
                row.LabelHost.Position = new float3(indent, MathF.Round((rowHeight - lineHeight) * 0.5f), 0.2f);

                bool isBlueprintInherited = BlueprintEditorReadOnlyService.IsInheritedEntity(node.Entity);
                string label = node.Entity is EditorEntity editorEntity ? editorEntity.Name : node.Entity.GetType().Name;
                row.Label.Text = isBlueprintInherited
                    ? string.Concat(label, " [blueprint]")
                    : label;
                row.Label.Size = new int2(Math.Max(0, rowWidth - (int)indent), (int)MathF.Ceiling(lineHeight));
                row.Label.Color = isBlueprintInherited
                    ? ThemeManager.Colors.InputForegroundSecondary
                    : ThemeManager.Colors.InputForegroundPrimary;
            }

            UpdateDepthGuideAppearance();
        }

        /// <summary>Places one vertical guide in each ancestor disclosure column for this row.</summary>
        /// <param name="row">Visible pooled hierarchy row.</param>
        /// <param name="depth">The row's hierarchy depth.</param>
        /// <param name="rowHeight">Scaled row height.</param>
        /// <param name="visibilityLeft">Fixed left coordinate of the visibility control.</param>
        void LayoutDepthGuides(SceneHierarchyRow row, int depth, int rowHeight, int visibilityLeft) {
            EnsureDepthGuideCount(row, depth);

            int lineWidth = UiMetrics.ScalePixels(RowSeparatorThickness);
            int firstGuideCenter = visibilityLeft + GetArrowSlotWidthPixels() + (GetDisclosureButtonSizePixels() / 2);
            for (int guideIndex = 0; guideIndex < row.DepthGuideEntities.Count; guideIndex++) {
                EditorEntity guideEntity = row.DepthGuideEntities[guideIndex];
                bool isVisible = guideIndex < depth;
                guideEntity.Enabled = isVisible;
                if (!isVisible) {
                    continue;
                }

                guideEntity.Position = new float3(
                    firstGuideCenter + (guideIndex * GetRowIndentPixels()),
                    0f,
                    0.15f);
                SpriteComponent guideSprite = row.DepthGuideSprites[guideIndex];
                guideSprite.Size = new int2(lineWidth, rowHeight);
                guideSprite.Color = ThemeManager.Colors.SurfaceInput;
            }
        }

        /// <summary>Highlights depth guides that belong to a hovered, expanded ancestor branch.</summary>
        void UpdateDepthGuideAppearance() {
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                bool isVisibleRow = row.Entity.Enabled && row.NodeIndex >= 0 && row.NodeIndex < nodes.Count;
                int depth = isVisibleRow ? nodes[row.NodeIndex].Depth : 0;

                for (int guideIndex = 0; guideIndex < row.DepthGuideSprites.Count; guideIndex++) {
                    bool isHighlighted = isVisibleRow &&
                                         guideIndex < depth &&
                                         IsDepthGuideHighlightedByHoveredExpandedAncestor(row.NodeIndex, guideIndex);
                    row.DepthGuideSprites[guideIndex].Color = isHighlighted
                        ? ThemeManager.Colors.AccentSecondary
                        : ThemeManager.Colors.SurfaceInput;
                }
            }
        }

        /// <summary>Checks whether one ancestor's disclosure control owns a guide column hover highlight.</summary>
        /// <param name="nodeIndex">Flattened index of the descendant row that draws the guide.</param>
        /// <param name="guideIndex">Depth column represented by that guide.</param>
        /// <returns>True when the visible ancestor for this column is hovered and expanded.</returns>
        bool IsDepthGuideHighlightedByHoveredExpandedAncestor(int nodeIndex, int guideIndex) {
            int ancestorNodeIndex = nodeIndex - 1;
            while (ancestorNodeIndex >= 0 && nodes[ancestorNodeIndex].Depth > guideIndex) {
                ancestorNodeIndex--;
            }

            if (ancestorNodeIndex < 0 || nodes[ancestorNodeIndex].Depth != guideIndex) {
                return false;
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow ancestorRow = rows[rowIndex];
                if (ancestorRow.Entity.Enabled &&
                    ancestorRow.NodeIndex == ancestorNodeIndex &&
                    ancestorRow.HasChildren &&
                    ancestorRow.IsExpanded &&
                    ancestorRow.IsDisclosureHovering) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Creates pooled vertical guide sprites until this row can show all ancestor levels.</summary>
        /// <param name="row">Pooled hierarchy row that owns the guide sprites.</param>
        /// <param name="depth">Required number of ancestor guides.</param>
        void EnsureDepthGuideCount(SceneHierarchyRow row, int depth) {
            while (row.DepthGuideEntities.Count < depth) {
                EditorEntity guideEntity = new EditorEntity(OwnerCore, InteractionServices) {
                    LayerMask = EditorLayerMasks.SceneHierarchyContent,
                    Position = new float3(0f, 0f, 0.15f)
                };
                SpriteComponent guideSprite = new SpriteComponent {
                    Texture = OwnerCore.RenderManager2D.PixelTexture,
                    Color = ThemeManager.Colors.SurfaceInput
                };
                guideEntity.AddComponent(guideSprite);
                row.Entity.AddChild(guideEntity);
                row.DepthGuideEntities.Add(guideEntity);
                row.DepthGuideSprites.Add(guideSprite);
            }
        }

        /// <summary>
        /// Ensures the row pool has enough entries to render the current hierarchy.
        /// </summary>
        /// <param name="count">Required row count.</param>
        void EnsureRowCount(int count) {
            for (int i = rows.Count; i < count; i++) {
                rows.Add(CreateRow());
            }
        }

        /// <summary>
        /// Resets one pooled row that no longer maps to a visible hierarchy node.
        /// </summary>
        /// <param name="row">Row to disable.</param>
        void DisableRow(SceneHierarchyRow row) {
            row.Entity.Enabled = false;
            for (int guideIndex = 0; guideIndex < row.DepthGuideEntities.Count; guideIndex++) {
                row.DepthGuideEntities[guideIndex].Enabled = false;
            }
            row.NodeIndex = -1;
            row.NodeEntity = null;
            row.HasChildren = false;
            row.IsExpanded = false;
            row.IsHovering = false;
            row.IsPressed = false;
            row.IsArrowPressed = false;
            row.IsVisibilityPressed = false;
            row.IsVisibilityButtonPressed = false;
            row.IsDisclosureButtonPressed = false;
            row.IsDisclosureHovering = false;
            row.IsVisibilityHovering = false;
            row.IsSelected = false;
            row.IsSelectable = false;
            row.IsSceneRoot = false;
            row.ArrowHitLeft = 0;
            row.ArrowHitWidth = 0;
            row.ArrowHitTop = 0;
            row.ArrowHitHeight = 0;
            row.VisibilityHitLeft = 0;
            row.VisibilityHitWidth = 0;
            row.VisibilityHitTop = 0;
            row.VisibilityHitHeight = 0;
            row.Label.Text = string.Empty;
            row.FocusTarget.SetTargetFocused(false);
            UpdateRowVisualState(row);
            UpdateDisclosureAppearance(row);
            UpdateVisibilityAppearance(row);
        }

        /// <summary>
        /// Creates a single row entity with background, label, and hover handling.
        /// </summary>
        /// <returns>Newly created row elements.</returns>
        SceneHierarchyRow CreateRow() {
            var rowEntity = new EditorEntity(OwnerCore, InteractionServices);
            rowEntity.LayerMask = EditorLayerMasks.SceneHierarchyContent;
            rowEntity.Position = float3.Zero;

            var background = new SpriteComponent();
            background.Texture = OwnerCore.RenderManager2D.PixelTexture;
            background.Color = new byte4(255, 255, 255, 0);
            rowEntity.AddComponent(background);

            var topBorderEntity = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                Position = new float3(0f, 0f, 0.3f)
            };
            var topBorder = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
                Size = new int2(Size.X, UiMetrics.ScalePixels(RowSeparatorThickness))
            };
            topBorderEntity.AddComponent(topBorder);
            rowEntity.AddChild(topBorderEntity);

            var bottomBorderEntity = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                Position = new float3(0f, GetRowHeightPixels() - UiMetrics.ScalePixels(RowSeparatorThickness), 0.3f)
            };
            var bottomBorder = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
                Size = new int2(Size.X, UiMetrics.ScalePixels(RowSeparatorThickness))
            };
            bottomBorderEntity.AddComponent(bottomBorder);
            rowEntity.AddChild(bottomBorderEntity);

            var interactable = new InteractableComponent();
            interactable.Size = new int2(Size.X, GetRowHeightPixels());
            rowEntity.AddComponent(interactable);

            var arrowHost = new EditorEntity(OwnerCore, InteractionServices);
            arrowHost.LayerMask = EditorLayerMasks.SceneHierarchyContent;
            arrowHost.Position = float3.Zero;
            rowEntity.AddChild(arrowHost);

            RoundedRectComponent disclosureBackground = new RoundedRectComponent {
                Size = new int2(GetDisclosureButtonSizePixels(), GetDisclosureButtonSizePixels()),
                Radius = 2f,
                BorderThickness = UiMetrics.ScalePixels(1),
                FillColor = new byte4(255, 255, 255, 0),
                BorderColor = ThemeManager.Colors.AccentTertiary
            };
            arrowHost.AddComponent(disclosureBackground);

            InteractableComponent disclosureInteractable = new InteractableComponent {
                Size = new int2(GetDisclosureButtonSizePixels(), GetDisclosureButtonSizePixels()),
                HoverCursor = PointerCursorKind.Hand
            };
            arrowHost.AddComponent(disclosureInteractable);

            EditorEntity disclosureIconEntity = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                Position = new float3(
                    (GetDisclosureButtonSizePixels() - GetDisclosureIconSizePixels()) * 0.5f,
                    (GetDisclosureButtonSizePixels() - GetDisclosureIconSizePixels()) * 0.5f,
                    0.1f)
            };
            arrowHost.AddChild(disclosureIconEntity);
            SpriteComponent disclosureIcon = new SpriteComponent {
                Texture = DisclosureIconTexture,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(GetDisclosureIconSizePixels(), GetDisclosureIconSizePixels())
            };
            disclosureIconEntity.AddComponent(disclosureIcon);

            EditorEntity visibilityHost = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                Position = float3.Zero
            };
            InteractableComponent visibilityInteractable = new InteractableComponent {
                Size = new int2(GetDisclosureButtonSizePixels(), GetDisclosureButtonSizePixels()),
                HoverCursor = PointerCursorKind.Hand
            };
            visibilityHost.AddComponent(visibilityInteractable);
            EditorEntity visibilityIconEntity = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = EditorLayerMasks.SceneHierarchyContent,
                Position = new float3(
                    (GetDisclosureButtonSizePixels() - GetVisibilityIconSizePixels()) * 0.5f,
                    (GetDisclosureButtonSizePixels() - GetVisibilityIconSizePixels()) * 0.5f,
                    0.1f)
            };
            visibilityHost.AddChild(visibilityIconEntity);
            SpriteComponent visibilityIcon = new SpriteComponent {
                Texture = VisibilityIconTexture,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(GetVisibilityIconSizePixels(), GetVisibilityIconSizePixels())
            };
            visibilityIconEntity.AddComponent(visibilityIcon);
            rowEntity.AddChild(visibilityHost);

            var labelHost = new EditorEntity(OwnerCore, InteractionServices);
            labelHost.LayerMask = EditorLayerMasks.SceneHierarchyContent;
            labelHost.Position = new float3(8, 2, 0.2f);
            rowEntity.AddChild(labelHost);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(100, GetRowHeightPixels());
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
                    UpdateRowVisualState(row);
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
                topBorderEntity,
                topBorder,
                bottomBorderEntity,
                bottomBorder,
                arrowHost,
                disclosureBackground,
                disclosureIconEntity,
                disclosureIcon,
                disclosureInteractable,
                visibilityHost,
                visibilityIconEntity,
                visibilityIcon,
                visibilityInteractable,
                labelHost,
                text,
                interactable,
                focusTarget);

            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(row.FocusTarget);
            interactable.CursorEvent += (pos, delta, state) => HandleRowCursor(row, pos, state);
            disclosureInteractable.CursorEvent += (pos, delta, state) => HandleDisclosureCursor(row, state);
            visibilityInteractable.CursorEvent += (pos, delta, state) => HandleVisibilityCursor(row, state);
            scrollContentRoot.AddChild(rowEntity);
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
                    row.IsVisibilityPressed = row.ContainsVisibilityPoint(point);
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = row.IsPressed &&
                                          row.IsHovering &&
                                          !row.IsArrowPressed &&
                                          !row.ContainsArrowPoint(point) &&
                                          !row.IsVisibilityPressed &&
                                          !row.ContainsVisibilityPoint(point);
                    row.IsPressed = false;
                    row.IsArrowPressed = false;
                    row.IsVisibilityPressed = false;
                    if (shouldActivate) {
                        ActivateRow(row);
                    }
                    break;
                case PointerInteraction.Leave:
                    row.IsHovering = false;
                    row.IsPressed = false;
                    row.IsArrowPressed = false;
                    row.IsVisibilityPressed = false;
                    break;
                default:
                    break;
            }

            UpdateRowVisualState(row);
        }

        /// <summary>Updates the disclosure button hover state and toggles only its owning branch.</summary>
        /// <param name="row">Hierarchy row whose dedicated disclosure button received input.</param>
        /// <param name="state">Pointer event raised by the button's own hit target.</param>
        void HandleDisclosureCursor(SceneHierarchyRow row, PointerInteraction state) {
            if (row == null || row.NodeEntity == null || !row.HasChildren) {
                return;
            }
            if (state == PointerInteraction.Hover) {
                row.IsDisclosureHovering = true;
            } else if (state == PointerInteraction.Press) {
                row.IsDisclosureHovering = true;
                row.IsDisclosureButtonPressed = true;
            } else if (state == PointerInteraction.Release) {
                bool shouldToggle = row.IsDisclosureButtonPressed;
                row.IsDisclosureButtonPressed = false;
                if (shouldToggle) {
                    ToggleExpanded(row);
                }
            } else if (state == PointerInteraction.Leave) {
                row.IsDisclosureHovering = false;
                row.IsDisclosureButtonPressed = false;
            }
            UpdateDisclosureAppearance(row);
            UpdateDepthGuideAppearance();
        }

        /// <summary>Toggles editor-only rendering visibility from the row's dedicated eye button.</summary>
        /// <param name="row">Hierarchy row whose entity should be hidden or shown.</param>
        /// <param name="state">Pointer event raised by the eye button.</param>
        void HandleVisibilityCursor(SceneHierarchyRow row, PointerInteraction state) {
            if (row == null || row.NodeEntity == null || !row.Entity.Enabled) {
                return;
            }

            if (state == PointerInteraction.Hover) {
                row.IsVisibilityHovering = true;
            } else if (state == PointerInteraction.Press) {
                row.IsVisibilityHovering = true;
                row.IsVisibilityButtonPressed = true;
            } else if (state == PointerInteraction.Release) {
                bool shouldToggle = row.IsVisibilityButtonPressed;
                row.IsVisibilityButtonPressed = false;
                if (shouldToggle) {
                    row.NodeEntity.RenderSuppressed = !row.NodeEntity.RenderSuppressed;
                    UpdateVisibilityAppearance(row);
                    InteractionServices.SceneMutation.MarkSceneMutated();
                }
            } else if (state == PointerInteraction.Leave) {
                row.IsVisibilityHovering = false;
                row.IsVisibilityButtonPressed = false;
            }

            UpdateVisibilityAppearance(row);
        }

        /// <summary>Clears the scene selection when a click is released over the empty hierarchy background.</summary>
        /// <param name="point">Pointer position relative to the background hit target.</param>
        /// <param name="delta">Pointer movement since the previous input frame.</param>
        /// <param name="state">Pointer interaction raised for the background target.</param>
        void HandleBackgroundCursor(int2 point, int2 delta, PointerInteraction state) {
            if (state != PointerInteraction.Release) {
                return;
            }

            EditorSelectionService selection = EditorSessionInteractionServices.From(this).Selection;
            if (selection.SelectedEntity != null) {
                selection.ClearSelection();
            }
        }

        /// <summary>Applies theme colors to the square and chevron according to pointer hover.</summary>
        /// <param name="row">Pooled row whose control should be styled.</param>
        void UpdateDisclosureAppearance(SceneHierarchyRow row) {
            if (row == null) {
                return;
            }
            row.DisclosureBackground.FillColor = row.IsDisclosureHovering
                ? ThemeManager.Colors.AccentSecondary
                : new byte4(255, 255, 255, 0);
            row.DisclosureBackground.BorderColor = ThemeManager.Colors.AccentTertiary;
            row.DisclosureIcon.Color = ThemeManager.Colors.InputForegroundPrimary;
        }

        /// <summary>Updates the eye texture and tint for the entity's local visibility state.</summary>
        /// <param name="row">Pooled hierarchy row whose eye should be refreshed.</param>
        void UpdateVisibilityAppearance(SceneHierarchyRow row) {
            if (row == null || row.VisibilityIcon == null) {
                return;
            }

            bool isHidden = row.NodeEntity != null && row.NodeEntity.RenderSuppressed;
            row.VisibilityIcon.Texture = isHidden ? HiddenVisibilityIconTexture : VisibilityIconTexture;
            row.VisibilityIcon.Color = row.IsVisibilityHovering
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.InputForegroundPrimary;
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
            if (row == null || row.NodeIndex < 0) {
                return;
            }

            int adjacentIndex = row.NodeIndex + offset;
            if (adjacentIndex < 0 || adjacentIndex >= nodes.Count) {
                return;
            }

            EnsureNodeVisible(adjacentIndex);

            int visibleRowIndex = adjacentIndex - scrollComponent.ScrollOffset;
            if (visibleRowIndex < 0 || visibleRowIndex >= rows.Count) {
                return;
            }

            SceneHierarchyRow adjacentRow = rows[visibleRowIndex];
            if (!adjacentRow.Entity.Enabled || adjacentRow.NodeEntity == null || adjacentRow.NodeIndex != adjacentIndex) {
                return;
            }

            EditorSessionInteractionServices.From(this).KeyboardFocus.SetFocusedTarget(adjacentRow.FocusTarget);
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

            int refreshedNodeIndex = FindNodeIndex(entity);
            if (refreshedNodeIndex >= 0) {
                EnsureNodeVisible(refreshedNodeIndex);
            }

            SceneHierarchyRow refreshedRow = FindVisibleRow(entity);
            if (refreshedRow != null) {
                EditorSessionInteractionServices.From(this).KeyboardFocus.SetFocusedTarget(refreshedRow.FocusTarget);
            }
        }

        /// <summary>
        /// Updates a row's hover background and horizontal separator colors from its interaction state.
        /// </summary>
        /// <param name="row">Row whose visuals should update.</param>
        void UpdateRowVisualState(SceneHierarchyRow row) {
            row.Background.Color = row.IsPressed
                ? ThemeManager.Colors.AccentPrimary
                : row.IsSelected
                    ? ThemeManager.Colors.AccentSecondary
                    : row.IsHovering
                        ? ThemeManager.Colors.AccentSecondary
                        : new byte4(255, 255, 255, 0);
            byte4 separatorColor = ThemeManager.Colors.SurfaceInput;
            if (row.IsKeyboardFocused) {
                separatorColor = ThemeManager.Colors.AccentTertiary;
            }

            row.TopBorder.Color = separatorColor;
            row.BottomBorder.Color = separatorColor;
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

            EditorSessionInteractionServices.From(this).Selection.SetSelectedEntity(row.NodeEntity);
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
        /// Finds the flattened hierarchy node index for the provided entity.
        /// </summary>
        /// <param name="entity">Entity to locate.</param>
        /// <returns>Flattened node index when found; otherwise -1.</returns>
        int FindNodeIndex(Entity entity) {
            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++) {
                if (ReferenceEquals(nodes[nodeIndex].Entity, entity)) {
                    return nodeIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Adjusts the scroll offset so the requested flattened node lies inside the visible viewport.
        /// </summary>
        /// <param name="nodeIndex">Flattened node index that should remain visible.</param>
        void EnsureNodeVisible(int nodeIndex) {
            int visibleRowCount = Math.Max(1, scrollComponent.VisibleItemCount);
            int scrollOffset = scrollComponent.ScrollOffset;
            if (nodeIndex < scrollOffset) {
                scrollComponent.ScrollTo(nodeIndex);
                return;
            }

            int visibleEndIndex = scrollOffset + visibleRowCount - 1;
            if (nodeIndex > visibleEndIndex) {
                scrollComponent.ScrollTo(nodeIndex - visibleRowCount + 1);
            }
        }

        /// <summary>
        /// Resolves the host region used to clamp hierarchy context menus.
        /// </summary>
        /// <returns>Host size for context-menu layout.</returns>
        int2 GetContextMenuHostSize() {
            return new int2(
                Math.Max(Size.X, MinSize.X),
                Math.Max(Size.Y + TitleBarHeightPixels, MinSize.Y + TitleBarHeightPixels));
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
                   pointer.Y >= panelY + TitleBarHeightPixels &&
                   pointer.Y < panelY + TitleBarHeightPixels + panelHeight;
        }

        /// <summary>
        /// Tries to resolve one visible row for the provided screen-space pointer.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <param name="row">Resolved row when one is found.</param>
        /// <returns>True when a visible hierarchy row was found.</returns>
        bool TryGetRowAtScreenPoint(int2 pointer, out SceneHierarchyRow row) {
            if (!IsPointerInsideContent(pointer)) {
                row = null;
                return false;
            }

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
                    pointer.Y < rowPosition.Y + GetRowHeightPixels()) {
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
            if (!IsPointerInsideContent(point)) {
                return false;
            }

            float3 position = row.Entity.Position;
            int rowWidth = Math.Max(Size.X, MinSize.X);
            return point.X >= position.X &&
                   point.X < position.X + rowWidth &&
                   point.Y >= position.Y &&
                   point.Y < position.Y + GetRowHeightPixels();
        }

        /// <summary>
        /// Rebuilds the visible row slice after the wheel-driven scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll controller that raised the change notification.</param>
        /// <param name="scrollOffset">Current visible node offset.</param>
        void HandleScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            UpdateScrollContentPosition();
            LayoutRows();
        }

        /// <summary>
        /// Gets the scaled row height used by hierarchy rows.
        /// </summary>
        /// <returns>Scaled row height in pixels.</returns>
        int GetRowHeightPixels() {
            return UiMetrics.ScalePixels(RowHeight);
        }

        /// <summary>
        /// Updates the content camera viewport to match the current panel body rectangle.
        /// </summary>
        void UpdateContentViewport() {
            float viewportX = Position.X;
            float viewportY = Position.Y + TitleBarHeightPixels;
            float viewportWidth = Math.Max(1, Size.X);
            float viewportHeight = Math.Max(1, Size.Y);
            contentCameraComponent.Viewport = new float4(viewportX, viewportY, viewportWidth, viewportHeight);
            backgroundInteractable.Size = new int2((int)viewportWidth, (int)viewportHeight);
        }

        /// <summary>
        /// Updates the scroll-content root position from the current item scroll offset.
        /// </summary>
        void UpdateScrollContentPosition() {
            scrollContentRoot.Position = new float3(0f, -(scrollComponent.ScrollOffset * GetRowHeightPixels()), 0.1f);
        }

        /// <summary>
        /// Gets the scrollable content height available for hierarchy rows.
        /// </summary>
        /// <returns>Content viewport height in pixels.</returns>
        int GetContentHeightPixels() {
            return Math.Max(Size.Y, MinSize.Y);
        }

        /// <summary>
        /// Gets the scaled indentation added per hierarchy depth.
        /// </summary>
        /// <returns>Scaled indentation width in pixels.</returns>
        int GetRowIndentPixels() {
            return UiMetrics.ScalePixels(RowIndent);
        }

        /// <summary>
        /// Gets the scaled left padding reserved before the row arrow slot.
        /// </summary>
        /// <returns>Scaled left padding in pixels.</returns>
        int GetRowPaddingLeftPixels() {
            return UiMetrics.ScalePixels(RowPaddingLeft);
        }

        /// <summary>
        /// Gets the scaled arrow-slot width.
        /// </summary>
        /// <returns>Scaled arrow-slot width in pixels.</returns>
        int GetArrowSlotWidthPixels() {
            return UiMetrics.ScalePixels(ArrowSlotWidth);
        }

        /// <summary>Returns the scaled square size used for the disclosure button.</summary>
        /// <returns>Disclosure button width and height in UI pixels.</returns>
        int GetDisclosureButtonSizePixels() {
            return UiMetrics.ScalePixels(DisclosureButtonSize);
        }

        /// <summary>Returns the scaled display size used for the imported disclosure chevron.</summary>
        /// <returns>Chevron width and height in UI pixels.</returns>
        int GetDisclosureIconSizePixels() {
            return UiMetrics.ScalePixels(DisclosureIconSize);
        }

        /// <summary>Returns the scaled display size of the visibility eye icon.</summary>
        /// <returns>Icon width and height in UI pixels.</returns>
        int GetVisibilityIconSizePixels() {
            return UiMetrics.ScalePixels(VisibilityIconSize);
        }

        /// <summary>
        /// Gets the scaled spacing preserved between the arrow slot and row label.
        /// </summary>
        /// <returns>Scaled arrow-to-label spacing in pixels.</returns>
        int GetArrowLabelSpacingPixels() {
            return UiMetrics.ScalePixels(ArrowLabelSpacing);
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


