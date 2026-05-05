namespace helengine {
    /// <summary>
    /// Describes one trigger overlap event emitted by the 3D physics runtime during a fixed step.
    /// </summary>
    public sealed class TriggerEvent3D {
        /// <summary>
        /// Initializes one trigger overlap event.
        /// </summary>
        /// <param name="kind">Lifecycle transition emitted during the current step.</param>
        /// <param name="triggerEntity">Entity that owns the trigger collider.</param>
        /// <param name="otherEntity">Other entity involved in the overlap pair.</param>
        public TriggerEvent3D(TriggerEventKind3D kind, Entity triggerEntity, Entity otherEntity) {
            Kind = kind;
            TriggerEntity = triggerEntity ?? throw new ArgumentNullException(nameof(triggerEntity));
            OtherEntity = otherEntity ?? throw new ArgumentNullException(nameof(otherEntity));
        }

        /// <summary>
        /// Gets the lifecycle transition emitted during the current step.
        /// </summary>
        public TriggerEventKind3D Kind { get; }

        /// <summary>
        /// Gets the entity that owns the trigger collider.
        /// </summary>
        public Entity TriggerEntity { get; }

        /// <summary>
        /// Gets the other entity involved in the overlap pair.
        /// </summary>
        public Entity OtherEntity { get; }
    }
}
