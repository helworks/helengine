#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_BOX_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves sphere-box contact information.
    /// </summary>
    public static class SphereBoxContactResolver3D {
        /// <summary>
        /// Finds the collision normal and penetration depth for one overlapping sphere-box pair.
        /// </summary>
        /// <param name="sphereBody">Sphere body state.</param>
        /// <param name="boxBody">Box body state.</param>
        /// <param name="collisionNormal">Unit normal pointing from the box toward the sphere.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the sphere overlaps the box.</returns>
        public static bool TryResolveContact(BodyState3D sphereBody, BodyState3D boxBody, out float3 collisionNormal, out float penetration) {
            if (sphereBody == null) {
                throw new ArgumentNullException(nameof(sphereBody));
            }
            if (boxBody == null) {
                throw new ArgumentNullException(nameof(boxBody));
            }

            float minX = boxBody.Position.X - boxBody.HalfExtents.X;
            float maxX = boxBody.Position.X + boxBody.HalfExtents.X;
            float minY = boxBody.Position.Y - boxBody.HalfExtents.Y;
            float maxY = boxBody.Position.Y + boxBody.HalfExtents.Y;
            float minZ = boxBody.Position.Z - boxBody.HalfExtents.Z;
            float maxZ = boxBody.Position.Z + boxBody.HalfExtents.Z;
            float3 closestPoint = new float3(
                PrimitiveContactMath3D.Clamp(sphereBody.Position.X, minX, maxX),
                PrimitiveContactMath3D.Clamp(sphereBody.Position.Y, minY, maxY),
                PrimitiveContactMath3D.Clamp(sphereBody.Position.Z, minZ, maxZ));
            float3 delta = sphereBody.Position - closestPoint;
            float distanceSquared = float3.Dot(delta, delta);
            float radiusSquared = sphereBody.SphereRadius * sphereBody.SphereRadius;
            if (distanceSquared > radiusSquared) {
                collisionNormal = float3.Zero;
                penetration = 0f;
                return false;
            }

            if (distanceSquared > 0.0000001f) {
                float distance = (float)Math.Sqrt(distanceSquared);
                collisionNormal = delta / distance;
                penetration = sphereBody.SphereRadius - distance;
                return true;
            }

            float localX = sphereBody.Position.X - boxBody.Position.X;
            float localY = sphereBody.Position.Y - boxBody.Position.Y;
            float localZ = sphereBody.Position.Z - boxBody.Position.Z;
            float distanceToFaceX = boxBody.HalfExtents.X - Math.Abs(localX);
            float distanceToFaceY = boxBody.HalfExtents.Y - Math.Abs(localY);
            float distanceToFaceZ = boxBody.HalfExtents.Z - Math.Abs(localZ);
            if (distanceToFaceX <= distanceToFaceY && distanceToFaceX <= distanceToFaceZ) {
                collisionNormal = new float3(localX >= 0f ? 1f : -1f, 0f, 0f);
                penetration = sphereBody.SphereRadius + distanceToFaceX;
                return true;
            }
            if (distanceToFaceY <= distanceToFaceZ) {
                collisionNormal = new float3(0f, localY >= 0f ? 1f : -1f, 0f);
                penetration = sphereBody.SphereRadius + distanceToFaceY;
                return true;
            }

            collisionNormal = new float3(0f, 0f, localZ >= 0f ? 1f : -1f);
            penetration = sphereBody.SphereRadius + distanceToFaceZ;
            return true;
        }
    }
}
#endif
