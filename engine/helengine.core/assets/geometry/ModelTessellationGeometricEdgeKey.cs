namespace helengine {
    /// <summary>
    /// Identifies one position-space edge independently of duplicated attribute vertices and winding direction.
    /// </summary>
    internal sealed class ModelTessellationGeometricEdgeKey : IEquatable<ModelTessellationGeometricEdgeKey> {
        /// <summary>
        /// Stored coordinate value for the first endpoint's X coordinate.
        /// </summary>
        readonly Real FirstX;

        /// <summary>
        /// Stored coordinate value for the first endpoint's Y coordinate.
        /// </summary>
        readonly Real FirstY;

        /// <summary>
        /// Stored coordinate value for the first endpoint's Z coordinate.
        /// </summary>
        readonly Real FirstZ;

        /// <summary>
        /// Stored coordinate value for the second endpoint's X coordinate.
        /// </summary>
        readonly Real SecondX;

        /// <summary>
        /// Stored coordinate value for the second endpoint's Y coordinate.
        /// </summary>
        readonly Real SecondY;

        /// <summary>
        /// Stored coordinate value for the second endpoint's Z coordinate.
        /// </summary>
        readonly Real SecondZ;

        /// <summary>
        /// Initializes one unordered position-space edge key.
        /// </summary>
        /// <param name="first">First endpoint position.</param>
        /// <param name="second">Second endpoint position.</param>
        public ModelTessellationGeometricEdgeKey(float3 first, float3 second) {
            Real firstX = first.X;
            Real firstY = first.Y;
            Real firstZ = first.Z;
            Real secondX = second.X;
            Real secondY = second.Y;
            Real secondZ = second.Z;
            if (Compare(firstX, firstY, firstZ, secondX, secondY, secondZ) <= 0) {
                FirstX = firstX;
                FirstY = firstY;
                FirstZ = firstZ;
                SecondX = secondX;
                SecondY = secondY;
                SecondZ = secondZ;
            } else {
                FirstX = secondX;
                FirstY = secondY;
                FirstZ = secondZ;
                SecondX = firstX;
                SecondY = firstY;
                SecondZ = firstZ;
            }
        }

        /// <summary>
        /// Determines whether another position-space edge has the same endpoints.
        /// </summary>
        /// <param name="other">Other key to compare.</param>
        /// <returns>True when the stored endpoints match.</returns>
        public bool Equals(ModelTessellationGeometricEdgeKey other) {
            return FirstX == other.FirstX && FirstY == other.FirstY && FirstZ == other.FirstZ
                && SecondX == other.SecondX && SecondY == other.SecondY && SecondZ == other.SecondZ;
        }

        /// <summary>
        /// Determines whether an arbitrary object represents the same position-space edge.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True when the supplied object is an equal key.</returns>
        public override bool Equals(object obj) {
            return false;
        }

        /// <summary>
        /// Gets a stable hash code for dictionary lookup.
        /// </summary>
        /// <returns>Hash code derived from the stored endpoint coordinates.</returns>
        public override int GetHashCode() {
            unchecked {
                int hashCode = FirstX.GetHashCode();
                hashCode = (hashCode * 397) ^ FirstY.GetHashCode();
                hashCode = (hashCode * 397) ^ FirstZ.GetHashCode();
                hashCode = (hashCode * 397) ^ SecondX.GetHashCode();
                hashCode = (hashCode * 397) ^ SecondY.GetHashCode();
                return (hashCode * 397) ^ SecondZ.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two stored three-component positions lexicographically.
        /// </summary>
        /// <param name="leftX">First left coordinate.</param>
        /// <param name="leftY">Second left coordinate.</param>
        /// <param name="leftZ">Third left coordinate.</param>
        /// <param name="rightX">First right coordinate.</param>
        /// <param name="rightY">Second right coordinate.</param>
        /// <param name="rightZ">Third right coordinate.</param>
        /// <returns>Negative when left precedes right, zero when equal, otherwise positive.</returns>
        static int Compare(Real leftX, Real leftY, Real leftZ, Real rightX, Real rightY, Real rightZ) {
            int result = leftX.CompareTo(rightX);
            if (result != 0) {
                return result;
            }

            result = leftY.CompareTo(rightY);
            if (result != 0) {
                return result;
            }

            return leftZ.CompareTo(rightZ);
        }
    }
}
