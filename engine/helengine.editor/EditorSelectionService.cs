namespace helengine.editor {
    /// <summary>
    /// Tracks the current editor selection and raises selection change events.
    /// </summary>
    public sealed class EditorSelectionService : IDisposable {
        /// <summary>
        /// Stores the currently selected entity instance.
        /// </summary>
        Entity SelectedEntityValue;

        /// <summary>
        /// Raised when the selected entity changes.
        /// </summary>
        public event Action<EditorSelectionChangedEventArgs> SelectionChanged;

        /// <summary>
        /// Gets the currently selected entity.
        /// </summary>
        public Entity SelectedEntity {
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
        public void SetSelectedEntity(Entity entity) {
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
        public void ClearSelection() {
            SelectedEntityValue = null;
            RaiseSelectionChanged(new EditorSelectionChangedEventArgs(null, false));
        }

        /// <summary>
        /// Clears the current selection and removes all subscribers between tests or editor shutdown.
        /// </summary>
        public void Dispose() {
            SelectedEntityValue = null;
            SelectionChanged = null;
        }

        /// <summary>
        /// Raises the selection changed event.
        /// </summary>
        /// <param name="args">Selection change data.</param>
        void RaiseSelectionChanged(EditorSelectionChangedEventArgs args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            SelectionChanged?.Invoke(args);
        }
    }
}
