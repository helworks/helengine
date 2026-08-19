using BepuPhysics.Collidables;
using BepuUtilities.Memory;

namespace helengine {
    /// <summary>
    /// Builds official BEPU runtime shapes from Helengine collider components.
    /// </summary>
    public static class BepuShapeFactory3D {
        /// <summary>
        /// Builds one BEPU box shape from one authored box collider and the owning entity's world scale, matching the HelPhysics backend's effective-size convention.
        /// </summary>
        /// <param name="collider">Authored box collider to translate.</param>
        /// <param name="worldScale">Owning entity's composed world scale.</param>
        /// <returns>BEPU box shape matching the authored collider scaled per axis.</returns>
        public static Box CreateBoxShape(BoxCollider3DComponent collider, float3 worldScale) {
            if (collider == null) {
                throw new ArgumentNullException(nameof(collider));
            }

            return new Box(
                Math.Abs(collider.Size.X * worldScale.X),
                Math.Abs(collider.Size.Y * worldScale.Y),
                Math.Abs(collider.Size.Z * worldScale.Z));
        }

        /// <summary>
        /// Builds one BEPU sphere shape from one authored sphere collider and the owning entity's world scale, using the largest scale axis like the trigger overlap path.
        /// </summary>
        /// <param name="collider">Authored sphere collider to translate.</param>
        /// <param name="worldScale">Owning entity's composed world scale.</param>
        /// <returns>BEPU sphere shape matching the authored collider scaled by the largest axis.</returns>
        public static Sphere CreateSphereShape(SphereCollider3DComponent collider, float3 worldScale) {
            if (collider == null) {
                throw new ArgumentNullException(nameof(collider));
            }

            float scaleFactor = Math.Max(Math.Abs(worldScale.X), Math.Max(Math.Abs(worldScale.Y), Math.Abs(worldScale.Z)));
            return new Sphere(collider.Radius * scaleFactor);
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
