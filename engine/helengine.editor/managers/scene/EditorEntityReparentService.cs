namespace helengine.editor {
    /// <summary>
    /// Applies validated entity-parent changes for editor scene hierarchy operations.
    /// </summary>
    public class EditorEntityReparentService {
        /// <summary>
        /// Reparents one entity to a new parent or to the scene root when the parent is null.
        /// </summary>
        /// <param name="entity">Entity to reparent.</param>
        /// <param name="newParent">New parent entity, or null for the scene root.</param>
        /// <returns>True when the hierarchy changed.</returns>
        public bool Reparent(Entity entity, Entity newParent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (ReferenceEquals(entity, newParent)) {
                throw new InvalidOperationException("An entity cannot be parented to itself.");
            }
            if (newParent != null && newParent.Children == null) {
                throw new InvalidOperationException("The new parent must initialize its child collection before reparenting.");
            }
            if (newParent != null && IsSameEntityOrDescendant(newParent, entity)) {
                throw new InvalidOperationException("An entity cannot be parented to one of its descendants.");
            }
            if (ReferenceEquals(entity.Parent, newParent)) {
                return false;
            }

            if (entity.Parent != null) {
                entity.Parent.RemoveChild(entity);
            }

            if (newParent != null) {
                newParent.AddChild(entity);
            }

            return true;
        }

        /// <summary>
        /// Returns true when the candidate matches the root entity or lies within its descendant chain.
        /// </summary>
        /// <param name="candidate">Entity being evaluated.</param>
        /// <param name="root">Root entity whose hierarchy should be excluded.</param>
        /// <returns>True when the candidate is the root or one of its descendants.</returns>
        bool IsSameEntityOrDescendant(Entity candidate, Entity root) {
            Entity current = candidate;
            while (current != null) {
                if (ReferenceEquals(current, root)) {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
