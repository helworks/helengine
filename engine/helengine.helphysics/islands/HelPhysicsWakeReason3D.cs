namespace helengine {
    /// <summary>
    /// Identifies the first condition that transitioned one complete dynamic island from asleep to awake during a fixed step.
    /// </summary>
    enum HelPhysicsWakeReason3D {
        /// <summary>
        /// Indicates that no asleep-to-awake island transition was recorded.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that an explicitly applied force initiated the island wake.
        /// </summary>
        ExplicitForce = 1,

        /// <summary>
        /// Indicates that an explicitly applied impulse initiated the island wake.
        /// </summary>
        ExplicitImpulse = 2,

        /// <summary>
        /// Indicates that a meaningful new broadphase candidate touching a sleeping body initiated the island wake.
        /// </summary>
        NewCandidateContact = 3,

        /// <summary>
        /// Indicates that active contact with a moving kinematic body initiated the island wake.
        /// </summary>
        MovingKinematicContact = 4
    }
}
