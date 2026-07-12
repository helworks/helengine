namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one entity-scoped mutation by restoring detached before/after entity snapshots.
    /// </summary>
    public class EntityStateChangeHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Detached entity snapshot captured before the mutation.
        /// </summary>
        readonly SerializedEditorEntityState PreviousEntityState;

        /// <summary>
        /// Detached entity snapshot captured after the mutation.
        /// </summary>
        readonly SerializedEditorEntityState CurrentEntityState;

        /// <summary>
        /// Initializes one entity-state change history operation.
        /// </summary>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <param name="currentEntityState">Detached entity snapshot captured after the mutation.</param>
        public EntityStateChangeHistoryOperation(
            SerializedEditorEntityState previousEntityState,
            SerializedEditorEntityState currentEntityState) {
            PreviousEntityState = previousEntityState ?? throw new ArgumentNullException(nameof(previousEntityState));
            CurrentEntityState = currentEntityState ?? throw new ArgumentNullException(nameof(currentEntityState));
        }

        /// <summary>
        /// Gets a short human-readable description of this history operation.
        /// </summary>
        public string Description {
            get { return "Edit Entity"; }
        }

        /// <summary>
        /// Restores the entity snapshot captured before the mutation and reselects the entity.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.DeleteEntityById(CurrentEntityState.EntityId);
            context.RestoreEntity(PreviousEntityState);
            context.RestoreSelectionByEntityId(PreviousEntityState.EntityId);
        }

        /// <summary>
        /// Restores the entity snapshot captured after the mutation and reselects the entity.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.DeleteEntityById(PreviousEntityState.EntityId);
            context.RestoreEntity(CurrentEntityState);
            context.RestoreSelectionByEntityId(CurrentEntityState.EntityId);
        }
    }
}
