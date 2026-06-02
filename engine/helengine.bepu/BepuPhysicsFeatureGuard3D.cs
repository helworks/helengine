namespace helengine {
    /// <summary>
    /// Rejects unsupported physics features during the first BEPU replacement pass.
    /// </summary>
    public static class BepuPhysicsFeatureGuard3D {
        /// <summary>
        /// Validates that one entity only uses collider and rigid-body features supported by the new runtime.
        /// </summary>
        /// <param name="entity">Entity to validate.</param>
        public static void ValidateEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<Component> components = entity.Components;
            if (components == null) {
                return;
            }

            for (int index = 0; index < components.Count; index++) {
                Component component = components[index];
                bool isSupportedCollider = component is BoxCollider3DComponent || component is SphereCollider3DComponent;
                if (component is Collider3DComponent && !isSupportedCollider) {
                    throw new NotSupportedException("Only box and sphere colliders are supported by helengine.bepu in the first replacement pass.");
                }
            }
        }
    }
}
