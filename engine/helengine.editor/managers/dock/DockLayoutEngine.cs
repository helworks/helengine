namespace helengine.editor {
    /// <summary>
    /// Lays out dockable editor entities using a split-tree layout
    /// </summary>
    public class DockLayoutEngine {
        const int EdgeMinThreshold = 16;
        const float EdgeBandFraction = 0.15f;
        const float CenterFraction = 0.4f;
        const int CenterMinSize = 24;
        const int PreviewMargin = 8;
        /// <summary>
        /// Pixel distance from a split line that enables resize interactions.
        /// </summary>
        const int ResizeHandleThreshold = 5;

        readonly List<DockableEntity> dockables;
        readonly int padding;
        readonly int gap;
        /// <summary>
        /// Active split node being resized, or null when no resize is in progress.
        /// </summary>
        SplitNode? activeResizeNode;
        /// <summary>
        /// Tracks whether the active resize handle is vertical (left/right) or horizontal (top/bottom).
        /// </summary>
        bool activeResizeVertical;

        LayoutNode? root;

        /// <summary>
        /// Creates a new layout engine with optional padding and gap values.
        /// </summary>
        /// <param name="padding">Space to leave around the host bounds.</param>
        /// <param name="gap">Space inserted between docked panels.</param>
        public DockLayoutEngine(int padding = 0, int gap = 0) {
            this.padding = padding;
            this.gap = gap;
            dockables = new List<DockableEntity>(8);
        }

        /// <summary>
        /// Gets the dockable entities currently tracked by the layout engine.
        /// </summary>
        public IReadOnlyList<DockableEntity> Dockables => dockables;

        /// <summary>
        /// Gets a value indicating whether any panel is currently docked.
        /// </summary>
        public bool HasDocked => root != null;
        /// <summary>
        /// Gets a value indicating whether a split handle is actively being resized.
        /// </summary>
        public bool IsResizing => activeResizeNode != null;
        /// <summary>
        /// Gets a value indicating whether the active resize handle is vertical.
        /// </summary>
        public bool ActiveResizeIsVertical => activeResizeVertical;

        /// <summary>
        /// Adds a dockable entity to the layout list if it is not already tracked.
        /// </summary>
        /// <param name="entity">Entity to add.</param>
        public void Add(DockableEntity entity) {
            if (!dockables.Contains(entity)) {
                dockables.Add(entity);
            }
        }

        /// <summary>
        /// Removes a dockable entity from tracking and undocks it.
        /// </summary>
        /// <param name="entity">Entity to remove.</param>
        /// <returns>True if the entity was removed; otherwise false.</returns>
        public bool Remove(DockableEntity entity) {
            bool removed = dockables.Remove(entity);
            if (root != null) {
                root = root.Remove(entity);
            }

            EndResize();
            entity.IsDocked = false;
            return removed;
        }

        /// <summary>
        /// Undocks the specified entity and reflows the layout tree.
        /// </summary>
        /// <param name="entity">Entity to undock.</param>
        public void Undock(DockableEntity entity) {
            if (root != null) {
                root = root.Remove(entity);
            }

            EndResize();
            entity.IsDocked = false;
        }

        /// <summary>
        /// Docks an entity as the root panel, replacing any existing layout.
        /// </summary>
        /// <param name="entity">Entity to dock as root.</param>
        public void DockAsRoot(DockableEntity entity) {
            root = new PanelNode(entity);
            EndResize();
            entity.IsDocked = true;
        }

        /// <summary>
        /// Docks an entity relative to an anchor panel in the specified direction.
        /// </summary>
        /// <param name="entity">Entity to dock.</param>
        /// <param name="anchor">Anchor panel to split.</param>
        /// <param name="direction">Direction to insert.</param>
        /// <param name="splitFraction">Fraction of the anchor allocated to the incoming panel.</param>
        public void DockRelative(DockableEntity entity, DockableEntity anchor, DockInsertDirection direction, float splitFraction = 0.5f) {
            Dock(entity, new DockHint(direction, anchor, float3.Zero, new int2(0, 0), splitFraction));
        }

        /// <summary>
        /// Docks an entity using a prepared hint generated from a pointer preview.
        /// </summary>
        /// <param name="entity">Entity to dock.</param>
        /// <param name="hint">Docking hint describing the target and direction.</param>
        public void Dock(DockableEntity entity, DockHint hint) {
            if (entity == null) {
                return;
            }

            if (root != null) {
                root = root.Remove(entity);
            }

            root = InsertDock(root, entity, hint);
            EndResize();
            entity.IsDocked = true;
        }

        /// <summary>
        /// Performs layout using the host size at the origin.
        /// </summary>
        /// <param name="hostSize">Size of the host area.</param>
        public void Layout(int2 hostSize) {
            Layout(hostSize, float3.Zero);
        }

        /// <summary>
        /// Performs layout using the host size and a specified origin.
        /// </summary>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin point of the host area.</param>
        public void Layout(int2 hostSize, float3 origin) {
            if (root == null) {
                return;
            }

            float left = origin.X + padding;
            float top = origin.Y + padding;
            float right = origin.X + hostSize.X - padding;
            float bottom = origin.Y + hostSize.Y - padding;

            if (right <= left || bottom <= top) {
                return;
            }

            root.Layout(left, top, right, bottom, origin.Z, gap);
        }

        /// <summary>
        /// Gets the minimum host size required to satisfy all docked panel minimum sizes.
        /// </summary>
        /// <returns>Minimum host size in pixels.</returns>
        public int2 GetMinimumHostSize() {
            if (root == null) {
                return new int2(1, 1);
            }

            GetMinSize(root, out int minWidth, out int minHeight);
            int paddedWidth = minWidth + padding * 2;
            int paddedHeight = minHeight + padding * 2;
            return new int2(Math.Max(1, paddedWidth), Math.Max(1, paddedHeight));
        }

        /// <summary>
        /// Attempts to find a resize handle under the pointer and returns its axis.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <param name="isVertical">True when the handle separates left/right panels.</param>
        /// <returns>True if a resize handle is under the pointer; otherwise false.</returns>
        public bool TryGetResizeAxis(int2 pointer, int2 hostSize, float3 origin, out bool isVertical) {
            isVertical = false;
            if (root == null || !IsPointerWithinHost(pointer, hostSize, origin)) {
                return false;
            }

            SplitNode? best = null;
            bool bestVertical = false;
            float bestDistance = float.MaxValue;
            FindResizeHandle(root, pointer.X, pointer.Y, ResizeHandleThreshold, ref best, ref bestVertical, ref bestDistance);
            if (best == null) {
                return false;
            }

            isVertical = bestVertical;
            return true;
        }

        /// <summary>
        /// Attempts to begin resizing a split handle under the pointer.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <param name="isVertical">True when the handle separates left/right panels.</param>
        /// <returns>True if resizing began; otherwise false.</returns>
        public bool TryBeginResize(int2 pointer, int2 hostSize, float3 origin, out bool isVertical) {
            isVertical = false;
            if (root == null || !IsPointerWithinHost(pointer, hostSize, origin)) {
                return false;
            }

            SplitNode? best = null;
            bool bestVertical = false;
            float bestDistance = float.MaxValue;
            FindResizeHandle(root, pointer.X, pointer.Y, ResizeHandleThreshold, ref best, ref bestVertical, ref bestDistance);
            if (best == null) {
                return false;
            }

            activeResizeNode = best;
            activeResizeVertical = bestVertical;
            isVertical = bestVertical;
            return true;
        }

        /// <summary>
        /// Updates the active resize split using the current pointer position.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        public void UpdateResize(int2 pointer) {
            if (activeResizeNode == null) {
                return;
            }

            float left = activeResizeNode.BoundsLeft;
            float top = activeResizeNode.BoundsTop;
            float right = activeResizeNode.BoundsRight;
            float bottom = activeResizeNode.BoundsBottom;

            float width = MathF.Max(1f, right - left);
            float height = MathF.Max(1f, bottom - top);

            float fraction = activeResizeVertical
                ? (pointer.X - left) / width
                : (pointer.Y - top) / height;

            activeResizeNode.SetSplitFraction(fraction);
        }

        /// <summary>
        /// Ends any active resize operation.
        /// </summary>
        public void EndResize() {
            activeResizeNode = null;
        }

        /// <summary>
        /// Determines whether the pointer is within the dock host bounds.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <returns>True when the pointer is inside the host bounds.</returns>
        bool IsPointerWithinHost(int2 pointer, int2 hostSize, float3 origin) {
            float hostLeft = origin.X + padding;
            float hostTop = origin.Y + padding;
            float hostRight = origin.X + hostSize.X - padding;
            float hostBottom = origin.Y + hostSize.Y - padding;

            return pointer.X >= hostLeft &&
                   pointer.Y >= hostTop &&
                   pointer.X <= hostRight &&
                   pointer.Y <= hostBottom;
        }

        /// <summary>
        /// Searches the layout tree for the closest resize handle to the pointer.
        /// </summary>
        /// <param name="node">Layout node to search.</param>
        /// <param name="x">Pointer X position.</param>
        /// <param name="y">Pointer Y position.</param>
        /// <param name="threshold">Pixel distance allowed from the split line.</param>
        /// <param name="best">Best matching split node found so far.</param>
        /// <param name="bestVertical">Axis of the best match found so far.</param>
        /// <param name="bestDistance">Distance of the best match found so far.</param>
        void FindResizeHandle(
            LayoutNode node,
            float x,
            float y,
            int threshold,
            ref SplitNode? best,
            ref bool bestVertical,
            ref float bestDistance) {
            if (node is SplitNode split) {
                if (TryGetResizeDistance(split, x, y, threshold, out bool isVertical, out float distance) &&
                    distance < bestDistance) {
                    best = split;
                    bestVertical = isVertical;
                    bestDistance = distance;
                }

                FindResizeHandle(split.First, x, y, threshold, ref best, ref bestVertical, ref bestDistance);
                FindResizeHandle(split.Second, x, y, threshold, ref best, ref bestVertical, ref bestDistance);
            }
        }

        /// <summary>
        /// Checks whether the pointer is near a split line and returns the distance.
        /// </summary>
        /// <param name="split">Split node to evaluate.</param>
        /// <param name="x">Pointer X position.</param>
        /// <param name="y">Pointer Y position.</param>
        /// <param name="threshold">Pixel distance allowed from the split line.</param>
        /// <param name="isVertical">True when the split is vertical.</param>
        /// <param name="distance">Distance from the split line when matched.</param>
        /// <returns>True if the pointer is close enough to resize.</returns>
        bool TryGetResizeDistance(
            SplitNode split,
            float x,
            float y,
            int threshold,
            out bool isVertical,
            out float distance) {
            isVertical = split.IsVertical;
            distance = float.MaxValue;

            float left = split.BoundsLeft;
            float top = split.BoundsTop;
            float right = split.BoundsRight;
            float bottom = split.BoundsBottom;

            if (isVertical) {
                if (y < top || y > bottom) {
                    return false;
                }

                float lineX = left + (right - left) * split.SplitFraction;
                distance = MathF.Abs(x - lineX);
                return distance <= threshold;
            }

            if (x < left || x > right) {
                return false;
            }

            float lineY = top + (bottom - top) * split.SplitFraction;
            distance = MathF.Abs(y - lineY);
            return distance <= threshold;
        }

        /// <summary>
        /// Computes the minimum size required for a layout node.
        /// </summary>
        /// <param name="node">Node to inspect.</param>
        /// <param name="minWidth">Minimum width in pixels.</param>
        /// <param name="minHeight">Minimum height in pixels.</param>
        static void GetMinSize(LayoutNode node, out int minWidth, out int minHeight) {
            if (node is PanelNode panel) {
                panel.GetMinimumSize(out minWidth, out minHeight);
                return;
            }

            if (node is SplitNode split) {
                GetMinSize(split.First, out int firstWidth, out int firstHeight);
                GetMinSize(split.Second, out int secondWidth, out int secondHeight);
                int gap = Math.Max(0, split.CachedGap);

                if (split.IsVertical) {
                    minWidth = firstWidth + secondWidth + gap;
                    minHeight = Math.Max(firstHeight, secondHeight);
                } else {
                    minWidth = Math.Max(firstWidth, secondWidth);
                    minHeight = firstHeight + secondHeight + gap;
                }
                return;
            }

            minWidth = 1;
            minHeight = 1;
        }

        /// <summary>
        /// Calculates a dock hint based on a pointer position within the host area.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <param name="fillOnly">True to only allow fill hints.</param>
        /// <param name="hint">Resulting docking hint.</param>
        /// <returns>True if a dock hint could be determined; otherwise false.</returns>
        public bool TryGetDockHint(int2 pointer, int2 hostSize, float3 origin, bool fillOnly, out DockHint hint) {
            hint = default;

            if (hostSize.X <= 0 || hostSize.Y <= 0) {
                return false;
            }

            float hostLeft = origin.X + padding;
            float hostTop = origin.Y + padding;
            float hostRight = origin.X + hostSize.X - padding;
            float hostBottom = origin.Y + hostSize.Y - padding;

            if (pointer.X < hostLeft || pointer.Y < hostTop || pointer.X > hostRight || pointer.Y > hostBottom) {
                return false;
            }

            PanelNode? anchorNode = root?.Hit(pointer.X, pointer.Y);
            DockableEntity? anchorEntity = anchorNode?.Entity;

            float targetLeft = anchorNode?.Bounds.X ?? hostLeft;
            float targetTop = anchorNode?.Bounds.Y ?? hostTop;
            float targetWidth = anchorNode?.Bounds.Z ?? (hostRight - hostLeft);
            float targetHeight = anchorNode?.Bounds.W ?? (hostBottom - hostTop);

            float localX = pointer.X - targetLeft;
            float localY = pointer.Y - targetTop;

            int centerWidth = Math.Max(CenterMinSize, (int)MathF.Round(targetWidth * CenterFraction));
            int centerHeight = Math.Max(CenterMinSize, (int)MathF.Round(targetHeight * CenterFraction));
            float centerStartX = targetLeft + (targetWidth - centerWidth) * 0.5f;
            float centerStartY = targetTop + (targetHeight - centerHeight) * 0.5f;
            float centerEndX = centerStartX + centerWidth;
            float centerEndY = centerStartY + centerHeight;

            if (fillOnly) {
                int previewWidth = Math.Max(1, (int)(targetWidth - PreviewMargin * 2));
                int previewHeight = Math.Max(1, (int)(targetHeight - PreviewMargin * 2));
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Fill, anchorEntity, pos, new int2(previewWidth, previewHeight), 1f);
                return true;
            }

            int horizontalBand = Math.Max(EdgeMinThreshold, (int)MathF.Round(targetWidth * EdgeBandFraction));
            int verticalBand = Math.Max(EdgeMinThreshold, (int)MathF.Round(targetHeight * EdgeBandFraction));

            if (localX <= horizontalBand) {
                int width = Math.Max(1, (int)MathF.Round(targetWidth * 0.5f) - PreviewMargin * 2);
                int height = Math.Max(1, (int)(targetHeight - PreviewMargin * 2));
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Left, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (localX >= targetWidth - horizontalBand) {
                int width = Math.Max(1, (int)MathF.Round(targetWidth * 0.5f) - PreviewMargin * 2);
                int height = Math.Max(1, (int)(targetHeight - PreviewMargin * 2));
                float3 pos = new float3(targetLeft + targetWidth - MathF.Round(targetWidth * 0.5f) + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Right, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (localY <= verticalBand) {
                int width = Math.Max(1, (int)(targetWidth - PreviewMargin * 2));
                int height = Math.Max(1, (int)MathF.Round(targetHeight * 0.5f) - PreviewMargin * 2);
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Top, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (localY >= targetHeight - verticalBand) {
                int width = Math.Max(1, (int)(targetWidth - PreviewMargin * 2));
                int height = Math.Max(1, (int)MathF.Round(targetHeight * 0.5f) - PreviewMargin * 2);
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + targetHeight - MathF.Round(targetHeight * 0.5f) + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Bottom, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (pointer.X >= centerStartX && pointer.X <= centerEndX &&
                pointer.Y >= centerStartY && pointer.Y <= centerEndY) {
                int width = Math.Max(1, (int)(centerWidth - PreviewMargin * 2));
                int height = Math.Max(1, (int)(centerHeight - PreviewMargin * 2));
                float3 pos = new float3(centerStartX + PreviewMargin, centerStartY + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Fill, anchorEntity, pos, new int2(width, height));
                return true;
            }

            return false;
        }

        LayoutNode InsertDock(LayoutNode? currentRoot, DockableEntity entity, DockHint hint) {
            PanelNode newPanel = new PanelNode(entity);

            if (currentRoot == null) {
                return newPanel;
            }

            PanelNode? anchorNode = hint.Anchor != null ? currentRoot.Find(hint.Anchor) : currentRoot.FirstLeaf();
            if (anchorNode == null) {
                anchorNode = currentRoot.FirstLeaf();
            }

            if (anchorNode == null) {
                return newPanel;
            }

            LayoutNode replacement;
            switch (hint.Direction) {
                case DockInsertDirection.Left:
                    replacement = new SplitNode(isVertical: true, hint.SplitFraction, newPanel, anchorNode);
                    break;
                case DockInsertDirection.Right:
                    replacement = new SplitNode(isVertical: true, hint.SplitFraction, anchorNode, newPanel);
                    break;
                case DockInsertDirection.Top:
                    replacement = new SplitNode(isVertical: false, hint.SplitFraction, newPanel, anchorNode);
                    break;
                case DockInsertDirection.Bottom:
                    replacement = new SplitNode(isVertical: false, hint.SplitFraction, anchorNode, newPanel);
                    break;
                case DockInsertDirection.Fill:
                default:
                    anchorNode.AddTab(entity);
                    return currentRoot;
            }

            return Replace(currentRoot, anchorNode, replacement);
        }

        static LayoutNode Replace(LayoutNode node, PanelNode target, LayoutNode replacement) {
            if (ReferenceEquals(node, target)) {
                return replacement;
            }

            if (node is SplitNode split) {
                split.First = Replace(split.First, target, replacement);
                split.Second = Replace(split.Second, target, replacement);
            }

            return node;
        }

        /// <summary>
        /// Represents a node in the docking layout tree.
        /// </summary>
        abstract class LayoutNode {
            public abstract void Layout(float left, float top, float right, float bottom, float z, int gap);
            public abstract PanelNode? Hit(float x, float y);
            public abstract LayoutNode? Remove(DockableEntity entity);
            public abstract PanelNode? Find(DockableEntity entity);
            public abstract PanelNode? FirstLeaf();
        }

        /// <summary>
        /// Represents a docked panel leaf node.
        /// </summary>
        sealed class PanelNode : LayoutNode {
            /// <summary>
            /// Collection of dockable windows grouped into tabs.
            /// </summary>
            readonly List<DockableEntity> tabs;
            /// <summary>
            /// Tab strip UI used when multiple dockables share this panel.
            /// </summary>
            DockTabStrip? tabStrip;
            /// <summary>
            /// Active tab index within the group.
            /// </summary>
            int activeTabIndex;

            /// <summary>
            /// Initializes a new panel node with a single dockable tab.
            /// </summary>
            /// <param name="entity">Initial dockable window for the panel.</param>
            public PanelNode(DockableEntity entity) {
                tabs = new List<DockableEntity>(2) { entity };
                activeTabIndex = 0;
                Bounds = new float4(0, 0, 0, 0);
            }

            /// <summary>
            /// Gets the active dockable entity for this panel.
            /// </summary>
            public DockableEntity Entity => tabs[activeTabIndex];

            /// <summary>
            /// Gets the cached bounds for this panel.
            /// </summary>
            public float4 Bounds { get; private set; }

            /// <summary>
            /// Adds a dockable window as a new tab and makes it active.
            /// </summary>
            /// <param name="entity">Dockable entity to add.</param>
            public void AddTab(DockableEntity entity) {
                int existingIndex = tabs.IndexOf(entity);
                if (existingIndex >= 0) {
                    SetActiveTab(existingIndex);
                    return;
                }

                tabs.Add(entity);
                activeTabIndex = tabs.Count - 1;
                entity.IsDocked = true;
                ApplyTabVisibility();
            }

            /// <summary>
            /// Sets the active tab index and refreshes visibility.
            /// </summary>
            /// <param name="index">Index to activate.</param>
            public void SetActiveTab(int index) {
                if (tabs.Count == 0) {
                    return;
                }

                int clamped = Math.Clamp(index, 0, tabs.Count - 1);
                if (activeTabIndex != clamped) {
                    activeTabIndex = clamped;
                }

                ApplyTabVisibility();
            }

            /// <summary>
            /// Computes the minimum size required by this panel based on its tabs.
            /// </summary>
            /// <param name="minWidth">Minimum width in pixels.</param>
            /// <param name="minHeight">Minimum height in pixels.</param>
            public void GetMinimumSize(out int minWidth, out int minHeight) {
                int maxWidth = 1;
                int maxHeight = 1;

                for (int i = 0; i < tabs.Count; i++) {
                    DockableEntity tab = tabs[i];
                    maxWidth = Math.Max(maxWidth, tab.MinSize.X);
                    maxHeight = Math.Max(maxHeight, tab.MinSize.Y);
                }

                minWidth = Math.Max(1, maxWidth);
                minHeight = Math.Max(1, maxHeight + DockableEntity.TitleBarHeight);
            }

            public override void Layout(float left, float top, float right, float bottom, float z, int gap) {
                float width = Math.Max(1, right - left);
                float height = Math.Max(1, bottom - top);
                Bounds = new float4(left, top, width, height);

                int targetWidth = Math.Max(1, (int)MathF.Round(width));
                int targetHeight = Math.Max(1, (int)MathF.Round(height - DockableEntity.TitleBarHeight));

                ApplyTabVisibility();

                for (int i = 0; i < tabs.Count; i++) {
                    DockableEntity tab = tabs[i];
                    tab.Position = new float3(left, top, z);
                    tab.Size = new int2(targetWidth, targetHeight);
                    tab.IsDocked = true;
                }

                UpdateTabStrip(left, top, z, targetWidth);
            }

            public override PanelNode? Hit(float x, float y) {
                if (x >= Bounds.X && x <= Bounds.X + Bounds.Z &&
                    y >= Bounds.Y && y <= Bounds.Y + Bounds.W) {
                    return this;
                }

                return null;
            }

            public override LayoutNode? Remove(DockableEntity entity) {
                int index = tabs.IndexOf(entity);
                if (index < 0) {
                    return this;
                }

                tabs.RemoveAt(index);
                entity.IsDocked = false;
                entity.Enabled = true;
                entity.SetTitleTextVisible(true);
                entity.SetTitleBarInteractableEnabled(true);

                if (tabs.Count == 0) {
                    tabStrip?.Hide();
                    return null;
                }

                if (activeTabIndex >= tabs.Count) {
                    activeTabIndex = tabs.Count - 1;
                }

                ApplyTabVisibility();
                if (tabs.Count <= 1) {
                    tabStrip?.Hide();
                }

                return this;
            }

            public override PanelNode? Find(DockableEntity entity) {
                for (int i = 0; i < tabs.Count; i++) {
                    if (ReferenceEquals(tabs[i], entity)) {
                        return this;
                    }
                }
                return null;
            }

            public override PanelNode? FirstLeaf() {
                return this;
            }

            /// <summary>
            /// Ensures only the active tab is visible and hides title text when tabbed.
            /// </summary>
            void ApplyTabVisibility() {
                if (tabs.Count == 0) {
                    return;
                }

                int clamped = Math.Clamp(activeTabIndex, 0, tabs.Count - 1);
                activeTabIndex = clamped;
                bool showTitle = tabs.Count <= 1;

                for (int i = 0; i < tabs.Count; i++) {
                    DockableEntity tab = tabs[i];
                    bool isActive = i == activeTabIndex;
                    tab.Enabled = isActive;
                    tab.SetTitleTextVisible(showTitle && isActive);
                    tab.SetTitleBarInteractableEnabled(showTitle && isActive);
                }
            }

            /// <summary>
            /// Updates the tab strip UI to match the current dockable group.
            /// </summary>
            /// <param name="left">Left edge of the panel.</param>
            /// <param name="top">Top edge of the panel.</param>
            /// <param name="z">Depth offset for UI elements.</param>
            /// <param name="width">Width of the panel.</param>
            void UpdateTabStrip(float left, float top, float z, int width) {
                if (tabs.Count <= 1) {
                    tabStrip?.Hide();
                    return;
                }

                if (tabStrip == null) {
                    tabStrip = new DockTabStrip(Entity.TitleFont, SetActiveTab);
                }

                DockableEntity active = Entity;
                tabStrip.UpdateTabs(tabs, activeTabIndex, new float3(left, top, z), width, active.LayerMask);
            }
        }

        /// <summary>
        /// Represents a split between two child layout nodes.
        /// </summary>
        sealed class SplitNode : LayoutNode {
            readonly bool isVertical;
            /// <summary>
            /// Cached left edge of the node during layout.
            /// </summary>
            float boundsLeft;
            /// <summary>
            /// Cached top edge of the node during layout.
            /// </summary>
            float boundsTop;
            /// <summary>
            /// Cached right edge of the node during layout.
            /// </summary>
            float boundsRight;
            /// <summary>
            /// Cached bottom edge of the node during layout.
            /// </summary>
            float boundsBottom;
            /// <summary>
            /// Cached gap value used for layout.
            /// </summary>
            int cachedGap;

            public SplitNode(bool isVertical, float splitFraction, LayoutNode first, LayoutNode second) {
                this.isVertical = isVertical;
                SplitFraction = Math.Clamp(splitFraction, 0.05f, 0.95f);
                First = first;
                Second = second;
            }

            public float SplitFraction { get; private set; }
            /// <summary>
            /// Gets a value indicating whether the split divides left/right panels.
            /// </summary>
            public bool IsVertical => isVertical;
            /// <summary>
            /// Gets the cached left edge of this split node.
            /// </summary>
            public float BoundsLeft => boundsLeft;
            /// <summary>
            /// Gets the cached top edge of this split node.
            /// </summary>
            public float BoundsTop => boundsTop;
            /// <summary>
            /// Gets the cached right edge of this split node.
            /// </summary>
            public float BoundsRight => boundsRight;
            /// <summary>
            /// Gets the cached bottom edge of this split node.
            /// </summary>
            public float BoundsBottom => boundsBottom;
            /// <summary>
            /// Gets the cached gap value used for layout.
            /// </summary>
            public int CachedGap => cachedGap;

            public LayoutNode First { get; set; }

            public LayoutNode Second { get; set; }

            public override void Layout(float left, float top, float right, float bottom, float z, int gap) {
                boundsLeft = left;
                boundsTop = top;
                boundsRight = right;
                boundsBottom = bottom;
                cachedGap = gap;

                if (isVertical) {
                    float availableWidth = right - left;
                    float splitWidth = MathF.Max(1f, availableWidth * SplitFraction);
                    if (availableWidth > 0f) {
                        GetMinSize(First, out int firstMinWidth, out _);
                        GetMinSize(Second, out int secondMinWidth, out _);

                        float minSplit = firstMinWidth + gap * 0.5f;
                        float maxSplit = availableWidth - secondMinWidth - gap * 0.5f;
                        if (minSplit > maxSplit) {
                            float midpoint = (minSplit + maxSplit) * 0.5f;
                            minSplit = midpoint;
                            maxSplit = midpoint;
                        }

                        splitWidth = Math.Clamp(splitWidth, minSplit, maxSplit);
                        SplitFraction = Math.Clamp(splitWidth / availableWidth, 0.05f, 0.95f);
                    }
                    float firstRight = left + splitWidth - gap * 0.5f;
                    float secondLeft = left + splitWidth + gap * 0.5f;

                    First.Layout(left, top, firstRight, bottom, z, gap);
                    Second.Layout(secondLeft, top, right, bottom, z, gap);
                } else {
                    float availableHeight = bottom - top;
                    float splitHeight = MathF.Max(1f, availableHeight * SplitFraction);
                    if (availableHeight > 0f) {
                        GetMinSize(First, out _, out int firstMinHeight);
                        GetMinSize(Second, out _, out int secondMinHeight);

                        float minSplit = firstMinHeight + gap * 0.5f;
                        float maxSplit = availableHeight - secondMinHeight - gap * 0.5f;
                        if (minSplit > maxSplit) {
                            float midpoint = (minSplit + maxSplit) * 0.5f;
                            minSplit = midpoint;
                            maxSplit = midpoint;
                        }

                        splitHeight = Math.Clamp(splitHeight, minSplit, maxSplit);
                        SplitFraction = Math.Clamp(splitHeight / availableHeight, 0.05f, 0.95f);
                    }
                    float firstBottom = top + splitHeight - gap * 0.5f;
                    float secondTop = top + splitHeight + gap * 0.5f;

                    First.Layout(left, top, right, firstBottom, z, gap);
                    Second.Layout(left, secondTop, right, bottom, z, gap);
                }
            }

            /// <summary>
            /// Updates the split fraction while clamping to safe bounds.
            /// </summary>
            /// <param name="fraction">New split fraction.</param>
            public void SetSplitFraction(float fraction) {
                float minBound = 0.05f;
                float maxBound = 0.95f;

                float available = isVertical ? boundsRight - boundsLeft : boundsBottom - boundsTop;
                float usable = MathF.Max(1f, available - cachedGap);

                if (usable > 0f) {
                    GetMinSize(First, out int firstMinWidth, out int firstMinHeight);
                    GetMinSize(Second, out int secondMinWidth, out int secondMinHeight);

                    float firstMinAxis = isVertical ? firstMinWidth : firstMinHeight;
                    float secondMinAxis = isVertical ? secondMinWidth : secondMinHeight;

                    float rawMin = firstMinAxis / usable;
                    float rawMax = 1f - (secondMinAxis / usable);

                    if (rawMin > rawMax) {
                        float midpoint = (rawMin + rawMax) * 0.5f;
                        rawMin = midpoint;
                        rawMax = midpoint;
                    }

                    minBound = Math.Clamp(rawMin, 0.05f, 0.95f);
                    maxBound = Math.Clamp(rawMax, 0.05f, 0.95f);
                }

                SplitFraction = Math.Clamp(fraction, minBound, maxBound);
            }

            public override PanelNode? Hit(float x, float y) {
                PanelNode? firstHit = First.Hit(x, y);
                if (firstHit != null) {
                    return firstHit;
                }

                return Second.Hit(x, y);
            }

            public override LayoutNode? Remove(DockableEntity entity) {
                LayoutNode? newFirst = First.Remove(entity);
                LayoutNode? newSecond = Second.Remove(entity);

                if (newFirst == null && newSecond == null) {
                    return null;
                }

                if (newFirst == null) {
                    return newSecond;
                }

                if (newSecond == null) {
                    return newFirst;
                }

                First = newFirst;
                Second = newSecond;
                return this;
            }

            public override PanelNode? Find(DockableEntity entity) {
                PanelNode? first = First.Find(entity);
                if (first != null) {
                    return first;
                }

                return Second.Find(entity);
            }

            public override PanelNode? FirstLeaf() {
                return First.FirstLeaf() ?? Second.FirstLeaf();
            }
        }
    }
}
