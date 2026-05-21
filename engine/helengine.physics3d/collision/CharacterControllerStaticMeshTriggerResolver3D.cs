#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_STATIC_MESH_CONTACT
namespace helengine {
    /// <summary>
    /// Detects trigger overlaps between character controllers and cooked static mesh colliders.
    /// </summary>
    public static class CharacterControllerStaticMeshTriggerResolver3D {
        /// <summary>
        /// Collects trigger overlaps for one controller against all trigger cooked static meshes in the world.
        /// </summary>
        /// <param name="controllerState">Controller being tested.</param>
        /// <param name="staticMeshStates">Cooked static meshes that can own trigger colliders.</param>
        /// <param name="currentTriggerPairs">Current step trigger pair list being populated.</param>
        public static void CollectOverlaps(CharacterControllerState3D controllerState, IReadOnlyList<StaticMeshBodyState3D> staticMeshStates, List<TriggerPairKey3D> currentTriggerPairs) {
            if (controllerState == null) {
                throw new ArgumentNullException(nameof(controllerState));
            }
            if (staticMeshStates == null) {
                throw new ArgumentNullException(nameof(staticMeshStates));
            }
            if (currentTriggerPairs == null) {
                throw new ArgumentNullException(nameof(currentTriggerPairs));
            }

            for (int index = 0; index < staticMeshStates.Count; index++) {
                StaticMeshBodyState3D meshState = staticMeshStates[index];
                if (!meshState.MeshCollider.IsTrigger) {
                    continue;
                }
                if (!CanCollidersInteract(controllerState.BoxCollider, meshState.MeshCollider)) {
                    continue;
                }
                if (!BoxStaticMeshContactResolver3D.TryResolveContact(controllerState.Position, controllerState.HalfExtents, meshState, out float3 contactNormal, out float penetrationDepth)) {
                    continue;
                }

                TriggerPairKey3D pairKey = new TriggerPairKey3D(meshState.Entity, controllerState.Entity);
                if (!currentTriggerPairs.Contains(pairKey)) {
                    currentTriggerPairs.Add(pairKey);
                }
            }
        }

        /// <summary>
        /// Determines whether the controller collider and one static-mesh trigger collider permit interaction according to their layers and masks.
        /// </summary>
        /// <param name="controllerCollider">Controller collider.</param>
        /// <param name="meshCollider">Static-mesh collider.</param>
        /// <returns>True when both filters permit interaction.</returns>
        static bool CanCollidersInteract(Collider3DComponent controllerCollider, Collider3DComponent meshCollider) {
            if (controllerCollider == null) {
                throw new ArgumentNullException(nameof(controllerCollider));
            }
            if (meshCollider == null) {
                throw new ArgumentNullException(nameof(meshCollider));
            }

            return (controllerCollider.CollisionMask & meshCollider.CollisionLayer) != 0 &&
                (meshCollider.CollisionMask & controllerCollider.CollisionLayer) != 0;
        }
    }
}
#endif
