namespace helengine {
    /// <summary>
    /// Stores one stable trigger-owner pair tracked across fixed simulation steps.
    /// </summary>
    public readonly struct TriggerPairKey3D : IEquatable<TriggerPairKey3D> {
        /// <summary>
        /// Initializes one trigger pair key.
        /// </summary>
        /// <param name="triggerEntity">Entity that owns the trigger collider.</param>
        /// <param name="otherEntity">Other entity participating in the overlap.</param>
        public TriggerPairKey3D(Entity triggerEntity, Entity otherEntity) {
            TriggerEntity = triggerEntity ?? throw new ArgumentNullException(nameof(triggerEntity));
            OtherEntity = otherEntity ?? throw new ArgumentNullException(nameof(otherEntity));
            if (ReferenceEquals(triggerEntity, otherEntity)) {
                throw new ArgumentException("Trigger pair keys require two distinct entities.");
            }
        }

        /// <summary>
        /// Gets the entity that owns the trigger collider.
        /// </summary>
        public Entity TriggerEntity { get; }

        /// <summary>
        /// Gets the other entity participating in the overlap pair.
        /// </summary>
        public Entity OtherEntity { get; }

        /// <inheritdoc />
        public bool Equals(TriggerPairKey3D other) {
            return TriggerEntity == other.TriggerEntity &&
                OtherEntity == other.OtherEntity;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is TriggerPairKey3D other && Equals(other);
        }
    }
}
