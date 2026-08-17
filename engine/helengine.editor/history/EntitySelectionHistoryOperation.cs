namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one user-initiated selection change without affecting saved scene state.
    /// </summary>
    public class EntitySelectionHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Stable scene entity id selected before the change, or zero when nothing was selected.
        /// </summary>
        readonly uint PreviousEntityId;

        /// <summary>
        /// Stable scene entity id selected after the change, or zero when the change cleared the selection.
        /// </summary>
        readonly uint CurrentEntityId;

        /// <summary>
        /// Initializes one selection-change history operation.
        /// </summary>
        /// <param name="previousEntityId">Stable scene entity id selected before the change, or zero when none existed.</param>
        /// <param name="currentEntityId">Stable scene entity id selected after the change, or zero when the selection was cleared.</param>
        public EntitySelectionHistoryOperation(uint previousEntityId, uint currentEntityId) {
            if (previousEntityId == currentEntityId) {
                throw new ArgumentException("Selection history requires the previous and current selection to differ.", nameof(currentEntityId));
            }

            PreviousEntityId = previousEntityId;
            CurrentEntityId = currentEntityId;
        }

        /// <summary>
        /// Gets a short human-readable description of this history operation.
        /// </summary>
        public string Description {
            get { return "Change Selection"; }
        }

        /// <summary>
        /// Gets whether this operation mutates saved scene state; selection changes never do.
        /// </summary>
        public bool AffectsSavedState {
            get { return false; }
        }

        /// <summary>
        /// Restores the selection that existed before the change.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            ApplySelection(context, PreviousEntityId);
        }

        /// <summary>
        /// Reapplies the selection produced by the change.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            ApplySelection(context, CurrentEntityId);
        }

        /// <summary>
        /// Applies one selection target, clearing the selection when the id is zero.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        /// <param name="entityId">Stable scene entity id to select, or zero to clear the selection.</param>
        static void ApplySelection(EditorHistoryContext context, uint entityId) {
            if (entityId == 0u) {
                context.ClearSelection();
                return;
            }

            context.RestoreSelectionByEntityId(entityId);
        }
    }
}
