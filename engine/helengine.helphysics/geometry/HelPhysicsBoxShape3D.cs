namespace helengine {
    /// <summary>
    /// Represents a box centered at its local origin with strictly positive half extents on every local axis.
    /// </summary>
    public readonly struct HelPhysicsBoxShape3D {
        /// <summary>
        /// Stores the positive distance from the local origin to each pair of opposite box faces.
        /// </summary>
        public readonly PhysicsVector3 HalfExtents;

        /// <summary>
        /// Initializes a centered box from positive distances to its local faces.
        /// </summary>
        /// <param name="halfExtents">Strictly positive local X, Y, and Z half extents.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any supplied half extent is zero or negative.</exception>
        public HelPhysicsBoxShape3D(PhysicsVector3 halfExtents) {
            ValidateHalfExtents(halfExtents);

            HalfExtents = halfExtents;
        }

        /// <summary>
        /// Validates that a box can occupy non-degenerate volume on every local axis.
        /// </summary>
        /// <param name="halfExtents">Candidate local distances from the box center to its faces.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any component is not strictly positive.</exception>
        static void ValidateHalfExtents(PhysicsVector3 halfExtents) {
            if (halfExtents.X <= PhysicsScalar.Zero || halfExtents.Y <= PhysicsScalar.Zero || halfExtents.Z <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), "Box half extents must be strictly positive on every axis.");
            }
        }
    }
}
