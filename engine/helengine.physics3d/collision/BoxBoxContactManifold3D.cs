namespace helengine {
    /// <summary>
    /// Stores the reduced four-point contact patch produced by one box-box collision query.
    /// </summary>
    public struct BoxBoxContactManifold3D {
        /// <summary>
        /// Unit normal pointing from the second box toward the first box.
        /// </summary>
        public float3 Normal;

        /// <summary>
        /// Positive overlap distance along the contact normal.
        /// </summary>
        public float Penetration;

        /// <summary>
        /// Signed penetration depth for the first contact point.
        /// </summary>
        public float Penetration0;

        /// <summary>
        /// Signed penetration depth for the second contact point.
        /// </summary>
        public float Penetration1;

        /// <summary>
        /// Signed penetration depth for the third contact point.
        /// </summary>
        public float Penetration2;

        /// <summary>
        /// Signed penetration depth for the fourth contact point.
        /// </summary>
        public float Penetration3;

        /// <summary>
        /// First world-space contact point on the shared contact patch.
        /// </summary>
        public float3 Contact0;

        /// <summary>
        /// Second world-space contact point on the shared contact patch.
        /// </summary>
        public float3 Contact1;

        /// <summary>
        /// Third world-space contact point on the shared contact patch.
        /// </summary>
        public float3 Contact2;

        /// <summary>
        /// Fourth world-space contact point on the shared contact patch.
        /// </summary>
        public float3 Contact3;

        /// <summary>
        /// Stable feature id for the first contact point.
        /// </summary>
        public int FeatureId0;

        /// <summary>
        /// Stable feature id for the second contact point.
        /// </summary>
        public int FeatureId1;

        /// <summary>
        /// Stable feature id for the third contact point.
        /// </summary>
        public int FeatureId2;

        /// <summary>
        /// Stable feature id for the fourth contact point.
        /// </summary>
        public int FeatureId3;

        /// <summary>
        /// Number of valid contact points stored in this manifold.
        /// </summary>
        public int ContactCount;
    }
}
