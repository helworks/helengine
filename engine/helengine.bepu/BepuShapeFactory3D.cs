using BepuPhysics.Collidables;
using BepuUtilities.Memory;

namespace helengine {
    /// <summary>
    /// Builds official BEPU runtime shapes from Helengine collider components.
    /// </summary>
    public static class BepuShapeFactory3D {
        /// <summary>
        /// Builds one BEPU box shape from one authored box collider.
        /// </summary>
        /// <param name="collider">Authored box collider to translate.</param>
        /// <returns>BEPU box shape matching the authored collider.</returns>
        public static Box CreateBoxShape(BoxCollider3DComponent collider) {
            if (collider == null) {
                throw new ArgumentNullException(nameof(collider));
            }

            return new Box(collider.Size.X, collider.Size.Y, collider.Size.Z);
        }

        /// <summary>
        /// Builds one BEPU sphere shape from one authored sphere collider.
        /// </summary>
        /// <param name="collider">Authored sphere collider to translate.</param>
        /// <returns>BEPU sphere shape matching the authored collider.</returns>
        public static Sphere CreateSphereShape(SphereCollider3DComponent collider) {
            if (collider == null) {
                throw new ArgumentNullException(nameof(collider));
            }

            return new Sphere(collider.Radius);
        }

        /// <summary>
        /// Builds one BEPU static mesh shape from one cooked runtime payload.
        /// </summary>
        /// <param name="runtimeData">Cooked runtime payload emitted for the BEPU backend.</param>
        /// <param name="pool">Buffer pool that should own the deserialized mesh resources.</param>
        /// <returns>BEPU mesh shape matching the cooked payload.</returns>
        public static Mesh CreateStaticMeshShape(StaticMeshCollisionRuntimeData3D runtimeData, BufferPool pool) {
            if (runtimeData == null) {
                throw new ArgumentNullException(nameof(runtimeData));
            } else if (!string.Equals(runtimeData.FormatId, BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported BEPU static mesh payload format '{runtimeData.FormatId}'.");
            }

            using EngineBinaryReader reader = runtimeData.CreatePayloadReader(
                BepuStaticMeshCollisionCookProcessor3D.FormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatVersionValue);
            return BepuStaticMeshCollisionBinarySerializer.Read(reader, pool ?? throw new ArgumentNullException(nameof(pool)));
        }
    }
}
