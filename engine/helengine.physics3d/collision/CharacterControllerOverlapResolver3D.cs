#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER
namespace helengine {
    /// <summary>
    /// Resolves solid body overlaps for character controllers after motion has been integrated for the current step.
    /// </summary>
    public static class CharacterControllerOverlapResolver3D {
        /// <summary>
        /// Resolves controller overlaps against non-support rigid bodies after the controller has been moved for the current step.
        /// </summary>
        /// <param name="bodyStates">Runtime rigid bodies that can block controller movement.</param>
        /// <param name="controllerHalfExtents">Current controller half extents.</param>
        /// <param name="supportBodyState">Support body currently holding the controller, when present.</param>
        /// <param name="position">Controller position to resolve.</param>
        /// <param name="verticalVelocity">Controller vertical velocity to clip when a vertical collision occurs.</param>
        public static void Resolve(
            IReadOnlyList<BodyState3D> bodyStates,
            float3 controllerHalfExtents,
            BodyState3D supportBodyState,
            ref float3 position,
            ref float verticalVelocity) {
            if (bodyStates == null) {
                throw new ArgumentNullException(nameof(bodyStates));
            }

            for (int iteration = 0; iteration < 2; iteration++) {
                for (int index = 0; index < bodyStates.Count; index++) {
                    BodyState3D bodyState = bodyStates[index];
                    if (bodyState == supportBodyState) {
                        continue;
                    }
                    if (bodyState.Collider.IsTrigger) {
                        continue;
                    }
                    if (!PrimitiveContactMath3D.Overlaps(position, controllerHalfExtents, bodyState.Position, bodyState.HalfExtents)) {
                        continue;
                    }

                    float xPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(position.X, controllerHalfExtents.X, bodyState.Position.X, bodyState.HalfExtents.X);
                    float yPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(position.Y, controllerHalfExtents.Y, bodyState.Position.Y, bodyState.HalfExtents.Y);
                    float zPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(position.Z, controllerHalfExtents.Z, bodyState.Position.Z, bodyState.HalfExtents.Z);
                    if (xPenetration <= 0.0001f || yPenetration <= 0.0001f || zPenetration <= 0.0001f) {
                        continue;
                    }

                    if (xPenetration <= yPenetration && xPenetration <= zPenetration) {
                        float axisDirection = PrimitiveContactMath3D.GetAxisDirection(position, bodyState.Position, 0);
                        position = PrimitiveContactMath3D.OffsetAxis(position, 0, xPenetration * axisDirection);
                        continue;
                    }

                    if (yPenetration <= zPenetration) {
                        float axisDirection = PrimitiveContactMath3D.GetAxisDirection(position, bodyState.Position, 1);
                        position = PrimitiveContactMath3D.OffsetAxis(position, 1, yPenetration * axisDirection);
                        verticalVelocity = 0f;
                        continue;
                    }

                    float zAxisDirection = PrimitiveContactMath3D.GetAxisDirection(position, bodyState.Position, 2);
                    position = PrimitiveContactMath3D.OffsetAxis(position, 2, zPenetration * zAxisDirection);
                }
            }
        }
    }
}
#endif
