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

            float3 worldPosition = entity.Position;
            float3 worldScale = entity.Scale;
            float4 worldOrientation = entity.Orientation;

            if (entity.Parent != null) {
                entity.Parent.RemoveChild(entity);
            }

            if (newParent != null) {
                newParent.AddChild(entity);
            }

            entity.LocalPosition = ResolveLocalPosition(worldPosition, newParent);
            entity.LocalScale = ResolveLocalScale(worldScale, newParent);
            entity.LocalOrientation = ResolveLocalOrientation(worldOrientation, newParent);

            return true;
        }

        /// <summary>
        /// Converts one preserved world position into the local space of the destination parent.
        /// </summary>
        /// <param name="worldPosition">World-space position to preserve.</param>
        /// <param name="newParent">Destination parent entity, or null for the scene root.</param>
        /// <returns>Local position that keeps the same world-space result after reparenting.</returns>
        float3 ResolveLocalPosition(float3 worldPosition, Entity newParent) {
            if (newParent == null) {
                return worldPosition;
            }

            float3 worldOffset = worldPosition - newParent.Position;
            float4 inverseParentOrientation = float4.Inverse(newParent.Orientation);
            return float4.RotateVector(worldOffset, inverseParentOrientation);
        }

        /// <summary>
        /// Converts one preserved world scale into the local scale required by the destination parent.
        /// </summary>
        /// <param name="worldScale">World-space scale to preserve.</param>
        /// <param name="newParent">Destination parent entity, or null for the scene root.</param>
        /// <returns>Local scale that keeps the same visible size after reparenting.</returns>
        float3 ResolveLocalScale(float3 worldScale, Entity newParent) {
            if (newParent == null) {
                return worldScale;
            }

            float3 parentScale = newParent.Scale;
            if (parentScale.X == 0f || parentScale.Y == 0f || parentScale.Z == 0f) {
                throw new InvalidOperationException("Cannot preserve world scale when the new parent has a zero scale component.");
            }

            return worldScale / parentScale;
        }

        /// <summary>
        /// Converts one preserved world orientation into the local orientation required by the destination parent.
        /// </summary>
        /// <param name="worldOrientation">World-space orientation to preserve.</param>
        /// <param name="newParent">Destination parent entity, or null for the scene root.</param>
        /// <returns>Local orientation that keeps the same world rotation after reparenting.</returns>
        float4 ResolveLocalOrientation(float4 worldOrientation, Entity newParent) {
            if (newParent == null) {
                return worldOrientation;
            }

            return worldOrientation * float4.Inverse(newParent.Orientation);
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
