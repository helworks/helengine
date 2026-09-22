namespace helengine {
    /// <summary>
    /// Identifies one body slot and the generation that proves the slot has not since been recycled.
    /// </summary>
    public readonly struct HelPhysicsBodyHandle3D : IEquatable<HelPhysicsBodyHandle3D> {
        /// <summary>
        /// Stores the fixed body-pool slot index, with <see cref="ushort.MaxValue"/> reserved as invalid.
        /// </summary>
        public readonly ushort Index;

        /// <summary>
        /// Stores the slot generation issued when this handle was allocated.
        /// </summary>
        public readonly ushort Generation;

        /// <summary>
        /// Stores the world ownership token for public handles, with zero reserved for pool-internal identities.
        /// </summary>
        public readonly uint WorldId;

        /// <summary>
        /// Initializes a body handle from its pool index and allocation generation.
        /// </summary>
        /// <param name="index">Fixed pool slot index, or <see cref="ushort.MaxValue"/> for an invalid handle.</param>
        /// <param name="generation">Generation current when the handle was issued.</param>
        public HelPhysicsBodyHandle3D(ushort index, ushort generation) {
            Index = index;
            Generation = generation;
            WorldId = 0;
        }

        /// <summary>
        /// Initializes a public body handle from its pool identity and the world that owns it.
        /// </summary>
        /// <param name="index">Fixed pool slot index, or <see cref="ushort.MaxValue"/> for an invalid handle.</param>
        /// <param name="generation">Generation current when the handle was issued.</param>
        /// <param name="worldId">Nonzero ownership token assigned by the creating world.</param>
        public HelPhysicsBodyHandle3D(ushort index, ushort generation, uint worldId) {
            Index = index;
            Generation = generation;
            WorldId = worldId;
        }

        /// <summary>
        /// Determines whether another handle carries the same slot, generation, and world identity.
        /// </summary>
        /// <param name="other">Handle to compare with this value.</param>
        /// <returns><see langword="true"/> when all identity fields match.</returns>
        public bool Equals(HelPhysicsBodyHandle3D other) {
            return Index == other.Index &&
                Generation == other.Generation &&
                WorldId == other.WorldId;
        }

        /// <summary>
        /// Determines whether an object carries the same body handle identity.
        /// </summary>
        /// <param name="obj">Object to compare with this handle.</param>
        /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal body handle.</returns>
        public override bool Equals(object obj) {
            return obj is HelPhysicsBodyHandle3D other && Equals(other);
        }

        /// <summary>
        /// Returns a deterministic hash derived from slot, generation, and world ownership.
        /// </summary>
        /// <returns>A stable hash suitable for native and managed keyed collections.</returns>
        public override int GetHashCode() {
            unchecked {
                int result = (Index * 397) ^ Generation;
                return (result * 397) ^ (int)WorldId;
            }
        }

        /// <summary>
        /// Compares two body handle identities.
        /// </summary>
        /// <param name="left">Left handle operand.</param>
        /// <param name="right">Right handle operand.</param>
        /// <returns><see langword="true"/> when all identity fields match.</returns>
        public static bool operator ==(HelPhysicsBodyHandle3D left, HelPhysicsBodyHandle3D right) {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two body handle identities for inequality.
        /// </summary>
        /// <param name="left">Left handle operand.</param>
        /// <param name="right">Right handle operand.</param>
        /// <returns><see langword="true"/> when any identity field differs.</returns>
        public static bool operator !=(HelPhysicsBodyHandle3D left, HelPhysicsBodyHandle3D right) {
            return !left.Equals(right);
        }
    }
}
