namespace helengine.editor {
    /// <summary>
    /// Tracks the current editor selection and raises selection change events.
    /// </summary>
    public static class EditorSelectionService {
        /// <summary>
        /// Stores the currently selected entity instance.
        /// </summary>
        static Entity SelectedEntityValue;

        /// <summary>
        /// Raised when the selected entity changes.
        /// </summary>
        public static event Action<EditorSelectionChangedEventArgs> SelectionChanged;

        /// <summary>
        /// Gets the currently selected entity.
        /// </summary>
        public static Entity SelectedEntity {
            get {
                if (SelectedEntityValue != null && SelectedEntityValue.IsDisposed) {
                    SelectedEntityValue = null;
                }

                return SelectedEntityValue;
            }
        }

        /// <summary>
        /// Sets the selected entity and raises a change event.
        /// </summary>
        /// <param name="entity">Entity to select.</param>
        public static void SetSelectedEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.IsDisposed) {
                throw new InvalidOperationException("Disposed entities cannot be selected.");
            }

            SelectedEntityValue = entity;
            RaiseSelectionChanged(new EditorSelectionChangedEventArgs(entity, true));
        }

        /// <summary>
        /// Clears the current selection and raises a change event.
        /// </summary>
        public static void ClearSelection() {
            SelectedEntityValue = null;
            RaiseSelectionChanged(new EditorSelectionChangedEventArgs(null, false));
        }

        /// <summary>
        /// Clears the current selection and removes all subscribers between tests or editor shutdown.
        /// </summary>
        public static void Reset() {
            SelectedEntityValue = null;
            SelectionChanged = null;
        }

        /// <summary>
        /// Raises the selection changed event.
        /// </summary>
        /// <param name="args">Selection change data.</param>
        static void RaiseSelectionChanged(EditorSelectionChangedEventArgs args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            SelectionChanged?.Invoke(args);
        }
    }
}
