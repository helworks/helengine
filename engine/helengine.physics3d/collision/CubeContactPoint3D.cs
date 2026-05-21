namespace helengine {
    /// <summary>
    /// Stores one world-space cube contact point and its positive penetration depth.
    /// </summary>
    public sealed class CubeContactPoint3D {
        /// <summary>
        /// Initializes one contact point.
        /// </summary>
        /// <param name="position">World-space point on the contact patch.</param>
        /// <param name="penetration">Positive penetration depth along the manifold normal.</param>
        public CubeContactPoint3D(float3 position, float penetration) {
            Position = position;
            Penetration = penetration;
        }

        /// <summary>
        /// Gets the world-space point on the contact patch.
        /// </summary>
        public float3 Position { get; }

        /// <summary>
        /// Gets the positive penetration depth along the manifold normal.
        /// </summary>
        public float Penetration { get; }
    }
}
