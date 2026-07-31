namespace helengine {
    /// <summary>
    /// Identifies one unordered body pair in deterministic ascending body-index order for persistent manifold lookup.
    /// </summary>
    public readonly struct HelPhysicsPairKey3D : IEquatable<HelPhysicsPairKey3D> {
        /// <summary>
        /// Stores the lower of the two distinct body indices supplied to the constructor.
        /// </summary>
        public readonly int FirstBodyIndex;

        /// <summary>
        /// Stores the higher of the two distinct body indices supplied to the constructor.
        /// </summary>
        public readonly int SecondBodyIndex;

        /// <summary>
        /// Initializes a pair key by validating two body indices and storing them in canonical ascending order.
        /// </summary>
        /// <param name="firstBodyIndex">One non-negative body index in the pair.</param>
        /// <param name="secondBodyIndex">The other non-negative body index in the pair.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an index is negative or both indices identify the same body.</exception>
        public HelPhysicsPairKey3D(int firstBodyIndex, int secondBodyIndex) {
            if (firstBodyIndex < 0 || secondBodyIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(firstBodyIndex), "Physics manifold pair indices must be non-negative.");
            }

            if (firstBodyIndex == secondBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(secondBodyIndex), "Physics manifold pair indices must identify distinct bodies.");
            }

            if (firstBodyIndex < secondBodyIndex) {
                FirstBodyIndex = firstBodyIndex;
                SecondBodyIndex = secondBodyIndex;
            } else {
                FirstBodyIndex = secondBodyIndex;
                SecondBodyIndex = firstBodyIndex;
            }
        }

        /// <summary>
        /// Determines whether another canonical key identifies the same unordered pair of body indices.
        /// </summary>
        /// <param name="other">Pair key to compare against this key.</param>
        /// <returns><see langword="true"/> when both canonical indices are equal; otherwise <see langword="false"/>.</returns>
        public bool Equals(HelPhysicsPairKey3D other) {
            return FirstBodyIndex == other.FirstBodyIndex && SecondBodyIndex == other.SecondBodyIndex;
        }

        /// <summary>
        /// Determines whether a boxed value identifies the same unordered pair of body indices.
        /// </summary>
        /// <param name="obj">Boxed value to compare against this key.</param>
        /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal pair key; otherwise <see langword="false"/>.</returns>
        public override bool Equals(object obj) {
            if (obj is HelPhysicsPairKey3D) {
                return Equals((HelPhysicsPairKey3D)obj);
            }

            return false;
        }

        /// <summary>
        /// Returns a deterministic integer hash derived from the canonical body-index order.
        /// </summary>
        /// <returns>Stable hash value used by the manifold cache's power-of-two table mask.</returns>
        public override int GetHashCode() {
            unchecked {
                return (FirstBodyIndex * 397) ^ SecondBodyIndex;
            }
        }

        /// <summary>
        /// Determines whether two keys identify the same unordered pair of bodies.
        /// </summary>
        /// <param name="left">First pair key to compare.</param>
        /// <param name="right">Second pair key to compare.</param>
        /// <returns><see langword="true"/> when both keys have equal canonical indices; otherwise <see langword="false"/>.</returns>
        public static bool operator ==(HelPhysicsPairKey3D left, HelPhysicsPairKey3D right) {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two keys identify different unordered pairs of bodies.
        /// </summary>
        /// <param name="left">First pair key to compare.</param>
        /// <param name="right">Second pair key to compare.</param>
        /// <returns><see langword="true"/> when the canonical indices differ; otherwise <see langword="false"/>.</returns>
        public static bool operator !=(HelPhysicsPairKey3D left, HelPhysicsPairKey3D right) {
            return !left.Equals(right);
        }
    }
}
