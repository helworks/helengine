namespace helengine {
    /// <summary>
    /// Represents a sphere centered at its local origin with a strictly positive radius.
    /// </summary>
    public readonly struct HelPhysicsSphereShape3D {
        /// <summary>
        /// Stores the sphere radius in local units.
        /// </summary>
        public readonly PhysicsScalar Radius;

        /// <summary>
        /// Initializes a sphere from a strictly positive radius.
        /// </summary>
        /// <param name="radius">Positive local-space radius.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not strictly positive.</exception>
        public HelPhysicsSphereShape3D(PhysicsScalar radius) {
            if (radius <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere radius must be strictly positive.");
            }

            Radius = radius;
        }
    }
}
