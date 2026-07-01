using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using System.Numerics;

namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies BEPU static-mesh cook payloads round-trip through the upstream mesh serializer.
    /// </summary>
    public sealed class BepuStaticMeshCollisionCookProcessorTests {
        /// <summary>
        /// Ensures the BEPU cook processor emits a payload that can be deserialized back into one BEPU mesh.
        /// </summary>
        [Fact]
        public void Cook_WhenStaticMeshCollisionDataIsValid_ProducesRoundTrippableMeshPayload() {
            BepuStaticMeshCollisionCookProcessor3D processor = new BepuStaticMeshCollisionCookProcessor3D();
            StaticMeshCollisionData3D collisionData = new StaticMeshCollisionData3D(
                [
                    new float3(-1f, 0f, -1f),
                    new float3(1f, 0f, -1f),
                    new float3(-1f, 0f, 1f)
                ],
                [0, 1, 2]);

            StaticMeshCollisionRuntimeData3D runtimeData = StaticMeshCollisionRuntimeData3D.Create(
                processor.FormatId,
                processor.BinaryFormatId,
                processor.BinaryFormatVersion,
                EngineBinaryEndianness.BigEndian,
                writer => processor.WritePayload(writer, collisionData));

            Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, runtimeData.FormatId);

            BufferPool pool = new BufferPool();
            try {
                Mesh mesh = BepuShapeFactory3D.CreateStaticMeshShape(runtimeData, pool);

                Assert.Equal(1, mesh.Triangles.Length);
                Assert.Equal(new Vector3(-1f, 0f, -1f), mesh.Triangles[0].A);
                Assert.Equal(new Vector3(1f, 0f, -1f), mesh.Triangles[0].B);
                Assert.Equal(new Vector3(-1f, 0f, 1f), mesh.Triangles[0].C);
                Assert.Equal(new Vector3(1f, 1f, 1f), mesh.Scale);

                mesh.Dispose(pool);
            } finally {
                pool.Clear();
            }
        }

        /// <summary>
        /// Ensures showcase-sized cooked triangle payloads still rebuild into one BEPU mesh with the full triangle count.
        /// </summary>
        [Fact]
        public void Cook_WhenStaticMeshCollisionDataContainsShowcaseSizedTriangleSoup_ProducesMeshWithExpectedTriangleCount() {
            BepuStaticMeshCollisionCookProcessor3D processor = new BepuStaticMeshCollisionCookProcessor3D();
            StaticMeshCollisionData3D collisionData = CreateTriangleGridCollisionData(96);

            StaticMeshCollisionRuntimeData3D runtimeData = StaticMeshCollisionRuntimeData3D.Create(
                processor.FormatId,
                processor.BinaryFormatId,
                processor.BinaryFormatVersion,
                EngineBinaryEndianness.LittleEndian,
                writer => processor.WritePayload(writer, collisionData));

            BufferPool pool = new BufferPool();
            try {
                Mesh mesh = BepuShapeFactory3D.CreateStaticMeshShape(runtimeData, pool);

                Assert.Equal(96, mesh.Triangles.Length);
                Assert.Equal(new Vector3(1f, 1f, 1f), mesh.Scale);

                mesh.Dispose(pool);
            } finally {
                pool.Clear();
            }
        }

        /// <summary>
        /// Builds one deterministic flat triangle grid used to exercise larger cooked static-mesh payloads.
        /// </summary>
        /// <param name="triangleCount">Triangle count to generate.</param>
        /// <returns>Generated static-mesh collision data.</returns>
        static StaticMeshCollisionData3D CreateTriangleGridCollisionData(int triangleCount) {
            if (triangleCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(triangleCount), "Triangle count must be greater than zero.");
            }

            List<float3> vertices = new List<float3>(triangleCount * 3);
            List<int> indices = new List<int>(triangleCount * 3);
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++) {
                float x = triangleIndex % 12;
                float z = triangleIndex / 12;
                int vertexStart = vertices.Count;
                vertices.Add(new float3(x, 0f, z));
                vertices.Add(new float3(x + 0.5f, 0f, z));
                vertices.Add(new float3(x, 0f, z + 0.5f));
                indices.Add(vertexStart);
                indices.Add(vertexStart + 1);
                indices.Add(vertexStart + 2);
            }

            return new StaticMeshCollisionData3D(vertices.ToArray(), indices.ToArray());
        }
    }
}
