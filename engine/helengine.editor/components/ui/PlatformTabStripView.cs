namespace helengine.editor {
    /// <summary>
    /// Renders a reusable overflow-aware platform tab strip for editor surfaces.
    /// </summary>
    public sealed class PlatformTabStripView : IFocusGroup {
        /// <summary>
        /// Gap between one overflow arrow and the clipped tab viewport.
        /// </summary>
        const int ArrowViewportSpacing = 4;

        /// <summary>
        /// Font used to render tab and arrow labels.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Layer mask applied to the strip root and generated child entities.
        /// </summary>
        readonly ushort LayerMaskValue;
        /// <summary>
        /// Width reserved for each platform tab.
        /// </summary>
        readonly int TabWidthValue;
        /// <summary>
        /// Height reserved for each platform tab and overflow arrow.
        /// </summary>
        readonly int TabHeightValue;
        /// <summary>
        /// Horizontal spacing between adjacent tabs.
        /// </summary>
        readonly int TabSpacingValue;
        /// <summary>
        /// Width reserved for each overflow arrow button.
        /// </summary>
        readonly int ArrowButtonWidthValue;
        /// <summary>
        /// Root entity that owns the entire tab strip hierarchy.
        /// </summary>
        readonly EditorEntity RootValue;
        /// <summary>
        /// Viewport entity that clips the scrolling tab content.
        /// </summary>
        readonly EditorEntity ViewportRoot;
        /// <summary>
        /// Clip rectangle applied to the scrolling tab viewport.
        /// </summary>
        readonly ClipRectComponent ViewportClipRect;
        /// <summary>
        /// Content root translated horizontally as the strip scrolls.
        /// </summary>
        readonly EditorEntity TabsContentRoot;
        /// <summary>
        /// Host entity for the left overflow arrow button.
        /// </summary>
        readonly EditorEntity LeftArrowHost;
        /// <summary>
        /// Host entity for the right overflow arrow button.
        /// </summary>
        readonly EditorEntity RightArrowHost;
        /// <summary>
        /// Button used to scroll the strip left.
        /// </summary>
        readonly ButtonComponent LeftArrowButton;
        /// <summary>
        /// Button used to scroll the strip right.
        /// </summary>
        readonly ButtonComponent RightArrowButton;
        /// <summary>
        /// Platform identifiers currently represented by the strip.
        /// </summary>
        readonly List<string> PlatformIds;
        /// <summary>
        /// Maps platform identifiers to their tab indices.
        /// </summary>
        readonly Dictionary<string, int> PlatformIndexById;
        /// <summary>
        /// Host entities for generated tab buttons.
        /// </summary>
        readonly List<EditorEntity> TabHosts;
        /// <summary>
        /// Generated tab buttons.
        /// </summary>
        readonly List<TabComponent> Tabs;
        /// <summary>
        /// Keyboard focus targets attached to generated tabs.
        /// </summary>
        readonly List<EditorFocusTarget> TabFocusTargets;
        /// <summary>
        /// Tracks whether custom render orders were supplied for the strip buttons.
        /// </summary>
        bool HasRenderOrderOverrides;
        /// <summary>
        /// Render order applied to tab and arrow backgrounds when overrides are enabled.
        /// </summary>
        byte BackgroundRenderOrder;
        /// <summary>
        /// Render order applied to tab and arrow labels when overrides are enabled.
        /// </summary>
        byte TextRenderOrder;

        /// <summary>
        /// Callback invoked when the selected platform changes.
        /// </summary>
        Action<string> SelectionChanged;
        /// <summary>
        /// Currently selected platform identifier.
        /// </summary>
        string SelectedPlatformIdValue;
        /// <summary>
        /// Current horizontal scroll offset in pixels.
        /// </summary>
        int HorizontalScrollOffsetPixels;
        /// <summary>
        /// Width currently assigned to the whole strip.
        /// </summary>
        int LayoutWidthPixels;
        /// <summary>
        /// Width currently assigned to the clipped tab viewport.
        /// </summary>
        int ViewportWidthPixels;
        /// <summary>
        /// Width required to display all tabs without clipping.
        /// </summary>
        int TabsContentWidthPixels;
        /// <summary>
        /// Tracks whether the strip currently overflows horizontally.
        /// </summary>
        bool HasOverflowValue;

        /// <summary>
        /// Initializes a new platform tab strip view.
        /// </summary>
        /// <param name="font">Font used to render tab and arrow labels.</param>
        /// <param name="layerMask">Layer mask applied to the strip hierarchy.</param>
        /// <param name="tabWidth">Fixed width used by each tab.</param>
        /// <param name="tabHeight">Fixed height used by each tab.</param>
        /// <param name="tabSpacing">Horizontal spacing between adjacent tabs.</param>
        /// <param name="arrowButtonWidth">Width used by each overflow arrow button.</param>
        public PlatformTabStripView(
            FontAsset font,
            ushort layerMask,
            int tabWidth,
            int tabHeight,
            int tabSpacing,
            int arrowButtonWidth) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (tabWidth < 1) {
                throw new ArgumentOutOfRangeException(nameof(tabWidth), "Tab width must be positive.");
            } else if (tabHeight < 1) {
                throw new ArgumentOutOfRangeException(nameof(tabHeight), "Tab height must be positive.");
            } else if (tabSpacing < 0) {
                throw new ArgumentOutOfRangeException(nameof(tabSpacing), "Tab spacing must not be negative.");
            } else if (arrowButtonWidth < 1) {
                throw new ArgumentOutOfRangeException(nameof(arrowButtonWidth), "Arrow button width must be positive.");
            }

            Font = font;
            LayerMaskValue = layerMask;
            TabWidthValue = tabWidth;
            TabHeightValue = tabHeight;
            TabSpacingValue = tabSpacing;
            ArrowButtonWidthValue = arrowButtonWidth;
            PlatformIds = new List<string>(8);
            PlatformIndexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            TabHosts = new List<EditorEntity>(8);
            Tabs = new List<TabComponent>(8);
            TabFocusTargets = new List<EditorFocusTarget>(8);
            SelectedPlatformIdValue = string.Empty;

            RootValue = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = true
            };

            LeftArrowHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = false
            };
            RootValue.AddChild(LeftArrowHost);

            LeftArrowButton = CreateArrowButton("<", HandleLeftArrowClicked);
            LeftArrowHost.AddComponent(LeftArrowButton);

            ViewportRoot = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = true
            };
            RootValue.AddChild(ViewportRoot);

            ViewportClipRect = new ClipRectComponent {
                Size = new int2(0, tabHeight)
            };
            ViewportRoot.AddComponent(ViewportClipRect);

            TabsContentRoot = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = true
            };
            ViewportRoot.AddChild(TabsContentRoot);

            RightArrowHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = false
            };
            RootValue.AddChild(RightArrowHost);

            RightArrowButton = CreateArrowButton(">", HandleRightArrowClicked);
            RightArrowHost.AddComponent(RightArrowButton);

            EditorKeyboardFocusService.RegisterGroup(this);
        }

        /// <summary>
        /// Gets the root entity that owns the strip visuals.
        /// </summary>
        public EditorEntity Root => RootValue;

        /// <summary>
        /// Gets the number of currently rendered platform tabs.
        /// </summary>
        public int TabCount => PlatformIds.Count;

        /// <summary>
        /// Gets the currently selected platform identifier.
        /// </summary>
        public string SelectedPlatformId => SelectedPlatformIdValue;

        /// <summary>
        /// Gets the current horizontal scroll offset in pixels.
        /// </summary>
        public int HorizontalScrollOffset => HorizontalScrollOffsetPixels;

        /// <summary>
        /// Gets a value indicating whether the strip can currently scroll left.
        /// </summary>
        public bool CanScrollLeft => HorizontalScrollOffsetPixels > 0;

        /// <summary>
        /// Gets a value indicating whether the strip can currently scroll right.
        /// </summary>
        public bool CanScrollRight => HorizontalScrollOffsetPixels < GetMaximumScrollOffsetPixels();

        /// <summary>
        /// Gets a value indicating whether the strip currently overflows horizontally.
        /// </summary>
        public bool HasOverflow => HasOverflowValue;

        /// <summary>
        /// Gets the root focus group that owns this strip.
        /// </summary>
        public IFocusGroup RootGroup => this;

        /// <summary>
        /// Gets the traversal order used when this strip participates in focus ordering.
        /// </summary>
        public int GroupOrder => 0;

        /// <summary>
        /// Gets whether the strip can currently receive keyboard focus.
        /// </summary>
        public bool CanReceiveFocus => RootValue.Enabled && RootValue.IsHierarchyEnabled && PlatformIds.Count > 0;

        /// <summary>
        /// Rebuilds the strip using the supplied platforms and selected platform id.
        /// </summary>
        /// <param name="platformIds">Platform identifiers to display.</param>
        /// <param name="selectedPlatformId">Initially selected platform identifier.</param>
        /// <param name="selectionChanged">Callback invoked when the selected platform changes.</param>
        public void SetPlatforms(IReadOnlyList<string> platformIds, string selectedPlatformId, Action<string> selectionChanged) {
            if (platformIds == null) {
                throw new ArgumentNullException(nameof(platformIds));
            } else if (platformIds.Count == 0) {
                throw new ArgumentException("At least one platform id must be provided.", nameof(platformIds));
            } else if (string.IsNullOrWhiteSpace(selectedPlatformId)) {
                throw new ArgumentException("Selected platform id must be provided.", nameof(selectedPlatformId));
            } else if (selectionChanged == null) {
                throw new ArgumentNullException(nameof(selectionChanged));
            }

            ClearTabs();
            SelectionChanged = selectionChanged;

            for (int i = 0; i < platformIds.Count; i++) {
                string platformId = platformIds[i];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new ArgumentException("Platform ids must be provided.", nameof(platformIds));
                }
                if (PlatformIndexById.ContainsKey(platformId)) {
                    throw new InvalidOperationException("Duplicate platform ids are not supported in the platform tab strip.");
                }

                PlatformIds.Add(platformId);
                PlatformIndexById[platformId] = i;
                AddTab(platformId, i);
            }

            if (!PlatformIndexById.ContainsKey(selectedPlatformId)) {
                throw new InvalidOperationException("The requested selected platform is not available in the platform tab strip.");
            }

            SelectedPlatformIdValue = selectedPlatformId;
            HorizontalScrollOffsetPixels = 0;
            RootValue.Enabled = true;
            LayoutTabs();
            RevealPlatform(selectedPlatformId);
            UpdateSelectedVisualState();
            UpdateOverflowState();
        }

        /// <summary>
        /// Updates the selected platform and reveals it if necessary.
        /// </summary>
        /// <param name="platformId">Platform identifier that should become selected.</param>
        public void SetSelectedPlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (!PlatformIndexById.ContainsKey(platformId)) {
                throw new InvalidOperationException("The requested platform tab is not available.");
            }

            SelectedPlatformIdValue = platformId;
            RevealPlatform(platformId);
            UpdateSelectedVisualState();
            UpdateOverflowState();
        }

        /// <summary>
        /// Updates the strip layout using the supplied top-left position and available width.
        /// </summary>
        /// <param name="left">Left position in pixels.</param>
        /// <param name="top">Top position in pixels.</param>
        /// <param name="width">Available width in pixels.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Layout width must be positive.");
            }

            LayoutWidthPixels = width;
            RootValue.Position = new float3(left, top, 0.1f);
            LayoutTabs();
            RevealPlatform(SelectedPlatformIdValue);
            UpdateSelectedVisualState();
            UpdateOverflowState();
        }

        /// <summary>
        /// Overrides the render order used by the generated tab and arrow visuals.
        /// </summary>
        /// <param name="backgroundOrder">Render order used for button backgrounds.</param>
        /// <param name="textOrder">Render order used for button labels.</param>
        public void SetRenderOrders(byte backgroundOrder, byte textOrder) {
            HasRenderOrderOverrides = true;
            BackgroundRenderOrder = backgroundOrder;
            TextRenderOrder = textOrder;
            LeftArrowButton.SetRenderOrders(backgroundOrder, textOrder);
            RightArrowButton.SetRenderOrders(backgroundOrder, textOrder);

            for (int i = 0; i < Tabs.Count; i++) {
                Tabs[i].SetRenderOrders(backgroundOrder, textOrder);
            }
        }

        /// <summary>
        /// Returns whether the supplied platform tab is fully visible inside the clipped viewport.
        /// </summary>
        /// <param name="platformId">Platform identifier whose visibility should be checked.</param>
        /// <returns>True when the tab is fully visible.</returns>
        public bool IsPlatformFullyVisible(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (!PlatformIndexById.TryGetValue(platformId, out int tabIndex)) {
                throw new InvalidOperationException("The requested platform tab is not available.");
            }

            int tabLeft = GetTabLeftPixels(tabIndex);
            int tabRight = tabLeft + TabWidthValue;
            int visibleLeft = HorizontalScrollOffsetPixels;
            int visibleRight = HorizontalScrollOffsetPixels + ViewportWidthPixels;
            return tabLeft >= visibleLeft && tabRight <= visibleRight;
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the strip bounds.
        /// </summary>
        /// <param name="point">Screen-space point to evaluate.</param>
        /// <returns>True when the point lies inside the strip.</returns>
        public bool ContainsScreenPoint(int2 point) {
            float3 position = RootValue.Position;
            return point.X >= position.X
                && point.X < position.X + LayoutWidthPixels
                && point.Y >= position.Y
                && point.Y < position.Y + TabHeightValue;
        }

        /// <summary>
        /// Applies the active-state visual for this focus group.
        /// </summary>
        /// <param name="isActive">True when the group should appear active.</param>
        public void SetGroupActive(bool isActive) {
        }

        /// <summary>
        /// Releases registered focus state and generated tab resources.
        /// </summary>
        public void Dispose() {
            ClearTabs();
            EditorKeyboardFocusService.UnregisterGroup(this);
        }

        /// <summary>
        /// Creates one overflow arrow button using the shared platform-tab palette.
        /// </summary>
        /// <param name="label">Arrow label shown by the button.</param>
        /// <param name="onClickAction">Callback invoked when the arrow is clicked.</param>
        /// <returns>Configured arrow button.</returns>
        ButtonComponent CreateArrowButton(string label, Action onClickAction) {
            ButtonComponent button = new ButtonComponent(label, new int2(ArrowButtonWidthValue, TabHeightValue), Font, onClickAction);
            button.UseSquareCorners();
            button.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            button.SetVisualPalette(
                ThemeManager.Colors.SurfacePrimary,
                ThemeManager.Colors.AccentSecondary,
                ThemeManager.Colors.AccentTertiary,
                ThemeManager.Colors.SurfaceInput,
                ThemeManager.Colors.AccentTertiary,
                ThemeManager.Colors.AccentTertiary);
            button.SetHoverCursor(PointerCursorKind.Hand);
            return button;
        }

        /// <summary>
        /// Creates one tab host, visual tab button, and keyboard focus target for the supplied platform id.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the tab.</param>
        /// <param name="tabIndex">Tab index used for ordering.</param>
        void AddTab(string platformId, int tabIndex) {
            EditorEntity tabHost = new EditorEntity {
                LayerMask = LayerMaskValue,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = true
            };
            TabsContentRoot.AddChild(tabHost);

            TabComponent tab = new TabComponent(platformId, new int2(TabWidthValue, TabHeightValue), Font, () => HandleTabClicked(platformId));
            if (HasRenderOrderOverrides) {
                tab.SetRenderOrders(BackgroundRenderOrder, TextRenderOrder);
            }
            tab.SetHoverCursor(PointerCursorKind.Hand);
            tabHost.AddComponent(tab);

            EditorFocusTarget focusTarget = new EditorFocusTarget(
                this,
                tabIndex,
                tabIndex == 0,
                () => RootValue.Enabled && tabHost.Enabled,
                point => ContainsTabPoint(tabHost, point),
                isFocused => HandleTabFocusChanged(platformId, isFocused),
                key => CanActivatePlatformKey(platformId, key),
                key => ActivatePlatformKey(platformId, key));

            EditorKeyboardFocusService.RegisterTarget(focusTarget);

            TabHosts.Add(tabHost);
            Tabs.Add(tab);
            TabFocusTargets.Add(focusTarget);
        }

        /// <summary>
        /// Unregisters and disposes all generated tab state.
        /// </summary>
        void ClearTabs() {
            for (int i = 0; i < TabFocusTargets.Count; i++) {
                EditorKeyboardFocusService.UnregisterTarget(TabFocusTargets[i]);
            }

            for (int i = TabHosts.Count - 1; i >= 0; i--) {
                TabHosts[i].Dispose();
            }

            PlatformIds.Clear();
            PlatformIndexById.Clear();
            TabHosts.Clear();
            Tabs.Clear();
            TabFocusTargets.Clear();
            SelectedPlatformIdValue = string.Empty;
            HorizontalScrollOffsetPixels = 0;
            TabsContentWidthPixels = 0;
            ViewportWidthPixels = 0;
            HasOverflowValue = false;
        }

        /// <summary>
        /// Handles pointer-driven tab selection.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the clicked tab.</param>
        void HandleTabClicked(string platformId) {
            EditorKeyboardFocusService.SetFocusedTarget(GetFocusTarget(platformId));
        }

        /// <summary>
        /// Handles focus changes for one tab target.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the target.</param>
        /// <param name="isFocused">True when the target became focused.</param>
        void HandleTabFocusChanged(string platformId, bool isFocused) {
            if (!isFocused) {
                return;
            }

            bool selectionChanged = !string.Equals(SelectedPlatformIdValue, platformId, StringComparison.OrdinalIgnoreCase);
            SelectedPlatformIdValue = platformId;
            RevealPlatform(platformId);
            UpdateSelectedVisualState();
            UpdateOverflowState();

            if (selectionChanged && SelectionChanged != null) {
                SelectionChanged(platformId);
            }
        }

        /// <summary>
        /// Returns whether the supplied key should activate or navigate from the supplied tab.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the target.</param>
        /// <param name="key">Key being routed to the target.</param>
        /// <returns>True when the strip should react to the key.</returns>
        bool CanActivatePlatformKey(string platformId, Keys key) {
            if (!PlatformIndexById.ContainsKey(platformId)) {
                return false;
            }

            return key == Keys.Enter
                || key == Keys.Space
                || key == Keys.Left
                || key == Keys.Right;
        }

        /// <summary>
        /// Handles one keyboard activation or navigation key for the supplied platform.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the focused target.</param>
        /// <param name="key">Key routed into the tab strip.</param>
        void ActivatePlatformKey(string platformId, Keys key) {
            if (key == Keys.Left) {
                MoveSelectionBy(-1);
                return;
            }

            if (key == Keys.Right) {
                MoveSelectionBy(1);
                return;
            }

            if (SelectionChanged != null) {
                SelectionChanged(platformId);
            }
        }

        /// <summary>
        /// Moves keyboard focus and selection by one tab in the supplied direction.
        /// </summary>
        /// <param name="delta">Signed direction to move.</param>
        void MoveSelectionBy(int delta) {
            if (PlatformIds.Count == 0) {
                return;
            }

            int selectedIndex = PlatformIndexById[SelectedPlatformIdValue];
            int nextIndex = selectedIndex + delta;
            if (nextIndex < 0) {
                nextIndex = 0;
            } else if (nextIndex >= PlatformIds.Count) {
                nextIndex = PlatformIds.Count - 1;
            }

            if (nextIndex == selectedIndex) {
                return;
            }

            EditorKeyboardFocusService.SetFocusedTarget(TabFocusTargets[nextIndex]);
        }

        /// <summary>
        /// Handles left-arrow button clicks.
        /// </summary>
        void HandleLeftArrowClicked() {
            HorizontalScrollOffsetPixels = Math.Max(0, HorizontalScrollOffsetPixels - GetScrollStepPixels());
            LayoutTabs();
            UpdateOverflowState();
        }

        /// <summary>
        /// Handles right-arrow button clicks.
        /// </summary>
        void HandleRightArrowClicked() {
            HorizontalScrollOffsetPixels = Math.Min(GetMaximumScrollOffsetPixels(), HorizontalScrollOffsetPixels + GetScrollStepPixels());
            LayoutTabs();
            UpdateOverflowState();
        }

        /// <summary>
        /// Recomputes arrow visibility, viewport bounds, and scrolling content placement.
        /// </summary>
        void LayoutTabs() {
            int availableWidth = LayoutWidthPixels > 0 ? LayoutWidthPixels : Math.Max(1, GetTabsContentWidthPixels());
            TabsContentWidthPixels = GetTabsContentWidthPixels();
            HasOverflowValue = TabsContentWidthPixels > availableWidth;

            int viewportLeft = 0;
            int viewportWidth = availableWidth;
            if (HasOverflowValue) {
                viewportLeft = ArrowButtonWidthValue + ArrowViewportSpacing;
                viewportWidth = availableWidth - ((ArrowButtonWidthValue + ArrowViewportSpacing) * 2);
            }

            ViewportWidthPixels = Math.Max(1, viewportWidth);
            HorizontalScrollOffsetPixels = Math.Clamp(HorizontalScrollOffsetPixels, 0, GetMaximumScrollOffsetPixels());

            LeftArrowHost.Enabled = HasOverflowValue;
            RightArrowHost.Enabled = HasOverflowValue;
            LeftArrowHost.Position = new float3(0f, 0f, 0.1f);
            RightArrowHost.Position = new float3(availableWidth - ArrowButtonWidthValue, 0f, 0.1f);

            ViewportRoot.Position = new float3(viewportLeft, 0f, 0.1f);
            ViewportClipRect.Size = new int2(ViewportWidthPixels, TabHeightValue);
            TabsContentRoot.Position = new float3(-HorizontalScrollOffsetPixels, 0f, 0.1f);

            for (int i = 0; i < TabHosts.Count; i++) {
                TabHosts[i].Position = new float3(GetTabLeftPixels(i), 0f, 0.1f);
            }
        }

        /// <summary>
        /// Reveals the supplied platform tab inside the current viewport if it is clipped.
        /// </summary>
        /// <param name="platformId">Platform identifier whose tab should be revealed.</param>
        void RevealPlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return;
            }
            if (!PlatformIndexById.TryGetValue(platformId, out int tabIndex)) {
                return;
            }

            if (ViewportWidthPixels <= 0) {
                return;
            }

            int tabLeft = GetTabLeftPixels(tabIndex);
            int tabRight = tabLeft + TabWidthValue;
            int visibleLeft = HorizontalScrollOffsetPixels;
            int visibleRight = HorizontalScrollOffsetPixels + ViewportWidthPixels;

            if (tabLeft < visibleLeft) {
                HorizontalScrollOffsetPixels = tabLeft;
            } else if (tabRight > visibleRight) {
                HorizontalScrollOffsetPixels = tabRight - ViewportWidthPixels;
            }

            HorizontalScrollOffsetPixels = Math.Clamp(HorizontalScrollOffsetPixels, 0, GetMaximumScrollOffsetPixels());
            TabsContentRoot.Position = new float3(-HorizontalScrollOffsetPixels, 0f, 0.1f);
        }

        /// <summary>
        /// Updates the selected-state visuals for every generated tab.
        /// </summary>
        void UpdateSelectedVisualState() {
            for (int i = 0; i < Tabs.Count; i++) {
                bool isSelected = string.Equals(PlatformIds[i], SelectedPlatformIdValue, StringComparison.OrdinalIgnoreCase);
                Tabs[i].SetSelected(isSelected);
            }
        }

        /// <summary>
        /// Updates arrow-enable state derived from the current scroll offset and overflow width.
        /// </summary>
        void UpdateOverflowState() {
            int availableWidth = LayoutWidthPixels > 0 ? LayoutWidthPixels : Math.Max(1, TabsContentWidthPixels);
            HasOverflowValue = TabsContentWidthPixels > availableWidth;
        }

        /// <summary>
        /// Returns the keyboard focus target for the supplied platform id.
        /// </summary>
        /// <param name="platformId">Platform identifier whose focus target should be returned.</param>
        /// <returns>Registered focus target for the platform.</returns>
        EditorFocusTarget GetFocusTarget(string platformId) {
            int tabIndex = PlatformIndexById[platformId];
            return TabFocusTargets[tabIndex];
        }

        /// <summary>
        /// Returns whether the supplied point lies inside one generated tab host.
        /// </summary>
        /// <param name="tabHost">Tab host entity whose bounds should be tested.</param>
        /// <param name="point">Screen-space point to evaluate.</param>
        /// <returns>True when the point lies inside the tab's visible bounds.</returns>
        bool ContainsTabPoint(EditorEntity tabHost, int2 point) {
            float3 tabPosition = tabHost.Position;
            float4 clipRect = ViewportClipRect.GetClipRect();

            float tabLeft = tabPosition.X;
            float tabTop = tabPosition.Y;
            float tabRight = tabLeft + TabWidthValue;
            float tabBottom = tabTop + TabHeightValue;
            float clipLeft = clipRect.X;
            float clipTop = clipRect.Y;
            float clipRight = clipLeft + clipRect.Z;
            float clipBottom = clipTop + clipRect.W;

            if (point.X < clipLeft || point.X >= clipRight || point.Y < clipTop || point.Y >= clipBottom) {
                return false;
            }

            return point.X >= tabLeft
                && point.X < tabRight
                && point.Y >= tabTop
                && point.Y < tabBottom;
        }

        /// <summary>
        /// Returns the left offset of one tab inside the scrolling content root.
        /// </summary>
        /// <param name="tabIndex">Zero-based tab index.</param>
        /// <returns>Left offset in pixels.</returns>
        int GetTabLeftPixels(int tabIndex) {
            return tabIndex * (TabWidthValue + TabSpacingValue);
        }

        /// <summary>
        /// Returns the total width required to display all generated tabs without clipping.
        /// </summary>
        /// <returns>Total tab-content width in pixels.</returns>
        int GetTabsContentWidthPixels() {
            if (PlatformIds.Count == 0) {
                return 0;
            }

            return (PlatformIds.Count * TabWidthValue) + ((PlatformIds.Count - 1) * TabSpacingValue);
        }

        /// <summary>
        /// Returns the maximum horizontal scroll offset supported by the current viewport.
        /// </summary>
        /// <returns>Maximum scroll offset in pixels.</returns>
        int GetMaximumScrollOffsetPixels() {
            return Math.Max(0, TabsContentWidthPixels - ViewportWidthPixels);
        }

        /// <summary>
        /// Returns the fixed horizontal scroll step used by the overflow arrows.
        /// </summary>
        /// <returns>Scroll step in pixels.</returns>
        int GetScrollStepPixels() {
            return TabWidthValue + TabSpacingValue;
        }
    }
}
