namespace helengine {
    /// <summary>
    /// Stores up to four cube contact points for one colliding body pair.
    /// </summary>
    public sealed class CubeContactManifold3D {
        /// <summary>
        /// Initializes one single-point contact manifold.
        /// </summary>
        /// <param name="normal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="point0">Primary contact point.</param>
        public CubeContactManifold3D(float3 normal, CubeContactPoint3D point0) {
            Normal = normal;
            Point0 = point0 ?? throw new ArgumentNullException(nameof(point0));
            Point1 = null;
            Point2 = null;
            Point3 = null;
            ContactCount = 1;
        }

        /// <summary>
        /// Initializes one four-point face contact manifold.
        /// </summary>
        /// <param name="normal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="point0">First contact patch point.</param>
        /// <param name="point1">Second contact patch point.</param>
        /// <param name="point2">Third contact patch point.</param>
        /// <param name="point3">Fourth contact patch point.</param>
        public CubeContactManifold3D(float3 normal, CubeContactPoint3D point0, CubeContactPoint3D point1, CubeContactPoint3D point2, CubeContactPoint3D point3) {
            Normal = normal;
            Point0 = point0 ?? throw new ArgumentNullException(nameof(point0));
            Point1 = point1 ?? throw new ArgumentNullException(nameof(point1));
            Point2 = point2 ?? throw new ArgumentNullException(nameof(point2));
            Point3 = point3 ?? throw new ArgumentNullException(nameof(point3));
            ContactCount = 4;
        }

        /// <summary>
        /// Gets the unit normal pointing from the second body toward the first body.
        /// </summary>
        public float3 Normal { get; }

        /// <summary>
        /// Gets the primary contact point.
        /// </summary>
        public CubeContactPoint3D Point0 { get; }

        /// <summary>
        /// Gets the second contact point when the manifold contains a face patch.
        /// </summary>
        public CubeContactPoint3D Point1 { get; }

        /// <summary>
        /// Gets the third contact point when the manifold contains a face patch.
        /// </summary>
        public CubeContactPoint3D Point2 { get; }

        /// <summary>
        /// Gets the fourth contact point when the manifold contains a face patch.
        /// </summary>
        public CubeContactPoint3D Point3 { get; }

        /// <summary>
        /// Gets the number of valid contact points in this manifold.
        /// </summary>
        public int ContactCount { get; }

        /// <summary>
        /// Gets one valid contact point by zero-based index.
        /// </summary>
        /// <param name="index">Contact index from zero through <see cref="ContactCount"/> minus one.</param>
        /// <returns>Contact point at the requested index.</returns>
        public CubeContactPoint3D GetPoint(int index) {
            if (index == 0) {
                return Point0;
            }
            if (index == 1 && ContactCount > 1) {
                return Point1;
            }
            if (index == 2 && ContactCount > 2) {
                return Point2;
            }
            if (index == 3 && ContactCount > 3) {
                return Point3;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Contact point index is outside the valid manifold range.");
        }
    }
}
