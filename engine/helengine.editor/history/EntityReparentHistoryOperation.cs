namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one authored scene hierarchy reparent mutation.
    /// </summary>
    public class EntityReparentHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Stable scene entity id of the reparented entity.
        /// </summary>
        readonly uint EntityId;

        /// <summary>
        /// Stable scene entity id of the source parent, or zero when the entity originally belonged at the scene root.
        /// </summary>
        readonly uint PreviousParentEntityId;

        /// <summary>
        /// Stable scene entity id of the destination parent, or zero when the entity moved to the scene root.
        /// </summary>
        readonly uint CurrentParentEntityId;

        /// <summary>
        /// Initializes one entity-reparent history operation.
        /// </summary>
        /// <param name="entityId">Stable scene entity id of the reparented entity.</param>
        /// <param name="previousParentEntityId">Stable scene entity id of the original parent, or zero for the scene root.</param>
        /// <param name="currentParentEntityId">Stable scene entity id of the new parent, or zero for the scene root.</param>
        public EntityReparentHistoryOperation(uint entityId, uint previousParentEntityId, uint currentParentEntityId) {
            if (entityId == 0u) {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Reparent history requires one non-zero scene entity id.");
            }

            EntityId = entityId;
            PreviousParentEntityId = previousParentEntityId;
            CurrentParentEntityId = currentParentEntityId;
        }

        /// <summary>
        /// Gets a short human-readable description of this history operation.
        /// </summary>
        public string Description {
            get { return "Reparent Entity"; }
        }

        /// <summary>
        /// Restores the original parent and keeps the reparented entity selected.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.ReparentEntity(EntityId, PreviousParentEntityId);
            context.RestoreSelectionByEntityId(EntityId);
        }

        /// <summary>
        /// Reapplies the destination parent and keeps the reparented entity selected.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.ReparentEntity(EntityId, CurrentParentEntityId);
            context.RestoreSelectionByEntityId(EntityId);
        }
    }
}
