#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER
namespace helengine {
    /// <summary>
    /// Detects trigger overlaps between character controllers and trigger rigid bodies.
    /// </summary>
    public static class CharacterControllerBodyTriggerResolver3D {
        /// <summary>
        /// Collects trigger overlaps for one controller against all trigger rigid bodies in the world.
        /// </summary>
        /// <param name="controllerState">Controller being tested.</param>
        /// <param name="bodyStates">Rigid bodies that can own trigger colliders.</param>
        /// <param name="currentTriggerPairs">Current step trigger pair list being populated.</param>
        public static void CollectOverlaps(CharacterControllerState3D controllerState, IReadOnlyList<BodyState3D> bodyStates, List<TriggerPairKey3D> currentTriggerPairs) {
            if (controllerState == null) {
                throw new ArgumentNullException(nameof(controllerState));
            }
            if (bodyStates == null) {
                throw new ArgumentNullException(nameof(bodyStates));
            }
            if (currentTriggerPairs == null) {
                throw new ArgumentNullException(nameof(currentTriggerPairs));
            }

            for (int index = 0; index < bodyStates.Count; index++) {
                BodyState3D bodyState = bodyStates[index];
                if (!bodyState.Collider.IsTrigger) {
                    continue;
                }
                if (!CanCollidersInteract(controllerState.BoxCollider, bodyState.Collider)) {
                    continue;
                }
                if (!PrimitiveContactMath3D.Overlaps(controllerState.Position, controllerState.HalfExtents, bodyState.Position, bodyState.HalfExtents)) {
                    continue;
                }

                TriggerPairKey3D pairKey = new TriggerPairKey3D(bodyState.Entity, controllerState.Entity);
                if (!currentTriggerPairs.Contains(pairKey)) {
                    currentTriggerPairs.Add(pairKey);
                }
            }
        }

        /// <summary>
        /// Determines whether the controller collider and one rigid-body trigger collider permit interaction according to their layers and masks.
        /// </summary>
        /// <param name="controllerCollider">Controller collider.</param>
        /// <param name="otherCollider">Other rigid-body collider.</param>
        /// <returns>True when both filters permit interaction.</returns>
        static bool CanCollidersInteract(Collider3DComponent controllerCollider, Collider3DComponent otherCollider) {
            if (controllerCollider == null) {
                throw new ArgumentNullException(nameof(controllerCollider));
            }
            if (otherCollider == null) {
                throw new ArgumentNullException(nameof(otherCollider));
            }

            return (controllerCollider.CollisionMask & otherCollider.CollisionLayer) != 0 &&
                (otherCollider.CollisionMask & controllerCollider.CollisionLayer) != 0;
        }
    }
}
#endif
