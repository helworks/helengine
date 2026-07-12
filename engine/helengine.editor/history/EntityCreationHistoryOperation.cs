namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one authored entity creation.
    /// </summary>
    public class EntityCreationHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Detached entity snapshot restored during redo and deleted during undo.
        /// </summary>
        readonly SerializedEditorEntityState EntityState;

        /// <summary>
        /// Stable scene entity id that should be restored to the selection after undo, or zero when no scene entity was selected beforehand.
        /// </summary>
        readonly uint PreviousSelectionEntityId;

        /// <summary>
        /// Initializes one entity-creation history operation.
        /// </summary>
        /// <param name="entityState">Detached serialized entity snapshot.</param>
        /// <param name="previousSelectionEntityId">Stable scene entity id selected before the creation, or zero when none existed.</param>
        public EntityCreationHistoryOperation(SerializedEditorEntityState entityState, uint previousSelectionEntityId) {
            EntityState = entityState ?? throw new ArgumentNullException(nameof(entityState));
            PreviousSelectionEntityId = previousSelectionEntityId;
        }

        /// <summary>
        /// Gets a short human-readable description of this history operation.
        /// </summary>
        public string Description {
            get { return "Create Entity"; }
        }

        /// <summary>
        /// Deletes the previously created entity and restores the prior selection when available.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.DeleteEntityById(EntityState.EntityId);
            if (PreviousSelectionEntityId != 0u) {
                context.RestoreSelectionByEntityId(PreviousSelectionEntityId);
            } else {
                context.ClearSelection();
            }
        }

        /// <summary>
        /// Restores the created entity and selects it again.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.RestoreEntity(EntityState);
            context.RestoreSelectionByEntityId(EntityState.EntityId);
        }
    }
}
