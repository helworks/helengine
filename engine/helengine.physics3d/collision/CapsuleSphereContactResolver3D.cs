#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_SPHERE_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves capsule-sphere contact information.
    /// </summary>
    public static class CapsuleSphereContactResolver3D {
        /// <summary>
        /// Finds the collision normal and penetration depth for one overlapping capsule-sphere pair.
        /// </summary>
        /// <param name="capsuleBody">Capsule body state.</param>
        /// <param name="sphereBody">Sphere body state.</param>
        /// <param name="collisionNormal">Unit normal pointing from the sphere toward the capsule.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the capsule overlaps the sphere.</returns>
        public static bool TryResolveContact(BodyState3D capsuleBody, BodyState3D sphereBody, out float3 collisionNormal, out float penetration) {
            if (capsuleBody == null) {
                throw new ArgumentNullException(nameof(capsuleBody));
            }
            if (sphereBody == null) {
                throw new ArgumentNullException(nameof(sphereBody));
            }

            float3 closestCapsulePoint = PrimitiveContactMath3D.GetClosestPointOnVerticalCapsuleSegment(capsuleBody, sphereBody.Position.Y);
            float3 delta = closestCapsulePoint - sphereBody.Position;
            float distanceSquared = float3.Dot(delta, delta);
            float combinedRadius = capsuleBody.SphereRadius + sphereBody.SphereRadius;
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
