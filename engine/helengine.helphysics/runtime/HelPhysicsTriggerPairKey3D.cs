namespace helengine {
    /// <summary>Identifies one directional trigger overlap with generational body handles.</summary>
    readonly struct HelPhysicsTriggerPairKey3D : IEquatable<HelPhysicsTriggerPairKey3D> {
        /// <summary>Gets the body that owns the trigger side of the pair.</summary>
        public readonly HelPhysicsBodyHandle3D TriggerBody;
        /// <summary>Gets the body on the other side of the overlap.</summary>
        public readonly HelPhysicsBodyHandle3D OtherBody;

        /// <summary>Initializes a directional overlap identity.</summary>
        public HelPhysicsTriggerPairKey3D(HelPhysicsBodyHandle3D triggerBody, HelPhysicsBodyHandle3D otherBody) {
            TriggerBody = triggerBody;
            OtherBody = otherBody;
        }

        /// <summary>Determines whether both directional identities match exactly.</summary>
        public bool Equals(HelPhysicsTriggerPairKey3D other) {
            return TriggerBody.Index == other.TriggerBody.Index &&
                TriggerBody.Generation == other.TriggerBody.Generation &&
                OtherBody.Index == other.OtherBody.Index &&
                OtherBody.Generation == other.OtherBody.Generation;
        }

        /// <summary>Determines whether an object carries the same overlap identity.</summary>
        public override bool Equals(object obj) {
            return obj is HelPhysicsTriggerPairKey3D other && Equals(other);
        }

        /// <summary>Returns a stable hash code for the generational pair.</summary>
        public override int GetHashCode() {
            unchecked {
                int result = (TriggerBody.Index * 397) ^ TriggerBody.Generation;
                result = (result * 397) ^ OtherBody.Index;
                return (result * 397) ^ OtherBody.Generation;
            }
        }

        /// <summary>Compares two directional identities.</summary>
        public static bool operator ==(HelPhysicsTriggerPairKey3D left, HelPhysicsTriggerPairKey3D right) {
            return left.Equals(right);
        }

        /// <summary>Compares two directional identities.</summary>
        public static bool operator !=(HelPhysicsTriggerPairKey3D left, HelPhysicsTriggerPairKey3D right) {
            return !left.Equals(right);
        }
    }
}