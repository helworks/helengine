namespace helengine {
    /// <summary>
    /// Tracks wheel-driven list scrolling for a rectangular viewport, acts as its own clip region, derives the visible range when needed, and can translate a bound content root automatically.
    /// </summary>
    public class ScrollComponent : UpdateComponent, IClipRegion2D {
        /// <summary>
        /// Standard mouse-wheel delta used to represent one notch on Windows-compatible devices.
        /// </summary>
        const int StandardWheelNotch = 120;

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
        /// Raised when the scroll offset changes because of wheel input or an explicit scroll request.
        /// </summary>
        public event Action<ScrollComponent, int> ScrollOffsetChanged;

        /// <summary>
        /// Gets or sets the viewport size used for pointer hit testing.
        /// </summary>
        public int2 Size {
            get { return SizeValue; }
            set {
                if (value.X < 0 || value.Y < 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scroll viewport size must not be negative.");
                }

                SizeValue = value;
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
        /// Gets or sets the number of items visible in the viewport.
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
        /// </summary>
        public Entity ContentRoot {
            get { return ContentRootValue; }
            set {
                ContentRootValue = value;
                ApplyContentRootOffset();
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
        /// Advances scrolling from the active mouse wheel while the pointer is inside the viewport.
        /// </summary>
        public override void Update() {
            TryApplyWheelInput();
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
            if (Parent == null) {
                return false;
            }

            if (MaximumScrollOffset <= 0) {
                return false;
            }

            if (RequiresPointerInsideValue && !ContainsScreenPoint(Core.Instance.Input.GetMouseX(), Core.Instance.Input.GetMouseY())) {
                return false;
            }

            int wheelDelta = Core.Instance.Input.GetMouseScrollWheelDelta();
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
            return true;
        }

        /// <summary>
        /// Applies the current scroll offset to the bound content root when one exists.
        /// </summary>
        void ApplyContentRootOffset() {
            if (ContentRootValue == null) {
                return;
            }

            float3 position = ContentRootValue.LocalPosition;
            ContentRootValue.LocalPosition = new float3(position.X, -(ScrollOffset * ItemExtentValue), position.Z);
        }

        /// <summary>
        /// Gets the clip rectangle used by descendants and pointer hit testing.
        /// </summary>
        /// <returns>Viewport rectangle expressed as X, Y, Width, Height.</returns>
        public float4 GetClipRect() {
            if (Parent == null) {
                throw new InvalidOperationException("Scroll components require an attached parent entity.");
            }

            float3 origin = Parent.Position;
            return new float4(origin.X, origin.Y, SizeValue.X, SizeValue.Y);
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
        /// Resolves the item count visible in the current viewport.
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

            return Math.Max(1, (SizeValue.Y + extent - 1) / extent);
        }
    }
}

