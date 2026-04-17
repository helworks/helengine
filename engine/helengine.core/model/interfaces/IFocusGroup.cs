namespace helengine {
    /// <summary>
    /// Represents one logical keyboard-focus scope.
    /// </summary>
    public interface IFocusGroup {
        /// <summary>
        /// Gets the root dock group that owns this group.
        /// </summary>
        IFocusGroup RootGroup { get; }

        /// <summary>
        /// Gets the traversal order within the root dock.
        /// </summary>
        int GroupOrder { get; }

        /// <summary>
        /// Gets whether this group can currently receive focus.
        /// </summary>
        bool CanReceiveFocus { get; }

        /// <summary>
        /// Returns true when the provided screen point is inside this group.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the group bounds.</returns>
        bool ContainsScreenPoint(int2 point);

        /// <summary>
        /// Applies the active-state visual for this group.
        /// </summary>
        /// <param name="isActive">True when the group should appear active.</param>
        void SetGroupActive(bool isActive);
    }
}
