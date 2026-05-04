namespace helengine.editor.tests.testing {
    /// <summary>
    /// Focus-target test double that records focus and activation requests.
    /// </summary>
    internal sealed class TestFocusTarget : IFocusTarget {
        /// <summary>
        /// Initializes one focus-target test double with fixed hit-test bounds.
        /// </summary>
        /// <param name="focusGroup">Focus group that owns this target.</param>
        /// <param name="tabIndex">Traversal order within the owning group.</param>
        /// <param name="isDefaultTarget">True when this target should be preferred for dock entry.</param>
        /// <param name="left">Left edge of the target's screen bounds.</param>
        /// <param name="top">Top edge of the target's screen bounds.</param>
        /// <param name="width">Width of the target's screen bounds.</param>
        /// <param name="height">Height of the target's screen bounds.</param>
        public TestFocusTarget(IFocusGroup focusGroup, int tabIndex, bool isDefaultTarget, int left, int top, int width, int height) {
            FocusGroup = focusGroup ?? throw new ArgumentNullException(nameof(focusGroup));
            TabIndex = tabIndex;
            IsDefaultTarget = isDefaultTarget;
            Bounds = new int4(left, top, width, height);
            CanReceiveFocusValue = true;
        }

        /// <summary>
        /// Gets or sets the focus group that owns this target.
        /// </summary>
        public IFocusGroup FocusGroup { get; set; }

        /// <summary>
        /// Gets or sets the traversal order within the owning group.
        /// </summary>
        public int TabIndex { get; set; }

        /// <summary>
        /// Gets or sets whether this target is the preferred entry point for its root dock.
        /// </summary>
        public bool IsDefaultTarget { get; set; }

        /// <summary>
        /// Gets or sets whether this target may currently participate in focus traversal.
        /// </summary>
        public bool CanReceiveFocusValue { get; set; }

        /// <summary>
        /// Gets whether the service currently marks this target focused.
        /// </summary>
        public bool IsFocused { get; private set; }

        /// <summary>
        /// Gets the last key routed through this target's activation path.
        /// </summary>
        public Keys LastActivationKey { get; private set; }

        /// <summary>
        /// Gets the bounds used for pointer hit testing.
        /// </summary>
        public int4 Bounds { get; }

        /// <summary>
        /// Gets whether this target may currently participate in focus traversal.
        /// </summary>
        public bool CanReceiveFocus => CanReceiveFocusValue;

        /// <summary>
        /// Returns true when the provided screen point falls inside the target's bounds.
        /// </summary>
        /// <param name="x">Screen-space X coordinate to evaluate.</param>
        /// <param name="y">Screen-space Y coordinate to evaluate.</param>
        /// <returns>True when the point is inside the target's bounds.</returns>
        public bool ContainsScreenPoint(int x, int y) {
            return x >= Bounds.X &&
                   x < Bounds.X + Bounds.Z &&
                   y >= Bounds.Y &&
                   y < Bounds.Y + Bounds.W;
        }

        /// <summary>
        /// Records the focus state assigned by the keyboard-focus service.
        /// </summary>
        /// <param name="isFocused">True when this target should be focused.</param>
        public void SetTargetFocused(bool isFocused) {
            IsFocused = isFocused;
        }

        /// <summary>
        /// Returns true when this test target should activate from the provided key.
        /// </summary>
        /// <param name="key">Key to evaluate.</param>
        /// <returns>True for Enter and Space.</returns>
        public bool CanActivateWithKey(Keys key) {
            return key == Keys.Enter || key == Keys.Space;
        }

        /// <summary>
        /// Records the activation key routed through the keyboard-focus service.
        /// </summary>
        /// <param name="key">Activation key received by the target.</param>
        public void ActivateFromKey(Keys key) {
            LastActivationKey = key;
        }
    }
}
