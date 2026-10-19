#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_BOX_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves capsule-box contact information.
    /// </summary>
    public static class CapsuleBoxContactResolver3D {
        /// <summary>
        /// Finds the collision normal and penetration depth for one overlapping capsule-box pair.
        /// </summary>
        /// <param name="capsuleBody">Capsule body state.</param>
        /// <param name="boxBody">Box body state.</param>
        /// <param name="collisionNormal">Unit normal pointing from the box toward the capsule.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the capsule overlaps the box.</returns>
        public static bool TryResolveContact(BodyState3D capsuleBody, BodyState3D boxBody, out float3 collisionNormal, out float penetration) {
            if (capsuleBody == null) {
                throw new ArgumentNullException(nameof(capsuleBody));
            }
            if (boxBody == null) {
                throw new ArgumentNullException(nameof(boxBody));
            }

            float segmentMinimumY = (float)(capsuleBody.Position.Y - capsuleBody.CapsuleSegmentHalfLength);
            float segmentMaximumY = (float)(capsuleBody.Position.Y + capsuleBody.CapsuleSegmentHalfLength);
            float minX = (float)(boxBody.Position.X - boxBody.HalfExtents.X);
            float maxX = (float)(boxBody.Position.X + boxBody.HalfExtents.X);
            float minY = (float)(boxBody.Position.Y - boxBody.HalfExtents.Y);
            float maxY = (float)(boxBody.Position.Y + boxBody.HalfExtents.Y);
            float minZ = (float)(boxBody.Position.Z - boxBody.HalfExtents.Z);
            float maxZ = (float)(boxBody.Position.Z + boxBody.HalfExtents.Z);
            float closestSegmentY = PrimitiveContactMath3D.Clamp((float)(boxBody.Position.Y), segmentMinimumY, segmentMaximumY);
            float closestBoxY = PrimitiveContactMath3D.Clamp(closestSegmentY, minY, maxY);
            float3 closestSegmentPoint = new float3(capsuleBody.Position.X, closestSegmentY, capsuleBody.Position.Z);
            float3 closestBoxPoint = new float3(
                PrimitiveContactMath3D.Clamp((float)(capsuleBody.Position.X), minX, maxX),
                closestBoxY,
                PrimitiveContactMath3D.Clamp((float)(capsuleBody.Position.Z), minZ, maxZ));
            float3 delta = closestSegmentPoint - closestBoxPoint;
            float distanceSquared = (float)(float3.Dot(delta, delta));
            float radiusSquared = capsuleBody.SphereRadius * capsuleBody.SphereRadius;
            if (distanceSquared > radiusSquared) {
                collisionNormal = float3.Zero;
                penetration = 0f;
                return false;
            }

            if (distanceSquared > 0.0000001f) {
                float distance = (float)Math.Sqrt(distanceSquared);
                collisionNormal = delta / distance;
                penetration = capsuleBody.SphereRadius - distance;
                return true;
            }

            float localX = (float)(closestSegmentPoint.X - boxBody.Position.X);
            float localY = (float)(closestSegmentPoint.Y - boxBody.Position.Y);
            float localZ = (float)(closestSegmentPoint.Z - boxBody.Position.Z);
            float distanceToFaceX = (float)(boxBody.HalfExtents.X - Math.Abs(localX));
            float distanceToFaceY = (float)(boxBody.HalfExtents.Y - Math.Abs(localY));
            float distanceToFaceZ = (float)(boxBody.HalfExtents.Z - Math.Abs(localZ));
            if (distanceToFaceX <= distanceToFaceY && distanceToFaceX <= distanceToFaceZ) {
                collisionNormal = new float3(localX >= 0f ? 1f : -1f, 0f, 0f);
                penetration = capsuleBody.SphereRadius + distanceToFaceX;
                return true;
            }
            if (distanceToFaceY <= distanceToFaceZ) {
                collisionNormal = new float3(0f, localY >= 0f ? 1f : -1f, 0f);
                penetration = capsuleBody.SphereRadius + distanceToFaceY;
                return true;
            }

            collisionNormal = new float3(0f, 0f, localZ >= 0f ? 1f : -1f);
            penetration = capsuleBody.SphereRadius + distanceToFaceZ;
            return true;
        }
    }
}
#endif
