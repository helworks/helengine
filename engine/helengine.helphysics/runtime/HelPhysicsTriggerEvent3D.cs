namespace helengine {
    /// <summary>Publishes one stable handle pair for a trigger overlap transition.</summary>
    public readonly struct HelPhysicsTriggerEvent3D {
        /// <summary>Gets the directional trigger body identity.</summary>
        public readonly HelPhysicsBodyHandle3D TriggerBody;
        /// <summary>Gets the other body identity.</summary>
        public readonly HelPhysicsBodyHandle3D OtherBody;
        /// <summary>Gets the transition kind.</summary>
        public readonly HelPhysicsTriggerEventKind3D Kind;

        /// <summary>Initializes one trigger transition.</summary>
        public HelPhysicsTriggerEvent3D(
            HelPhysicsTriggerEventKind3D kind,
            HelPhysicsBodyHandle3D triggerBody,
            HelPhysicsBodyHandle3D otherBody) {
            Kind = kind;
            TriggerBody = triggerBody;
            OtherBody = otherBody;
        }
    }
}