#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_STATIC_MESH_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves dynamic capsule contact against cooked static mesh triangles.
    /// </summary>
    public static class CapsuleStaticMeshContactResolver3D {
        /// <summary>
        /// Finds the deepest capsule-static-mesh overlap for one dynamic capsule body.
        /// </summary>
        /// <param name="capsuleBody">Dynamic capsule body being tested.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the static mesh surface.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the capsule overlaps the cooked mesh.</returns>
        public static bool TryResolveContact(BodyState3D capsuleBody, StaticMeshBodyState3D meshState, out float3 collisionNormal, out float penetration) {
            if (capsuleBody == null) {
                throw new ArgumentNullException(nameof(capsuleBody));
            }
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            bool foundContact = false;
            collisionNormal = float3.Zero;
            penetration = 0f;
            float3[] worldVertices = meshState.WorldVertices;
            int[] indices = meshState.MeshCollider.CollisionData.Indices;
            for (int triangleIndex = 0; triangleIndex < indices.Length; triangleIndex += 3) {
                float3 a = worldVertices[indices[triangleIndex]];
                float3 b = worldVertices[indices[triangleIndex + 1]];
                float3 c = worldVertices[indices[triangleIndex + 2]];
                if (!TryGetCapsuleTriangleContact(capsuleBody, a, b, c, out float3 triangleNormal, out float trianglePenetration)) {
                    continue;
                }

                if (!foundContact || trianglePenetration > penetration) {
                    foundContact = true;
                    collisionNormal = triangleNormal;
                    penetration = trianglePenetration;
                }
            }

            return foundContact;
        }

        /// <summary>
        /// Resolves capsule-triangle contact information when the shapes overlap.
        /// </summary>
        /// <param name="capsuleBody">Capsule body state.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the triangle.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the capsule overlaps the triangle.</returns>
        static bool TryGetCapsuleTriangleContact(BodyState3D capsuleBody, float3 a, float3 b, float3 c, out float3 collisionNormal, out float penetration) {
            float3 capsulePoint;
            float3 trianglePoint;
            GetClosestPointsBetweenVerticalCapsuleSegmentAndTriangle(capsuleBody, a, b, c, out capsulePoint, out trianglePoint);
            float3 delta = capsulePoint - trianglePoint;
            float distanceSquared = float3.Dot(delta, delta);
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

            collisionNormal = StaticMeshTriangleMath3D.GetTriangleUnitNormal(a, b, c);
            if (collisionNormal == float3.Zero) {
                penetration = 0f;
                return false;
            }

            penetration = capsuleBody.SphereRadius;
            return true;
        }

        /// <summary>
        /// Resolves the closest points between one vertical capsule segment and one triangle.
        /// </summary>
        /// <param name="capsuleBody">Capsule body state whose center segment is tested.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="capsulePoint">Closest point on the capsule center segment.</param>
        /// <param name="trianglePoint">Closest point on the triangle.</param>
        static void GetClosestPointsBetweenVerticalCapsuleSegmentAndTriangle(BodyState3D capsuleBody, float3 a, float3 b, float3 c, out float3 capsulePoint, out float3 trianglePoint) {
            if (capsuleBody == null) {
                throw new ArgumentNullException(nameof(capsuleBody));
            }

            float segmentMinimumY = capsuleBody.Position.Y - capsuleBody.CapsuleSegmentHalfLength;
            float segmentMaximumY = capsuleBody.Position.Y + capsuleBody.CapsuleSegmentHalfLength;
            float bestDistanceSquared = float.MaxValue;
            capsulePoint = new float3(capsuleBody.Position.X, segmentMinimumY, capsuleBody.Position.Z);
            trianglePoint = StaticMeshTriangleMath3D.GetClosestPointOnTriangle(capsulePoint, a, b, c);
            UpdateClosestSegmentTriangleCandidate(capsuleBody, segmentMinimumY, a, b, c, ref bestDistanceSquared, ref capsulePoint, ref trianglePoint);
            UpdateClosestSegmentTriangleCandidate(capsuleBody, segmentMaximumY, a, b, c, ref bestDistanceSquared, ref capsulePoint, ref trianglePoint);
            UpdateClosestSegmentTriangleCandidate(capsuleBody, StaticMeshTriangleMath3D.Clamp(a.Y, segmentMinimumY, segmentMaximumY), a, b, c, ref bestDistanceSquared, ref capsulePoint, ref trianglePoint);
            UpdateClosestSegmentTriangleCandidate(capsuleBody, StaticMeshTriangleMath3D.Clamp(b.Y, segmentMinimumY, segmentMaximumY), a, b, c, ref bestDistanceSquared, ref capsulePoint, ref trianglePoint);
            UpdateClosestSegmentTriangleCandidate(capsuleBody, StaticMeshTriangleMath3D.Clamp(c.Y, segmentMinimumY, segmentMaximumY), a, b, c, ref bestDistanceSquared, ref capsulePoint, ref trianglePoint);

            if (TryGetVerticalSegmentTriangleProjectionCandidate(capsuleBody, segmentMinimumY, segmentMaximumY, a, b, c, out float projectedY)) {
                UpdateClosestSegmentTriangleCandidate(capsuleBody, projectedY, a, b, c, ref bestDistanceSquared, ref capsulePoint, ref trianglePoint);
            }
        }

        /// <summary>
        /// Updates the closest segment-triangle candidate using one sampled segment Y coordinate.
        /// </summary>
        /// <param name="capsuleBody">Capsule body state whose center segment is tested.</param>
        /// <param name="segmentY">Sampled Y coordinate on the capsule center segment.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="bestDistanceSquared">Current best squared distance.</param>
        /// <param name="bestCapsulePoint">Current best point on the capsule segment.</param>
        /// <param name="bestTrianglePoint">Current best point on the triangle.</param>
        static void UpdateClosestSegmentTriangleCandidate(
            BodyState3D capsuleBody,
            float segmentY,
            float3 a,
            float3 b,
            float3 c,
            ref float bestDistanceSquared,
            ref float3 bestCapsulePoint,
            ref float3 bestTrianglePoint) {
            float3 candidateCapsulePoint = new float3(capsuleBody.Position.X, segmentY, capsuleBody.Position.Z);
            float3 candidateTrianglePoint = StaticMeshTriangleMath3D.GetClosestPointOnTriangle(candidateCapsulePoint, a, b, c);
            float3 delta = candidateCapsulePoint - candidateTrianglePoint;
            float distanceSquared = float3.Dot(delta, delta);
            if (distanceSquared < bestDistanceSquared) {
                bestDistanceSquared = distanceSquared;
                bestCapsulePoint = candidateCapsulePoint;
                bestTrianglePoint = candidateTrianglePoint;
            }
        }

        /// <summary>
        /// Tries to project the vertical capsule center line onto one triangle plane when the XZ projection lies inside the triangle.
        /// </summary>
        /// <param name="capsuleBody">Capsule body state whose center segment is tested.</param>
        /// <param name="segmentMinimumY">Minimum segment Y coordinate.</param>
        /// <param name="segmentMaximumY">Maximum segment Y coordinate.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="projectedY">Projected plane height on the vertical line when valid.</param>
        /// <returns>True when the vertical line projects through the triangle plane inside the triangle footprint.</returns>
        static bool TryGetVerticalSegmentTriangleProjectionCandidate(BodyState3D capsuleBody, float segmentMinimumY, float segmentMaximumY, float3 a, float3 b, float3 c, out float projectedY) {
            projectedY = 0f;
            float3 normal = StaticMeshTriangleMath3D.GetTriangleUnitNormal(a, b, c);
            if (normal == float3.Zero || Math.Abs(normal.Y) <= 0.0001f) {
                return false;
            }

            float2 point = new float2(capsuleBody.Position.X, capsuleBody.Position.Z);
            float2 a2 = new float2(a.X, a.Z);
            float2 b2 = new float2(b.X, b.Z);
            float2 c2 = new float2(c.X, c.Z);
            if (!StaticMeshTriangleMath3D.IsPointInsideProjectedTriangle(point, a2, b2, c2)) {
                return false;
            }

            float planeNumerator = (normal.X * (capsuleBody.Position.X - a.X)) + (normal.Z * (capsuleBody.Position.Z - a.Z));
            projectedY = a.Y - (planeNumerator / normal.Y);
            projectedY = StaticMeshTriangleMath3D.Clamp(projectedY, segmentMinimumY, segmentMaximumY);
            return true;
        }
    }
}
#endif
