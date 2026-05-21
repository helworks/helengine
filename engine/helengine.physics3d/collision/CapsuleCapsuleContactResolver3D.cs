#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_CAPSULE_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves capsule-capsule contact information.
    /// </summary>
    public static class CapsuleCapsuleContactResolver3D {
        /// <summary>
        /// Finds the collision normal and penetration depth for one overlapping capsule pair.
        /// </summary>
        /// <param name="first">First capsule body state.</param>
        /// <param name="second">Second capsule body state.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second capsule toward the first capsule.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the capsules overlap.</returns>
        public static bool TryResolveContact(BodyState3D first, BodyState3D second, out float3 collisionNormal, out float penetration) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            float firstY = PrimitiveContactMath3D.Clamp(second.Position.Y, first.Position.Y - first.CapsuleSegmentHalfLength, first.Position.Y + first.CapsuleSegmentHalfLength);
            float secondY = PrimitiveContactMath3D.Clamp(firstY, second.Position.Y - second.CapsuleSegmentHalfLength, second.Position.Y + second.CapsuleSegmentHalfLength);
            firstY = PrimitiveContactMath3D.Clamp(secondY, first.Position.Y - first.CapsuleSegmentHalfLength, first.Position.Y + first.CapsuleSegmentHalfLength);
            float3 firstPoint = new float3(first.Position.X, firstY, first.Position.Z);
            float3 secondPoint = new float3(second.Position.X, secondY, second.Position.Z);
            float3 delta = firstPoint - secondPoint;
            float distanceSquared = float3.Dot(delta, delta);
            float combinedRadius = first.SphereRadius + second.SphereRadius;
            float combinedRadiusSquared = combinedRadius * combinedRadius;
            if (distanceSquared > combinedRadiusSquared) {
                collisionNormal = float3.Zero;
                penetration = 0f;
                return false;
            }

            if (distanceSquared > 0.0000001f) {
                float distance = (float)Math.Sqrt(distanceSquared);
                collisionNormal = delta / distance;
                penetration = combinedRadius - distance;
                return true;
            }

            collisionNormal = new float3(0f, 1f, 0f);
            penetration = combinedRadius;
            return true;
        }
    }
}
#endif
