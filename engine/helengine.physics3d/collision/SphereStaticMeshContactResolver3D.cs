#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_STATIC_MESH_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves dynamic sphere contact against cooked static mesh triangles.
    /// </summary>
    public static class SphereStaticMeshContactResolver3D {
        /// <summary>
        /// Finds the deepest sphere-static-mesh overlap for one dynamic sphere body.
        /// </summary>
        /// <param name="sphereBody">Dynamic sphere body being tested.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the static mesh surface.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the sphere overlaps the cooked mesh.</returns>
        public static bool TryResolveContact(BodyState3D sphereBody, StaticMeshBodyState3D meshState, out float3 collisionNormal, out float penetration) {
            if (sphereBody == null) {
                throw new ArgumentNullException(nameof(sphereBody));
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
                if (!TryGetSphereTriangleContact(sphereBody.Position, sphereBody.SphereRadius, a, b, c, out float3 triangleNormal, out float trianglePenetration)) {
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
        /// Resolves sphere-triangle contact information when the shapes overlap.
        /// </summary>
        /// <param name="sphereCenter">World-space sphere center.</param>
        /// <param name="sphereRadius">Sphere radius.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the triangle.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the sphere overlaps the triangle.</returns>
        static bool TryGetSphereTriangleContact(float3 sphereCenter, float sphereRadius, float3 a, float3 b, float3 c, out float3 collisionNormal, out float penetration) {
            float3 closestPoint = StaticMeshTriangleMath3D.GetClosestPointOnTriangle(sphereCenter, a, b, c);
            float3 delta = sphereCenter - closestPoint;
            float distanceSquared = float3.Dot(delta, delta);
            float radiusSquared = sphereRadius * sphereRadius;
            if (distanceSquared > radiusSquared) {
                collisionNormal = float3.Zero;
                penetration = 0f;
                return false;
            }

            if (distanceSquared > 0.0000001f) {
                float distance = (float)Math.Sqrt(distanceSquared);
                collisionNormal = delta / distance;
                penetration = sphereRadius - distance;
                return true;
            }

            collisionNormal = StaticMeshTriangleMath3D.GetTriangleUnitNormal(a, b, c);
            if (collisionNormal == float3.Zero) {
                penetration = 0f;
                return false;
            }

            penetration = sphereRadius;
            return true;
        }
    }
}
#endif
