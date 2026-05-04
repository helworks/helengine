#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_SPHERE_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves sphere-sphere contact information.
    /// </summary>
    public static class SphereSphereContactResolver3D {
        /// <summary>
        /// Finds the collision normal and penetration depth for one overlapping sphere pair.
        /// </summary>
        /// <param name="first">First sphere body state.</param>
        /// <param name="second">Second sphere body state.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second sphere toward the first sphere.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the spheres overlap.</returns>
        public static bool TryResolveContact(BodyState3D first, BodyState3D second, out float3 collisionNormal, out float penetration) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            float3 centerOffset = first.Position - second.Position;
            float distanceSquared = float3.Dot(centerOffset, centerOffset);
            float combinedRadius = first.SphereRadius + second.SphereRadius;
            float combinedRadiusSquared = combinedRadius * combinedRadius;
            if (distanceSquared >= combinedRadiusSquared) {
                collisionNormal = float3.Zero;
                penetration = 0f;
                return false;
            }

            float distance = (float)Math.Sqrt(distanceSquared);
            if (distance <= 0.0001f) {
                collisionNormal = new float3(0f, 1f, 0f);
            } else {
                collisionNormal = centerOffset / distance;
            }

            penetration = combinedRadius - distance;
            return true;
        }
    }
}
#endif
