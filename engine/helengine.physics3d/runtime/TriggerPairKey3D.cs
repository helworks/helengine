namespace helengine {
    /// <summary>
    /// Identifies one stable trigger overlap pair using the trigger owner and the other overlapped entity.
    /// </summary>
    public readonly struct TriggerPairKey3D : IEquatable<TriggerPairKey3D> {
        /// <summary>
        /// Initializes one trigger-pair key using the entity that owns the trigger collider and the other overlapped entity.
        /// </summary>
        /// <param name="triggerEntity">Entity that owns the trigger collider.</param>
        /// <param name="otherEntity">Other entity overlapped by the trigger collider.</param>
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
        /// Gets the other entity overlapped by the trigger collider.
        /// </summary>
        public Entity OtherEntity { get; }

        /// <summary>
        /// Determines whether two trigger-pair keys identify the same ordered entity pair.
        /// </summary>
        /// <param name="other">Other key being compared.</param>
        /// <returns>True when both keys identify the same trigger and other entities.</returns>
        public bool Equals(TriggerPairKey3D other) {
            return ReferenceEquals(TriggerEntity, other.TriggerEntity) &&
                ReferenceEquals(OtherEntity, other.OtherEntity);
        }

        /// <summary>
        /// Determines whether this key equals another object instance.
        /// </summary>
        /// <param name="obj">Object instance being compared.</param>
        /// <returns>True when the supplied object is an equal trigger-pair key.</returns>
        public override bool Equals(object obj) {
            if (obj is TriggerPairKey3D other) {
                return Equals(other);
            }

            return false;
        }

        /// <summary>
        /// Produces the hash code used by trigger overlap tracking sets.
        /// </summary>
        /// <returns>Hash code for the ordered entity pair.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(TriggerEntity, OtherEntity);
        }
    }
}
