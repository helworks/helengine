namespace helengine {
    /// <summary>
    /// Carries one resolved character-controller pose, velocity, and grounding result.
    /// </summary>
    public readonly struct HelPhysicsCharacterControllerMotion3D {
        /// <summary>
        /// Initializes one resolved controller motion result.
        /// </summary>
        /// <param name="position">Resolved world-space controller center.</param>
        /// <param name="verticalVelocity">Resolved vertical velocity for the next step.</param>
        /// <param name="isGrounded">Whether the controller was snapped to walkable support.</param>
        /// <param name="supportVelocity">Velocity of the support used by the resolved motion.</param>
        public HelPhysicsCharacterControllerMotion3D(float3 position, float verticalVelocity, bool isGrounded, float3 supportVelocity) {
            Position = position;
            VerticalVelocity = verticalVelocity;
            IsGrounded = isGrounded;
            SupportVelocity = supportVelocity;
        }

        /// <summary>
        /// Gets the resolved world-space controller center.
        /// </summary>
        public float3 Position { get; }

        /// <summary>
        /// Gets the resolved vertical velocity retained for the next fixed step.
        /// </summary>
        public float VerticalVelocity { get; }

        /// <summary>
        /// Gets whether the resolved controller is grounded on walkable support.
        /// </summary>
        public bool IsGrounded { get; }

        /// <summary>
        /// Gets the support velocity inherited by the resolved controller.
        /// </summary>
        public float3 SupportVelocity { get; }
    }
}
