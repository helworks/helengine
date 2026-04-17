namespace helengine.editor.tests.testing {
    /// <summary>
    /// Focus-group test double that records active-state changes and screen bounds.
    /// </summary>
    public sealed class TestFocusGroup : IFocusGroup {
        /// <summary>
        /// Initializes one focus-group test double with fixed hit-test bounds.
        /// </summary>
        /// <param name="rootGroup">Root dock group that owns this group, or this group when null.</param>
        /// <param name="groupOrder">Traversal order within the root dock.</param>
        /// <param name="left">Left edge of the group's screen bounds.</param>
        /// <param name="top">Top edge of the group's screen bounds.</param>
        /// <param name="width">Width of the group's screen bounds.</param>
        /// <param name="height">Height of the group's screen bounds.</param>
        public TestFocusGroup(IFocusGroup rootGroup, int groupOrder, int left, int top, int width, int height) {
            RootGroup = rootGroup ?? this;
            GroupOrder = groupOrder;
            Bounds = new int4(left, top, width, height);
            CanReceiveFocusValue = true;
        }

        /// <summary>
        /// Gets the root dock group that owns this group.
        /// </summary>
        public IFocusGroup RootGroup { get; }

        /// <summary>
        /// Gets the traversal order within the root dock.
        /// </summary>
        public int GroupOrder { get; }

        /// <summary>
        /// Gets or sets whether this group may currently participate in focus traversal.
        /// </summary>
        public bool CanReceiveFocusValue { get; set; }

        /// <summary>
        /// Gets whether the service currently marks this group active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets the bounds used for pointer hit testing.
        /// </summary>
        public int4 Bounds { get; }

        /// <summary>
        /// Gets whether this group may currently participate in focus traversal.
        /// </summary>
        public bool CanReceiveFocus => CanReceiveFocusValue;

        /// <summary>
        /// Returns true when the provided screen point falls inside the group's bounds.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point is inside the group's bounds.</returns>
        public bool ContainsScreenPoint(int2 point) {
            return point.X >= Bounds.X &&
                   point.X < Bounds.X + Bounds.Z &&
                   point.Y >= Bounds.Y &&
                   point.Y < Bounds.Y + Bounds.W;
        }

        /// <summary>
        /// Records the active-state assigned by the keyboard-focus service.
        /// </summary>
        /// <param name="isActive">True when this group should be active.</param>
        public void SetGroupActive(bool isActive) {
            IsActive = isActive;
        }
    }
}
