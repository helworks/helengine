namespace helengine {
    /// <summary>
    /// Detects trigger overlaps between primitive rigid bodies and cooked static mesh colliders.
    /// </summary>
    public static class StaticMeshTriggerResolver3D {
        /// <summary>
        /// Determines whether one primitive body currently overlaps one cooked static mesh.
        /// </summary>
        /// <param name="bodyState">Primitive body being tested.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <returns>True when the supplied body overlaps the supplied cooked mesh.</returns>
        public static bool TryResolveOverlap(BodyState3D bodyState, StaticMeshBodyState3D meshState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            if (bodyState.ColliderShapeKind == ColliderShapeKind3D.Sphere) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_STATIC_MESH_CONTACT
                return SphereStaticMeshContactResolver3D.TryResolveContact(bodyState, meshState, out _, out _);
#else
                return false;
#endif
            }
            if (bodyState.ColliderShapeKind == ColliderShapeKind3D.Capsule) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_STATIC_MESH_CONTACT
                return CapsuleStaticMeshContactResolver3D.TryResolveContact(bodyState, meshState, out _, out _);
#else
                return false;
#endif
            }
            if (bodyState.ColliderShapeKind == ColliderShapeKind3D.Box) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_STATIC_MESH_CONTACT
                return BoxStaticMeshContactResolver3D.TryResolveContact(bodyState, meshState, out _, out _);
#else
                return false;
#endif
            }

            throw new InvalidOperationException($"Unsupported primitive-to-static-mesh trigger query for collider kind '{bodyState.ColliderShapeKind}'.");
        }
    }
}
