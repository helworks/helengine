namespace helengine {
    /// <summary>
    /// Represents conservative world-space bounds using inclusive minimum and maximum coordinates.
    /// </summary>
    public readonly struct HelPhysicsAabb3D {
        /// <summary>
        /// Stores the inclusive lower coordinate on each world axis.
        /// </summary>
        public readonly PhysicsVector3 Minimum;

        /// <summary>
        /// Stores the inclusive upper coordinate on each world axis.
        /// </summary>
        public readonly PhysicsVector3 Maximum;

        /// <summary>
        /// Initializes axis-aligned bounds from inclusive minimum and maximum coordinates.
        /// </summary>
        /// <param name="minimum">Lower coordinate on every axis.</param>
        /// <param name="maximum">Upper coordinate on every axis.</param>
        /// <exception cref="ArgumentException">Thrown when a minimum component exceeds its corresponding maximum component.</exception>
        public HelPhysicsAabb3D(PhysicsVector3 minimum, PhysicsVector3 maximum) {
            if (minimum.X > maximum.X || minimum.Y > maximum.Y || minimum.Z > maximum.Z) {
                throw new ArgumentException("An AABB minimum cannot exceed its maximum on any axis.", nameof(minimum));
            }

            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>
        /// Determines whether this bound and another bound share any volume, face, edge, or corner.
        /// </summary>
        /// <param name="other">Bounds to compare against this instance.</param>
        /// <returns>True when the two inclusive bounds overlap on all three axes.</returns>
        public bool Overlaps(HelPhysicsAabb3D other) {
            return Minimum.X <= other.Maximum.X && Maximum.X >= other.Minimum.X
                && Minimum.Y <= other.Maximum.Y && Maximum.Y >= other.Minimum.Y
                && Minimum.Z <= other.Maximum.Z && Maximum.Z >= other.Minimum.Z;
        }
    }
}
