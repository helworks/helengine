namespace helengine.editor {
    /// <summary>
    /// Represents one confirmed reparent selection from the reparent dialog.
    /// </summary>
    public class ReparentEntityDialogSelection {
        /// <summary>
        /// Initializes a new reparent selection.
        /// </summary>
        /// <param name="targetEntity">Entity being reparented.</param>
        /// <param name="parentEntity">Destination parent entity, or null for the scene root.</param>
        public ReparentEntityDialogSelection(Entity targetEntity, Entity parentEntity) {
            if (targetEntity == null) {
                throw new ArgumentNullException(nameof(targetEntity));
            }

            TargetEntity = targetEntity;
            ParentEntity = parentEntity;
        }

        /// <summary>
        /// Gets the entity being reparented.
        /// </summary>
        public Entity TargetEntity { get; }

        /// <summary>
        /// Gets the destination parent entity, or null for the scene root.
        /// </summary>
        public Entity ParentEntity { get; }
    }
}
