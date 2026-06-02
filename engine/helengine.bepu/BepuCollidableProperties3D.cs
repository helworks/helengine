using BepuPhysics.Constraints;

namespace helengine {
    /// <summary>
    /// Stores authored collider filtering and contact material data for one BEPU collidable.
    /// </summary>
    public struct BepuCollidableProperties3D {
        /// <summary>
        /// Collision layer bit assigned to the collidable.
        /// </summary>
        public ushort CollisionLayer;

        /// <summary>
        /// Collision mask bits used to accept other collidable layers.
        /// </summary>
        public ushort CollisionMask;

        /// <summary>
        /// Dynamic friction coefficient applied to contact tangents.
        /// </summary>
        public float DynamicFriction;

        /// <summary>
        /// Maximum recovery velocity used to approximate authored restitution.
        /// </summary>
        public float MaximumRecoveryVelocity;

        /// <summary>
        /// Contact spring settings applied to generated manifolds.
        /// </summary>
        public SpringSettings SpringSettings;
    }
}
