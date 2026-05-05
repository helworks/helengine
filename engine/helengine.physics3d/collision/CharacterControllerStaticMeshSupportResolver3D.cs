#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_STATIC_MESH_SUPPORT
namespace helengine {
    /// <summary>
    /// Resolves walkable character-controller support contributed by cooked static-mesh triangles.
    /// </summary>
    public static class CharacterControllerStaticMeshSupportResolver3D {
        /// <summary>
        /// Resolves the highest walkable static-mesh support height beneath the supplied controller footprint.
        /// </summary>
        /// <param name="staticMeshStates">Cooked static meshes that can contribute support.</param>
        /// <param name="centerX">Controller footprint center X coordinate.</param>
        /// <param name="centerY">Controller center Y coordinate.</param>
        /// <param name="centerZ">Controller footprint center Z coordinate.</param>
        /// <param name="halfExtents">Controller half extents.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="supportHeight">Resolved highest support height.</param>
        /// <returns>True when at least one walkable static-mesh support surface was found.</returns>
        public static bool TryResolveSupportHeight(
            IReadOnlyList<StaticMeshBodyState3D> staticMeshStates,
            float centerX,
            float centerY,
            float centerZ,
            float3 halfExtents,
            double maximumSlopeDegrees,
            out float supportHeight) {
            if (staticMeshStates == null) {
                throw new ArgumentNullException(nameof(staticMeshStates));
            }

            bool foundSupport = false;
            supportHeight = 0f;
            float sampleOffsetX = halfExtents.X * 0.8f;
            float sampleOffsetZ = halfExtents.Z * 0.8f;
            float maximumSupportHeight = centerY + halfExtents.Y + 0.05f;

            AccumulateSupportHeight(staticMeshStates, centerX, centerZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight);
            AccumulateSupportHeight(staticMeshStates, centerX - sampleOffsetX, centerZ - sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight);
            AccumulateSupportHeight(staticMeshStates, centerX - sampleOffsetX, centerZ + sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight);
            AccumulateSupportHeight(staticMeshStates, centerX + sampleOffsetX, centerZ - sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight);
            AccumulateSupportHeight(staticMeshStates, centerX + sampleOffsetX, centerZ + sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight);
            return foundSupport;
        }

        /// <summary>
        /// Resolves the highest support height available at one footprint sample point from cooked static meshes.
        /// </summary>
        /// <param name="staticMeshStates">Cooked static meshes that can contribute support.</param>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="maximumSupportHeight">Maximum support height treated as below the controller volume.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="foundSupport">Current support-found flag.</param>
        /// <param name="supportHeight">Current highest support height.</param>
        static void AccumulateSupportHeight(
            IReadOnlyList<StaticMeshBodyState3D> staticMeshStates,
            float sampleX,
            float sampleZ,
            float maximumSupportHeight,
            double maximumSlopeDegrees,
            ref bool foundSupport,
            ref float supportHeight) {
            for (int index = 0; index < staticMeshStates.Count; index++) {
                StaticMeshBodyState3D meshState = staticMeshStates[index];
                if (meshState.MeshCollider.IsTrigger) {
                    continue;
                }
                if (!TryGetStaticMeshSupportHeight(meshState, sampleX, sampleZ, maximumSlopeDegrees, out float meshSupportHeight)) {
                    continue;
                }
                if (meshSupportHeight > maximumSupportHeight) {
                    continue;
                }

                if (!foundSupport || meshSupportHeight > supportHeight) {
                    foundSupport = true;
                    supportHeight = meshSupportHeight;
                }
            }
        }

        /// <summary>
        /// Resolves the highest walkable support height contributed by one cooked static mesh at the supplied footprint sample point.
        /// </summary>
        /// <param name="meshState">Static mesh state whose triangles should be tested.</param>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="supportHeight">Resolved highest support height.</param>
        /// <returns>True when the sample point projects onto a walkable triangle.</returns>
        static bool TryGetStaticMeshSupportHeight(StaticMeshBodyState3D meshState, float sampleX, float sampleZ, double maximumSlopeDegrees, out float supportHeight) {
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            bool foundSupport = false;
            supportHeight = 0f;
            float3[] worldVertices = meshState.WorldVertices;
            int[] indices = meshState.MeshCollider.CollisionData.Indices;
            for (int triangleIndex = 0; triangleIndex < indices.Length; triangleIndex += 3) {
                float3 a = worldVertices[indices[triangleIndex]];
                float3 b = worldVertices[indices[triangleIndex + 1]];
                float3 c = worldVertices[indices[triangleIndex + 2]];
                if (!TryGetTriangleSupportHeight(a, b, c, sampleX, sampleZ, maximumSlopeDegrees, out float triangleSupportHeight)) {
                    continue;
                }

                if (!foundSupport || triangleSupportHeight > supportHeight) {
                    foundSupport = true;
                    supportHeight = triangleSupportHeight;
                }
            }

            return foundSupport;
        }

        /// <summary>
        /// Resolves the support height contributed by one walkable triangle at the supplied footprint sample point.
        /// </summary>
        /// <param name="a">First world-space triangle vertex.</param>
        /// <param name="b">Second world-space triangle vertex.</param>
        /// <param name="c">Third world-space triangle vertex.</param>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="supportHeight">Resolved support height.</param>
        /// <returns>True when the sample point projects onto the walkable triangle.</returns>
        static bool TryGetTriangleSupportHeight(float3 a, float3 b, float3 c, float sampleX, float sampleZ, double maximumSlopeDegrees, out float supportHeight) {
            float3 normal = StaticMeshTriangleMath3D.GetTriangleUnitNormal(a, b, c);
            if (normal == float3.Zero) {
                supportHeight = 0f;
                return false;
            }
            if (normal.Y <= 0.0001f) {
                supportHeight = 0f;
                return false;
            }
            if (normal.Y < CharacterControllerSupportMath3D.CalculateMinimumWalkableSurfaceDot(maximumSlopeDegrees)) {
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

            float planeNumerator = (normal.X * (sampleX - a.X)) + (normal.Z * (sampleZ - a.Z));
            supportHeight = a.Y - (planeNumerator / normal.Y);
            return true;
        }
    }
}
#endif
