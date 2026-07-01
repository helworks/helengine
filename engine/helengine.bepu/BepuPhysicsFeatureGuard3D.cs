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

            RigidBody3DComponent rigidBody = null;
            StaticMeshCollider3DComponent staticMeshCollider = null;
            for (int index = 0; index < components.Count; index++) {
                Component component = components[index];
                if (component is RigidBody3DComponent body) {
                    rigidBody = body;
                } else if (component is StaticMeshCollider3DComponent meshCollider) {
                    staticMeshCollider = meshCollider;
                } else if (component is BoxCollider3DComponent) {
                } else if (component is SphereCollider3DComponent) {
                } else if (component is Collider3DComponent) {
                    throw new NotSupportedException("Only box, sphere, and cooked static mesh colliders are supported by helengine.bepu in the current replacement pass.");
                }
            }

            if (staticMeshCollider == null) {
                return;
            } else if (rigidBody == null || rigidBody.BodyKind != BodyKind3D.Static) {
                throw new NotSupportedException("Static mesh colliders are supported only for static rigid bodies in helengine.bepu.");
            } else if (staticMeshCollider.CookedRuntimeData == null) {
                throw new NotSupportedException("Static mesh colliders require one cooked runtime payload for helengine.bepu.");
            } else if (!string.Equals(staticMeshCollider.CookedRuntimeData.FormatId, BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, StringComparison.Ordinal)) {
                throw new NotSupportedException($"Static mesh collider payload format '{staticMeshCollider.CookedRuntimeData.FormatId}' is not supported by helengine.bepu.");
            }
        }
    }
}
