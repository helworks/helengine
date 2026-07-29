namespace helengine {
    /// <summary>
    /// Identifies one geometric contact feature with a compact deterministic value suitable for manifold persistence.
    /// </summary>
    readonly struct HelPhysicsContactFeature3D : IEquatable<HelPhysicsContactFeature3D> {
        /// <summary>
        /// Stores the packed shape-pair feature identifier used for exact contact matching.
        /// </summary>
        public readonly uint Value;

        /// <summary>
        /// Initializes a contact feature from an already packed deterministic identifier.
        /// </summary>
        /// <param name="value">Packed feature value whose bit layout is owned by the collision routine that creates it.</param>
        public HelPhysicsContactFeature3D(uint value) {
            Value = value;
        }

        /// <summary>
        /// Determines whether another contact feature carries the same packed identifier.
        /// </summary>
        /// <param name="other">Contact feature to compare.</param>
        /// <returns>True when both features identify the same geometric provenance.</returns>
        public bool Equals(HelPhysicsContactFeature3D other) {
            return Value == other.Value;
        }

        /// <summary>
        /// Determines whether an object is a contact feature with the same packed identifier.
        /// </summary>
        /// <param name="obj">Object to compare with this feature.</param>
        /// <returns>True when <paramref name="obj"/> is an equal contact feature.</returns>
        public override bool Equals(object obj) {
            return obj is HelPhysicsContactFeature3D other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code derived directly from the packed identifier.
        /// </summary>
        /// <returns>A hash code suitable for value-based collections outside collision hot loops.</returns>
        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Compares two contact features by their packed identifiers.
        /// </summary>
        /// <param name="left">Left feature operand.</param>
        /// <param name="right">Right feature operand.</param>
        /// <returns>True when both operands identify the same feature.</returns>
        public static bool operator ==(HelPhysicsContactFeature3D left, HelPhysicsContactFeature3D right) {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two contact features by their packed identifiers.
        /// </summary>
        /// <param name="left">Left feature operand.</param>
        /// <param name="right">Right feature operand.</param>
        /// <returns>True when the operands identify different features.</returns>
        public static bool operator !=(HelPhysicsContactFeature3D left, HelPhysicsContactFeature3D right) {
            return !left.Equals(right);
        }
    }
}
