namespace helengine.editor {
    /// <summary>
    /// Delegate-backed focus-target wrapper used by editor-only surfaces.
    /// </summary>
    public class EditorFocusTarget : IFocusTarget {
        /// <summary>
        /// Resolves whether this target may currently receive focus.
        /// </summary>
        readonly Func<bool> CanReceiveFocusResolver;

        /// <summary>
        /// Resolves whether a screen point lies inside this target.
        /// </summary>
        readonly Func<int2, bool> ContainsScreenPointResolver;

        /// <summary>
        /// Applies the focused-state visual for this target.
        /// </summary>
        readonly Action<bool> SetTargetFocusedResolver;

        /// <summary>
        /// Resolves whether the target should react to a provided activation key.
        /// </summary>
        readonly Func<Keys, bool> CanActivateWithKeyResolver;

        /// <summary>
        /// Performs the target activation behavior for a provided key.
        /// </summary>
        readonly Action<Keys> ActivateFromKeyResolver;

        /// <summary>
        /// Initializes one delegate-backed focus target.
        /// </summary>
        /// <param name="focusGroup">Focus group that owns this target, or null until an editor surface binds it.</param>
        /// <param name="tabIndex">Traversal order within the owning group.</param>
        /// <param name="isDefaultTarget">True when the target is preferred for dock entry.</param>
        /// <param name="canReceiveFocusResolver">Resolver for current focus eligibility.</param>
        /// <param name="containsScreenPointResolver">Resolver for pointer hit testing.</param>
        /// <param name="setTargetFocusedResolver">Resolver for applying the focused-state visual.</param>
        /// <param name="canActivateWithKeyResolver">Resolver for key-based activation eligibility.</param>
        /// <param name="activateFromKeyResolver">Resolver for key-based activation behavior.</param>
        public EditorFocusTarget(
            IFocusGroup focusGroup,
            int tabIndex,
            bool isDefaultTarget,
            Func<bool> canReceiveFocusResolver,
            Func<int2, bool> containsScreenPointResolver,
            Action<bool> setTargetFocusedResolver,
            Func<Keys, bool> canActivateWithKeyResolver,
            Action<Keys> activateFromKeyResolver) {
            FocusGroup = focusGroup;
            TabIndex = tabIndex;
            IsDefaultTarget = isDefaultTarget;
            CanReceiveFocusResolver = canReceiveFocusResolver ?? throw new ArgumentNullException(nameof(canReceiveFocusResolver));
            ContainsScreenPointResolver = containsScreenPointResolver ?? throw new ArgumentNullException(nameof(containsScreenPointResolver));
            SetTargetFocusedResolver = setTargetFocusedResolver ?? throw new ArgumentNullException(nameof(setTargetFocusedResolver));
            CanActivateWithKeyResolver = canActivateWithKeyResolver ?? throw new ArgumentNullException(nameof(canActivateWithKeyResolver));
            ActivateFromKeyResolver = activateFromKeyResolver ?? throw new ArgumentNullException(nameof(activateFromKeyResolver));
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
        /// Gets whether this target can currently receive focus.
        /// </summary>
        public bool CanReceiveFocus => CanReceiveFocusResolver();

        /// <summary>
        /// Returns true when the provided screen point is inside this target.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the target bounds.</returns>
        public bool ContainsScreenPoint(int2 point) {
            return ContainsScreenPointResolver(point);
        }

        /// <summary>
        /// Applies the focused-state visual for this target.
        /// </summary>
        /// <param name="isFocused">True when the target should appear focused.</param>
        public void SetTargetFocused(bool isFocused) {
            SetTargetFocusedResolver(isFocused);
        }

        /// <summary>
        /// Returns true when this target should react to the provided activation key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when the target should activate for the key.</returns>
        public bool CanActivateWithKey(Keys key) {
            return CanActivateWithKeyResolver(key);
        }

        /// <summary>
        /// Performs the target's activation behavior for the provided key.
        /// </summary>
        /// <param name="key">Activation key routed to the target.</param>
        public void ActivateFromKey(Keys key) {
            ActivateFromKeyResolver(key);
        }
    }
}
