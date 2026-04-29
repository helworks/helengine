namespace helengine.editor {
    /// <summary>
    /// Delegate-backed focus-group wrapper used by editor-only surfaces.
    /// </summary>
    public class EditorFocusGroup : IFocusGroup {
        /// <summary>
        /// Resolves whether this group may currently receive focus.
        /// </summary>
        readonly Func<bool> CanReceiveFocusResolver;

        /// <summary>
        /// Resolves whether a screen point lies inside this group.
        /// </summary>
        readonly Func<int2, bool> ContainsScreenPointResolver;

        /// <summary>
        /// Applies the active-state visual for this group.
        /// </summary>
        readonly Action<bool> SetGroupActiveResolver;

        /// <summary>
        /// Initializes one delegate-backed focus group.
        /// </summary>
        /// <param name="rootGroup">Root dock group that owns this group.</param>
        /// <param name="groupOrder">Traversal order within the root dock.</param>
        /// <param name="canReceiveFocusResolver">Resolver for current focus eligibility.</param>
        /// <param name="containsScreenPointResolver">Resolver for pointer hit testing.</param>
        /// <param name="setGroupActiveResolver">Resolver for applying the active-state visual.</param>
        public EditorFocusGroup(
            IFocusGroup rootGroup,
            int groupOrder,
            Func<bool> canReceiveFocusResolver,
            Func<int2, bool> containsScreenPointResolver,
            Action<bool> setGroupActiveResolver) {
            RootGroup = rootGroup ?? throw new ArgumentNullException(nameof(rootGroup));
            GroupOrder = groupOrder;
            CanReceiveFocusResolver = canReceiveFocusResolver ?? throw new ArgumentNullException(nameof(canReceiveFocusResolver));
            ContainsScreenPointResolver = containsScreenPointResolver ?? throw new ArgumentNullException(nameof(containsScreenPointResolver));
            SetGroupActiveResolver = setGroupActiveResolver ?? throw new ArgumentNullException(nameof(setGroupActiveResolver));
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
        /// Gets whether this group can currently receive focus.
        /// </summary>
        public bool CanReceiveFocus => CanReceiveFocusResolver();

        /// <summary>
        /// Returns true when the provided screen point is inside this group.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the group bounds.</returns>
        public bool ContainsScreenPoint(int2 point) {
            return ContainsScreenPointResolver(point);
        }

        /// <summary>
        /// Applies the active-state visual for this group.
        /// </summary>
        /// <param name="isActive">True when the group should appear active.</param>
        public void SetGroupActive(bool isActive) {
            SetGroupActiveResolver(isActive);
        }
    }
}
