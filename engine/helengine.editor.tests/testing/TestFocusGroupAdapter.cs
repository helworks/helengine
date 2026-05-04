namespace helengine.editor.tests.testing {
    /// <summary>
    /// Interface adapter that exposes a <see cref="TestFocusGroup"/> through the editor focus-group contract.
    /// </summary>
    internal sealed class TestFocusGroupAdapter : IFocusGroup {
        /// <summary>
        /// Initializes one adapter for a test focus group.
        /// </summary>
        /// <param name="owner">Test focus group that owns this adapter.</param>
        public TestFocusGroupAdapter(TestFocusGroup owner) {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Gets the test focus group that owns this adapter.
        /// </summary>
        public TestFocusGroup Owner { get; }

        /// <summary>
        /// Gets the root dock group that owns this group.
        /// </summary>
        public IFocusGroup RootGroup => Owner.RootGroup;

        /// <summary>
        /// Gets the traversal order within the root dock.
        /// </summary>
        public int GroupOrder => Owner.GroupOrder;

        /// <summary>
        /// Gets whether this group can currently receive focus.
        /// </summary>
        public bool CanReceiveFocus => Owner.CanReceiveFocus;

        /// <summary>
        /// Returns true when the provided screen point is inside this group.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the group bounds.</returns>
        public bool ContainsScreenPoint(int2 point) {
            return Owner.ContainsScreenPoint(point);
        }

        /// <summary>
        /// Applies the active-state visual for this group.
        /// </summary>
        /// <param name="isActive">True when the group should appear active.</param>
        public void SetGroupActive(bool isActive) {
            Owner.SetGroupActive(isActive);
        }
    }
}
