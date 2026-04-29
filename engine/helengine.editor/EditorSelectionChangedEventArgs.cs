namespace helengine.editor {
    /// <summary>
    /// Provides selection change data for editor entity selection events.
    /// </summary>
    public sealed class EditorSelectionChangedEventArgs {
        /// <summary>
        /// Stores the selected entity instance.
        /// </summary>
        readonly Entity Selected;
        /// <summary>
        /// Stores whether the selection is currently valid.
        /// </summary>
        readonly bool HasSelectionValue;

        /// <summary>
        /// Initializes a new selection change payload.
        /// </summary>
        /// <param name="selected">Selected entity instance.</param>
        /// <param name="hasSelection">True when a selection is present.</param>
        public EditorSelectionChangedEventArgs(Entity selected, bool hasSelection) {
            if (hasSelection && selected == null) {
                throw new ArgumentNullException(nameof(selected));
            }

            Selected = selected;
            HasSelectionValue = hasSelection;
        }

        /// <summary>
        /// Gets the currently selected entity.
        /// </summary>
        public Entity SelectedEntity => Selected;

        /// <summary>
        /// Gets a value indicating whether a selection is active.
        /// </summary>
        public bool HasSelection => HasSelectionValue;
    }
}
