namespace helengine {
    /// <summary>
    /// Identifies the deterministic general mutation represented by one fixed deferred world-command slot.
    /// </summary>
    enum HelPhysicsDeferredCommandKind3D {
        /// <summary>
        /// Publishes a body and shape reservation into active simulation storage.
        /// </summary>
        ActivateBody = 0,

        /// <summary>
        /// Wakes a dynamic island and accumulates one world-space force for same-step integration.
        /// </summary>
        ApplyForce = 1,

        /// <summary>
        /// Wakes a dynamic island and applies one immediate world-space linear impulse.
        /// </summary>
        ApplyImpulse = 2,

        /// <summary>
        /// Replaces one kinematic body's world pose and authored linear and angular velocity at the next fixed-step boundary.
        /// </summary>
        SetKinematicState = 3
    }
}
