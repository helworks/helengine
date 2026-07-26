namespace helengine.editor {
    /// <summary>
    /// Identifies one position-space edge independently of duplicated attribute vertices and winding direction.
    /// </summary>
    internal readonly struct ModelTessellationGeometricEdgeKey : IEquatable<ModelTessellationGeometricEdgeKey> {
        /// <summary>
        /// Packed bit pattern for the first endpoint's X coordinate.
        /// </summary>
        readonly int FirstX;

        /// <summary>
        /// Packed bit pattern for the first endpoint's Y coordinate.
        /// </summary>
        readonly int FirstY;

        /// <summary>
        /// Packed bit pattern for the first endpoint's Z coordinate.
        /// </summary>
        readonly int FirstZ;

        /// <summary>
        /// Packed bit pattern for the second endpoint's X coordinate.
        /// </summary>
        readonly int SecondX;

        /// <summary>
        /// Packed bit pattern for the second endpoint's Y coordinate.
        /// </summary>
        readonly int SecondY;

        /// <summary>
        /// Packed bit pattern for the second endpoint's Z coordinate.
        /// </summary>
        readonly int SecondZ;

        /// <summary>
        /// Initializes one unordered position-space edge key.
        /// </summary>
        /// <param name="first">First endpoint position.</param>
        /// <param name="second">Second endpoint position.</param>
        public ModelTessellationGeometricEdgeKey(float3 first, float3 second) {
            int firstX = BitConverter.SingleToInt32Bits(first.X);
            int firstY = BitConverter.SingleToInt32Bits(first.Y);
            int firstZ = BitConverter.SingleToInt32Bits(first.Z);
            int secondX = BitConverter.SingleToInt32Bits(second.X);
            int secondY = BitConverter.SingleToInt32Bits(second.Y);
            int secondZ = BitConverter.SingleToInt32Bits(second.Z);
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
        /// <returns>True when the packed endpoints match.</returns>
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
            return obj is ModelTessellationGeometricEdgeKey other && Equals(other);
        }

        /// <summary>
        /// Gets a stable hash code for dictionary lookup.
        /// </summary>
        /// <returns>Hash code derived from the packed endpoint coordinates.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(FirstX, FirstY, FirstZ, SecondX, SecondY, SecondZ);
        }

        /// <summary>
        /// Compares two packed three-component positions lexicographically.
        /// </summary>
        /// <param name="leftX">First left coordinate.</param>
        /// <param name="leftY">Second left coordinate.</param>
        /// <param name="leftZ">Third left coordinate.</param>
        /// <param name="rightX">First right coordinate.</param>
        /// <param name="rightY">Second right coordinate.</param>
        /// <param name="rightZ">Third right coordinate.</param>
        /// <returns>Negative when left precedes right, zero when equal, otherwise positive.</returns>
        static int Compare(int leftX, int leftY, int leftZ, int rightX, int rightY, int rightZ) {
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
