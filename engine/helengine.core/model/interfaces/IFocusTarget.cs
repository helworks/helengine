namespace helengine {
    /// <summary>
    /// Represents one keyboard-focusable control or logical target.
    /// </summary>
    public interface IFocusTarget {
        /// <summary>
        /// Gets the focus group that owns this target.
        /// </summary>
        IFocusGroup FocusGroup { get; }

        /// <summary>
        /// Gets the traversal order within the owning group.
        /// </summary>
        int TabIndex { get; }

        /// <summary>
        /// Gets whether this target is the preferred entry point for its root dock.
        /// </summary>
        bool IsDefaultTarget { get; }

        /// <summary>
        /// Gets whether this target can currently receive focus.
        /// </summary>
        bool CanReceiveFocus { get; }

        /// <summary>
        /// Returns true when the provided screen point is inside this target.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the target bounds.</returns>
        bool ContainsScreenPoint(int2 point);

        /// <summary>
        /// Applies the focused-state visual for this target.
        /// </summary>
        /// <param name="isFocused">True when the target should appear focused.</param>
        void SetTargetFocused(bool isFocused);

        /// <summary>
        /// Returns true when this target should react to the provided activation key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when the target should activate for the key.</returns>
        bool CanActivateWithKey(Keys key);

        /// <summary>
        /// Performs the target's activation behavior for the provided key.
        /// </summary>
        /// <param name="key">Activation key routed to the target.</param>
        void ActivateFromKey(Keys key);
    }
}
