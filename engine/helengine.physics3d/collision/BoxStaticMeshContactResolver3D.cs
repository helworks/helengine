#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_STATIC_MESH_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves dynamic axis-aligned box contact against cooked static mesh triangles.
    /// </summary>
    public static class BoxStaticMeshContactResolver3D {
        /// <summary>
        /// Finds the deepest walkable box-static-mesh support overlap for one dynamic box body.
        /// </summary>
        /// <param name="boxBody">Dynamic box body being tested.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the static mesh surface.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the box overlaps the cooked mesh from above.</returns>
        public static bool TryResolveContact(BodyState3D boxBody, StaticMeshBodyState3D meshState, out float3 collisionNormal, out float penetration) {
            if (boxBody == null) {
                throw new ArgumentNullException(nameof(boxBody));
            }
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            return TryResolveContact(boxBody.Position, boxBody.HalfExtents, meshState, out collisionNormal, out penetration);
        }

        /// <summary>
        /// Finds the deepest walkable box-static-mesh support overlap for one arbitrary axis-aligned box volume.
        /// </summary>
        /// <param name="boxCenter">Box center being tested.</param>
        /// <param name="boxHalfExtents">Box half extents being tested.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the static mesh surface.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        /// <returns>True when the box overlaps the cooked mesh from above.</returns>
        public static bool TryResolveContact(float3 boxCenter, float3 boxHalfExtents, StaticMeshBodyState3D meshState, out float3 collisionNormal, out float penetration) {
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            bool foundSupport = false;
            float bestSupportHeight = 0f;
            collisionNormal = float3.Zero;
            penetration = 0f;
            float maximumSupportHeight = boxCenter.Y + boxHalfExtents.Y + 0.05f;
            float sampleOffsetX = boxHalfExtents.X * 0.8f;
            float sampleOffsetZ = boxHalfExtents.Z * 0.8f;

            AccumulateSupportHeight(boxCenter.X, boxCenter.Z, maximumSupportHeight, meshState, ref foundSupport, ref bestSupportHeight, ref collisionNormal);
            AccumulateSupportHeight(boxCenter.X - sampleOffsetX, boxCenter.Z - sampleOffsetZ, maximumSupportHeight, meshState, ref foundSupport, ref bestSupportHeight, ref collisionNormal);
            AccumulateSupportHeight(boxCenter.X - sampleOffsetX, boxCenter.Z + sampleOffsetZ, maximumSupportHeight, meshState, ref foundSupport, ref bestSupportHeight, ref collisionNormal);
            AccumulateSupportHeight(boxCenter.X + sampleOffsetX, boxCenter.Z - sampleOffsetZ, maximumSupportHeight, meshState, ref foundSupport, ref bestSupportHeight, ref collisionNormal);
            AccumulateSupportHeight(boxCenter.X + sampleOffsetX, boxCenter.Z + sampleOffsetZ, maximumSupportHeight, meshState, ref foundSupport, ref bestSupportHeight, ref collisionNormal);

            if (!foundSupport) {
                return false;
            }

            float bottom = boxCenter.Y - boxHalfExtents.Y;
            penetration = bestSupportHeight - bottom;
            if (penetration <= 0f) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the highest support height available at one footprint sample point on the cooked mesh.
        /// </summary>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="maximumSupportHeight">Maximum support height considered below the box volume.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <param name="foundSupport">Current support-found flag.</param>
        /// <param name="supportHeight">Current highest support height.</param>
        /// <param name="supportNormal">Current highest support normal.</param>
        static void AccumulateSupportHeight(
            float sampleX,
            float sampleZ,
            float maximumSupportHeight,
            StaticMeshBodyState3D meshState,
            ref bool foundSupport,
            ref float supportHeight,
            ref float3 supportNormal) {
            float3[] vertices = meshState.WorldVertices;
            int[] indices = meshState.MeshCollider.CollisionData.Indices;
            for (int triangleIndex = 0; triangleIndex < indices.Length; triangleIndex += 3) {
                float3 a = vertices[indices[triangleIndex]];
                float3 b = vertices[indices[triangleIndex + 1]];
                float3 c = vertices[indices[triangleIndex + 2]];
                if (!TryGetTriangleSupportHeight(a, b, c, sampleX, sampleZ, out float triangleSupportHeight, out float3 triangleNormal)) {
                    continue;
                }
                if (triangleSupportHeight > maximumSupportHeight) {
                    continue;
                }

                if (!foundSupport || triangleSupportHeight > supportHeight) {
                    foundSupport = true;
                    supportHeight = triangleSupportHeight;
                    supportNormal = triangleNormal;
                }
            }
        }

        /// <summary>
        /// Resolves the support height contributed by one upward-facing triangle at the supplied footprint sample point.
        /// </summary>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="supportHeight">Resolved support height.</param>
        /// <param name="supportNormal">Resolved upward-facing support normal.</param>
        /// <returns>True when the sample projects onto the triangle footprint and the triangle faces upward.</returns>
        static bool TryGetTriangleSupportHeight(float3 a, float3 b, float3 c, float sampleX, float sampleZ, out float supportHeight, out float3 supportNormal) {
            supportNormal = StaticMeshTriangleMath3D.GetTriangleUnitNormal(a, b, c);
            if (supportNormal == float3.Zero || supportNormal.Y <= 0.0001f) {
                supportHeight = 0f;
                return false;
            }

            float2 point = new float2(sampleX, sampleZ);
            float2 a2 = new float2(a.X, a.Z);
            float2 b2 = new float2(b.X, b.Z);
            float2 c2 = new float2(c.X, c.Z);
            if (!StaticMeshTriangleMath3D.IsPointInsideProjectedTriangle(point, a2, b2, c2)) {
                supportHeight = 0f;
                return false;
            }

            float planeNumerator = (supportNormal.X * (sampleX - a.X)) + (supportNormal.Z * (sampleZ - a.Z));
            supportHeight = a.Y - (planeNumerator / supportNormal.Y);
            return true;
        }
    }
}
#endif
