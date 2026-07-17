namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one authored entity deletion.
    /// </summary>
    public class EntityDeletionHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Detached entity snapshot restored during undo and deleted again during redo.
        /// </summary>
        readonly SerializedEditorEntityState EntityState;

        /// <summary>
        /// Initializes one entity-deletion history operation.
        /// </summary>
        /// <param name="entityState">Detached serialized entity snapshot captured before deletion.</param>
        public EntityDeletionHistoryOperation(SerializedEditorEntityState entityState) {
            EntityState = entityState ?? throw new ArgumentNullException(nameof(entityState));
        }

        /// <summary>
        /// Gets a short human-readable description of this history operation.
        /// </summary>
        public string Description {
            get { return "Delete Entity"; }
        }

        /// <summary>
        /// Restores the previously deleted entity and selects it again.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.RestoreEntity(EntityState);
            context.RestoreSelectionByEntityId(EntityState.EntityId);
        }

        /// <summary>
        /// Deletes the restored entity again and clears the current selection.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.DeleteEntityById(EntityState.EntityId);
            context.ClearSelection();
        }
    }
}
