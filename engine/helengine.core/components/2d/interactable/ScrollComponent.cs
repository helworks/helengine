namespace helengine {
    /// <summary>
    /// Tracks wheel-driven list scrolling for a rectangular viewport and exposes the current offset in item units.
    /// </summary>
    public class ScrollComponent : UpdateComponent {
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
        /// Number of items visible inside the current viewport.
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
        /// </summary>
        public int VisibleItemCount {
            get { return VisibleItemCountValue; }
            set {
                if (value < 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Visible item count must be zero or greater.");
                }

                VisibleItemCountValue = value;
                ClampScrollOffset();
            }
        }

        /// <summary>
        /// Gets the maximum scroll offset allowed by the current item and viewport counts.
        /// </summary>
        public int MaximumScrollOffset {
            get { return Math.Max(0, ItemCountValue - VisibleItemCountValue); }
        }

        /// <summary>
        /// Gets the current scroll offset in item units.
        /// </summary>
        public int ScrollOffset { get; private set; }

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

            float3 origin = Parent.Position;
            return x >= origin.X &&
                   x < origin.X + SizeValue.X &&
                   y >= origin.Y &&
                   y < origin.Y + SizeValue.Y;
        }

        /// <summary>
        /// Resets the scroll offset to the first visible item without raising a change event.
        /// </summary>
        public void ResetScrollOffset() {
            ScrollOffset = 0;
        }

        /// <summary>
        /// Clamps the current scroll offset to the active item range without raising a change event.
        /// </summary>
        public void ClampScrollOffset() {
            ScrollOffset = ClampOffset(ScrollOffset);
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

            return true;
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
    }
}

