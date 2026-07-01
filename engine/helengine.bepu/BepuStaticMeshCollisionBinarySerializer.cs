using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using System.Numerics;

namespace helengine {
    /// <summary>
    /// Serializes and deserializes BEPU static-mesh collision payloads through Helengine-owned endian-aware readers and writers.
    /// </summary>
    public static class BepuStaticMeshCollisionBinarySerializer {
        /// <summary>
        /// Writes one BEPU-compatible static-mesh payload using the supplied authored collision data.
        /// </summary>
        /// <param name="writer">Endian-aware writer owned by Helengine.</param>
        /// <param name="collisionData">Generic collision data to encode.</param>
        public static void Write(EngineBinaryWriter writer, StaticMeshCollisionData3D collisionData) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (collisionData == null) {
                throw new ArgumentNullException(nameof(collisionData));
            }

            writer.WriteInt32(collisionData.TriangleCount);
            writer.WriteFloat3(new float3(1f, 1f, 1f));
            for (int triangleIndex = 0; triangleIndex < collisionData.TriangleCount; triangleIndex++) {
                int indexOffset = triangleIndex * 3;
                writer.WriteFloat3(collisionData.Vertices[collisionData.Indices[indexOffset]]);
                writer.WriteFloat3(collisionData.Vertices[collisionData.Indices[indexOffset + 1]]);
                writer.WriteFloat3(collisionData.Vertices[collisionData.Indices[indexOffset + 2]]);
            }
        }

        /// <summary>
        /// Reconstructs one BEPU mesh from one Helengine-owned serialized payload body.
        /// </summary>
        /// <param name="reader">Endian-aware reader positioned at the payload body.</param>
        /// <param name="pool">Buffer pool that should own the reconstructed mesh resources.</param>
        /// <returns>Reconstructed BEPU mesh.</returns>
        public static Mesh Read(EngineBinaryReader reader, BufferPool pool) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (pool == null) {
                throw new ArgumentNullException(nameof(pool));
            }

            int triangleCount = reader.ReadInt32();
            if (triangleCount <= 0) {
                throw new InvalidOperationException("Static mesh payload must contain at least one triangle.");
            }

            float3 scaleValue = reader.ReadFloat3();
            pool.Take<Triangle>(triangleCount, out Buffer<Triangle> triangles);
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++) {
                triangles[triangleIndex] = new Triangle(
                    CreateVector3(reader.ReadFloat3()),
                    CreateVector3(reader.ReadFloat3()),
                    CreateVector3(reader.ReadFloat3()));
            }

            // The sweep-build path avoids the heavier binned tree build that currently destabilizes
            // generated native runtimes on PSP-sized static mesh showcase content.
            return Mesh.CreateWithSweepBuild(triangles, CreateVector3(scaleValue), pool);
        }

        /// <summary>
        /// Converts one Helengine float3 into the numerics vector type consumed by BEPU.
        /// </summary>
        /// <param name="value">Float3 value to convert.</param>
        /// <returns>Converted numerics vector.</returns>
        static Vector3 CreateVector3(float3 value) {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }
}
