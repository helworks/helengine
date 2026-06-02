using BepuPhysics.Collidables;

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
    }
}
