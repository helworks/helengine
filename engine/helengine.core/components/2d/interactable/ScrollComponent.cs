namespace helengine {
    /// <summary>
    /// Tracks wheel-driven list scrolling for a rectangular viewport, acts as its own clip region, derives the visible range when needed, and can translate a bound content root automatically.
    /// </summary>
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
    [RunInEditor]
#endif
    public class ScrollComponent : UpdateComponent, IClipRegion2D {
        /// <summary>
        /// Standard mouse-wheel delta used to represent one notch on Windows-compatible devices.
        /// </summary>
        const int StandardWheelNotch = 120;

        /// <summary>Default thickness of the scrollbar rendered inside the viewport.</summary>
        const int DefaultScrollBarThickness = 8;

        /// <summary>Controls whether this viewport owns a scrollbar; enabled by default.</summary>
        bool ShowScrollBarValue = true;

        /// <summary>Thickness in pixels of the scrollbar along its non-scrolling axis.</summary>
        int ScrollBarThicknessValue = DefaultScrollBarThickness;

        /// <summary>Runtime entity owning the optional scrollbar and its visuals.</summary>
        Entity ScrollBarHost;

        /// <summary>Runtime scrollbar created only while scrollbar support is enabled.</summary>
        ScrollBarComponent ScrollBarValue;

        /// <summary>
        /// Size of the scroll viewport in screen-space pixels.
        /// </summary>
        int2 SizeValue;

        /// <summary>
        /// Number of items that can appear in the backing list.
        /// </summary>
        int ItemCountValue;

        /// <summary>
        /// Pixel height or extent consumed by one item in the scrolling content.
        /// </summary>
        int ItemExtentValue = 1;

        /// <summary>
        /// Axis along which this scroll component measures and translates content.
        /// </summary>
        ScrollOrientation OrientationValue;

        /// <summary>
        /// Number of items visible inside the current viewport, or zero when the component should derive it automatically.
        /// </summary>
        int VisibleItemCountValue;

        /// <summary>
        /// Number of items to move for each wheel notch.
        /// </summary>
        int ScrollStepCountValue = 1;

        /// <summary>
        /// Wheel delta that maps to one scroll notch.
        /// </summary>
        int WheelNotchSizeValue = StandardWheelNotch;

        /// <summary>
        /// Tracks whether wheel scrolling should only occur while the pointer is inside the viewport.
        /// </summary>
        bool RequiresPointerInsideValue = true;

        /// <summary>
        /// Optional content root that should be translated when the scroll offset changes.
        /// </summary>
        Entity ContentRootValue;

        /// <summary>
        /// Optional entity that supplies the fixed viewport origin used for hit testing and clip rectangle exposure.
        /// </summary>
        Entity ClipOriginEntityValue;

        /// <summary>
        /// Raised when the scroll offset changes because of wheel input or an explicit scroll request.
        /// </summary>
        public event Action<ScrollComponent, int> ScrollOffsetChanged;

        /// <summary>Gets or sets whether a scrollbar is created for this viewport. Defaults to true; disabling it disposes the scrollbar without disabling scrolling.</summary>
        public bool ShowScrollBar {
            get { return ShowScrollBarValue; }
            set {
                if (ShowScrollBarValue == value) {
                    return;
                }

                ShowScrollBarValue = value;
                if (value && Parent != null && ComponentExecutionPolicy.ShouldRunComponentLifecycle(this, Parent)) {
                    CreateScrollBar();
                } else if (!value) {
                    RemoveScrollBar();
                }
            }
        }

        /// <summary>Gets or sets the positive scrollbar thickness in pixels; defaults to eight.</summary>
        public int ScrollBarThickness {
            get { return ScrollBarThicknessValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scrollbar thickness must be positive.");
                }

                ScrollBarThicknessValue = value;
                RefreshScrollBar();
            }
        }

        /// <summary>Gets the runtime scrollbar, or null when scrollbar creation is disabled or this component is detached.</summary>
        [ScenePersistenceIgnore]
        public ScrollBarComponent ScrollBar {
            get { return ScrollBarValue; }
        }

        /// <summary>
        /// Gets or sets the viewport size used for pointer hit testing.
        /// </summary>
        public int2 Size {
            get { return ResolveViewportSize(); }
            set {
                if (value.X < 0 || value.Y < 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scroll viewport size must not be negative.");
                }

                SizeValue = value;
                ClampScrollOffset();
            }
        }

        /// <summary>
        /// Gets or sets the total number of list items that can be scrolled through.
        /// </summary>
        public int ItemCount {
            get { return ItemCountValue; }
            set {
                if (value < 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Item count must be zero or greater.");
                }

                ItemCountValue = value;
                ClampScrollOffset();
            }
        }

        /// <summary>
        /// Gets or sets the number of complete items visible in the viewport.
        /// When set to zero, the component derives the value from the viewport height and item extent.
        /// </summary>
        public int VisibleItemCount {
            get { return GetVisibleItemCount(); }
            set {
                if (value < 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Visible item count must be zero or greater.");
                }

                VisibleItemCountValue = value;
                ClampScrollOffset();
            }
        }

        /// <summary>
        /// Gets whether the component should derive the visible item count from the viewport size and item extent.
        /// </summary>
        public bool UsesAutomaticVisibleItemCount {
            get { return VisibleItemCountValue < 1; }
        }

        /// <summary>
        /// Gets or sets the item extent used when the visible item count is derived automatically.
        /// </summary>
        public int ItemExtent {
            get { return ItemExtentValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Item extent must be at least one.");
                }

                ItemExtentValue = value;
                ClampScrollOffset();
                ApplyContentRootOffset();
            }
        }

        /// <summary>
        /// Gets the maximum scroll offset allowed by the current item and viewport counts.
        /// </summary>
        public int MaximumScrollOffset {
            get { return Math.Max(0, ItemCountValue - GetVisibleItemCount()); }
        }

        /// <summary>
        /// Gets the current scroll offset in item units.
        /// </summary>
        public int ScrollOffset { get; private set; }

        /// <summary>
        /// Gets or sets the content root that should move in response to the current scroll offset.
        /// This binding is runtime-only and is excluded from reflected scene persistence.
        /// </summary>
        [ScenePersistenceIgnore]
        public Entity ContentRoot {
            get { return ContentRootValue; }
            set {
                ContentRootValue = value;
                ApplyContentRootOffset();
            }
        }

        /// <summary>
        /// Gets or sets the entity whose world position should anchor the scroll viewport. When unset, the component uses the ancestor that owns its viewport clip rectangle, falling back to its parent.
        /// This binding is runtime-only and is excluded from reflected scene persistence.
        /// </summary>
        [ScenePersistenceIgnore]
        public Entity ClipOriginEntity {
            get { return ClipOriginEntityValue; }
            set {
                ClipOriginEntityValue = value;
                RefreshScrollBar();
            }
        }

        /// <summary>
        /// Gets or sets the number of items to move for each wheel notch.
        /// </summary>
        public int ScrollStepCount {
            get { return ScrollStepCountValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scroll step count must be at least one.");
                }

                ScrollStepCountValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the wheel delta that corresponds to one scroll notch.
        /// </summary>
        public int WheelNotchSize {
            get { return WheelNotchSizeValue; }
            set {
                if (value < 1) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Wheel notch size must be at least one.");
                }

                WheelNotchSizeValue = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the pointer must be inside the viewport before scrolling can occur.
        /// </summary>
        public bool RequiresPointerInside {
            get { return RequiresPointerInsideValue; }
            set { RequiresPointerInsideValue = value; }
        }

        /// <summary>
        /// Gets or sets whether the automatic visible count rounds a partial trailing item up instead of down.
        /// Enable only when the owning view clips overflow, so the partial item renders cut off instead of
        /// leaving empty space at the end of the scrolled range.
        /// </summary>
        public bool ShowsPartialTrailingItem { get; set; }

        /// <summary>
        /// Creates the optional scrollbar when the viewport is attached, including initially disabled entities.
        /// </summary>
        /// <param name="entity">Entity owning the scroll viewport.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            if (ShowScrollBarValue) {
                CreateScrollBar();
            }
        }

        /// <summary>Releases the owned scrollbar when the viewport is detached.</summary>
        /// <param name="entity">Entity losing the scroll viewport.</param>
        public override void ComponentRemoved(Entity entity) {
            RemoveScrollBar();
            base.ComponentRemoved(entity);
        }

        /// <summary>Advances wheel scrolling and updates the scrollbar layout.</summary>
        public override void Update() {
            TryApplyWheelInput();
            RefreshScrollBar();
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the current viewport bounds.
        /// </summary>
        /// <param name="x">Pointer X coordinate in screen coordinates.</param>
        /// <param name="y">Pointer Y coordinate in screen coordinates.</param>
        /// <returns>True when the point lies inside the scroll viewport.</returns>
        public bool ContainsScreenPoint(int x, int y) {
            if (Parent == null) {
                return false;
            }

            float4 viewportRect = GetClipRect();
            return x >= viewportRect.X &&
                   x < viewportRect.X + viewportRect.Z &&
                   y >= viewportRect.Y &&
                   y < viewportRect.Y + viewportRect.W;
        }

        /// <summary>
        /// Resets the scroll offset to the first visible item without raising a change event.
        /// </summary>
        public void ResetScrollOffset() {
            SetScrollOffset(0, false);
        }

        /// <summary>
        /// Clamps the current scroll offset to the active item range without raising a change event.
        /// </summary>
        public void ClampScrollOffset() {
            SetScrollOffset(ScrollOffset, false);
            RefreshScrollBar();
        }

        /// <summary>
        /// Applies one explicit scroll offset and notifies listeners when the value changes.
        /// </summary>
        /// <param name="scrollOffset">Desired scroll offset in item units.</param>
        /// <returns>True when the offset changed.</returns>
        public bool ScrollTo(int scrollOffset) {
            return SetScrollOffset(scrollOffset, true);
        }

        /// <summary>
        /// Attempts to apply wheel input from the current frame.
        /// </summary>
        /// <returns>True when the scroll offset changed.</returns>
        public bool TryApplyWheelInput() {
#if DESKTOP_PLATFORM
            if (Parent == null) {
                return false;
            }

            if (MaximumScrollOffset <= 0) {
                return false;
            }

            if (RequiresPointerInsideValue && !ContainsScreenPoint(OwnerCore.Input.GetMouseX(), OwnerCore.Input.GetMouseY())) {
                return false;
            }

            int wheelDelta = OwnerCore.Input.GetMouseScrollWheelDelta();
            if (wheelDelta == 0) {
                return false;
            }

            int scrollSteps = wheelDelta / WheelNotchSizeValue;
            if (scrollSteps == 0) {
                scrollSteps = wheelDelta > 0 ? 1 : -1;
            }

            scrollSteps *= ScrollStepCountValue;
            int nextOffset = ScrollOffset - scrollSteps;
            return SetScrollOffset(nextOffset, true);
#else
            return false;
#endif
        }

        /// <summary>
        /// Applies one scroll offset and optionally raises the change event.
        /// </summary>
        /// <param name="scrollOffset">Requested scroll offset.</param>
        /// <param name="raiseEvent">True to raise the offset changed event.</param>
        /// <returns>True when the offset changed.</returns>
        bool SetScrollOffset(int scrollOffset, bool raiseEvent) {
            int clampedOffset = ClampOffset(scrollOffset);
            if (clampedOffset == ScrollOffset) {
                return false;
            }

            ScrollOffset = clampedOffset;
            if (raiseEvent && ScrollOffsetChanged != null) {
                ScrollOffsetChanged(this, ScrollOffset);
            }

            ApplyContentRootOffset();
            RefreshScrollBar();
            return true;
        }

        /// <summary>
        /// Gets or sets the axis along which the viewport scrolls. Vertical is the default.
        /// </summary>
        public ScrollOrientation Orientation {
            get { return OrientationValue; }
            set {
                if (value != ScrollOrientation.Vertical && value != ScrollOrientation.Horizontal) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scroll orientation must be vertical or horizontal.");
                }

                if (OrientationValue == value) {
                    return;
                }

                OrientationValue = value;
                ClampScrollOffset();
                ApplyContentRootOffset();
            }
        }

        /// <summary>Creates the scrollbar subtree once during attachment or when scrollbar support is enabled.</summary>
        void CreateScrollBar() {
            ScrollBarHost = new Entity(OwnerCore);
            ScrollBarHost.LayerMask = Parent.LayerMask;
            ScrollBarHost.InitComponents();
            if (Parent.Children == null) {
                Parent.InitChildren();
            }

            Parent.AddChild(ScrollBarHost);
            ScrollBarValue = new ScrollBarComponent(new int2(1, 1));
            ScrollBarValue.Target = this;
            ScrollBarHost.AddComponent(ScrollBarValue);
            RefreshScrollBar();
        }

        /// <summary>Disposes the complete scrollbar subtree so disabled scrollbars retain no entities, visuals, or input regions.</summary>
        void RemoveScrollBar() {
            if (ScrollBarHost == null) {
                return;
            }

            NativeOwnership.DisposeAndDelete(ScrollBarHost);
            ScrollBarHost = null;
            ScrollBarValue = null;
        }

        /// <summary>Anchors the scrollbar to the fixed viewport edge and refreshes its range after layout or scroll changes.</summary>
        void RefreshScrollBar() {
            if (ScrollBarValue == null) {
                return;
            }

            float4 viewport = GetClipRect();
            bool horizontal = OrientationValue == ScrollOrientation.Horizontal;
            int thickness = Math.Min(ScrollBarThicknessValue, (int)(horizontal ? viewport.W : viewport.Z));
            int trackLength = (int)(horizontal ? viewport.Z : viewport.W);
            ScrollBarHost.Enabled = thickness > 0 && trackLength > 0;
            if (!ScrollBarHost.Enabled) {
                return;
            }

            ScrollBarHost.LayerMask = Parent.LayerMask;
            float3 origin = Parent.Position;
            ScrollBarHost.LocalPosition = new float3(
                viewport.X - origin.X + (horizontal ? 0f : viewport.Z - thickness),
                viewport.Y - origin.Y + (horizontal ? viewport.W - thickness : 0f),
                1f);
            ScrollBarValue.Size = horizontal ? new int2(trackLength, thickness) : new int2(thickness, trackLength);
        }

        /// <summary>
        /// Applies the current scroll offset to the bound content root when one exists.
        /// </summary>
        void ApplyContentRootOffset() {
            if (ContentRootValue == null) {
                return;
            }

            float3 position = ContentRootValue.LocalPosition;
            if (OrientationValue == ScrollOrientation.Horizontal) {
                ContentRootValue.LocalPosition = new float3(-(ScrollOffset * ItemExtentValue), position.Y, position.Z);
            } else {
                ContentRootValue.LocalPosition = new float3(position.X, -(ScrollOffset * ItemExtentValue), position.Z);
            }
        }

        /// <summary>
        /// Gets the clip rectangle used by descendants and pointer hit testing.
        /// </summary>
        /// <returns>Viewport rectangle expressed as X, Y, Width, Height.</returns>
        public float4 GetClipRect() {
            if (Parent == null) {
                throw new InvalidOperationException("Scroll components require an attached parent entity.");
            }

            Entity clipOriginEntity = ClipOriginEntityValue ?? Parent;
            float3 origin = clipOriginEntity.Position;
            int2 viewportSize = ResolveViewportSize();
            return new float4(origin.X, origin.Y, viewportSize.X, viewportSize.Y);
        }

        /// <summary>
        /// Clamps one requested scroll offset to the current available range.
        /// </summary>
        /// <param name="scrollOffset">Requested offset.</param>
        /// <returns>Clamped offset value.</returns>
        int ClampOffset(int scrollOffset) {
            int maxOffset = MaximumScrollOffset;
            if (scrollOffset < 0) {
                return 0;
            }

            if (scrollOffset > maxOffset) {
                return maxOffset;
            }

            return scrollOffset;
        }

        /// <summary>
        /// Resolves the count of visible items in the current viewport.
        /// </summary>
        /// <returns>Visible item count derived from the viewport or an explicit override.</returns>
        int GetVisibleItemCount() {
            if (VisibleItemCountValue > 0) {
                return VisibleItemCountValue;
            }

            int extent = ItemExtentValue;
            if (extent <= 0) {
                return 1;
            }

            int2 viewportSize = ResolveViewportSize();
            int scrollAxisExtent = OrientationValue == ScrollOrientation.Horizontal ? viewportSize.X : viewportSize.Y;
            if (ShowsPartialTrailingItem) {
                return Math.Max(1, (scrollAxisExtent + extent - 1) / extent);
            }

            return Math.Max(1, scrollAxisExtent / extent);
        }

        /// <summary>
        /// Resolves the active viewport size, inheriting the immediate parent clip-rect size when one exists.
        /// </summary>
        /// <returns>Viewport size used for clipping, hit testing, and automatic visible-count calculations.</returns>
        int2 ResolveViewportSize() {
            if (Parent != null && Parent.Parent != null && Parent.Parent.Components != null) {
                for (int componentIndex = 0; componentIndex < Parent.Parent.Components.Count; componentIndex++) {
                    if (Parent.Parent.Components[componentIndex] is ClipRectComponent clipRectComponent) {
                        return clipRectComponent.Size;
                    }
                }
            }

            return SizeValue;
        }
    }
}

