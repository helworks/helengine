namespace helengine {
    /// <summary>
    /// Identifies one uniform-grid cell in three-dimensional broadphase space.
    /// </summary>
    public readonly struct GridCellKey3D : IEquatable<GridCellKey3D> {
        /// <summary>
        /// Initializes one uniform-grid cell key.
        /// </summary>
        /// <param name="x">Cell index on the X axis.</param>
        /// <param name="y">Cell index on the Y axis.</param>
        /// <param name="z">Cell index on the Z axis.</param>
        public GridCellKey3D(int x, int y, int z) {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets the cell index on the X axis.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the cell index on the Y axis.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the cell index on the Z axis.
        /// </summary>
        public int Z { get; }

        /// <summary>
        /// Determines whether this key matches another cell key.
        /// </summary>
        /// <param name="other">Other cell key being compared.</param>
        /// <returns>True when all three indices match.</returns>
        public bool Equals(GridCellKey3D other) {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        /// <summary>
        /// Determines whether this key matches another object.
        /// </summary>
        /// <param name="obj">Object being compared.</param>
        /// <returns>True when the supplied object is an equal cell key.</returns>
        public override bool Equals(object obj) {
            if (obj is GridCellKey3D other) {
                return Equals(other);
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this cell key.
        /// </summary>
        /// <returns>Hash code value suitable for dictionary lookups.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
