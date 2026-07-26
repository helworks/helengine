namespace helengine.editor {
    /// <summary>
    /// Identifies one attribute-vertex edge independently of its winding direction.
    /// </summary>
    internal readonly struct ModelTessellationAttributeEdgeKey : IEquatable<ModelTessellationAttributeEdgeKey> {
        /// <summary>
        /// Gets the smaller endpoint index.
        /// </summary>
        readonly int FirstIndex;

        /// <summary>
        /// Gets the larger endpoint index.
        /// </summary>
        readonly int SecondIndex;

        /// <summary>
        /// Initializes one unordered attribute edge key.
        /// </summary>
        /// <param name="firstIndex">First raw vertex index.</param>
        /// <param name="secondIndex">Second raw vertex index.</param>
        public ModelTessellationAttributeEdgeKey(int firstIndex, int secondIndex) {
            if (firstIndex <= secondIndex) {
                FirstIndex = firstIndex;
                SecondIndex = secondIndex;
            } else {
                FirstIndex = secondIndex;
                SecondIndex = firstIndex;
            }
        }

        /// <summary>
        /// Determines whether another attribute edge has the same endpoints.
        /// </summary>
        /// <param name="other">Other key to compare.</param>
        /// <returns>True when the endpoints match.</returns>
        public bool Equals(ModelTessellationAttributeEdgeKey other) {
            return FirstIndex == other.FirstIndex && SecondIndex == other.SecondIndex;
        }

        /// <summary>
        /// Determines whether an arbitrary object represents the same attribute edge.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True when the supplied object is an equal key.</returns>
        public override bool Equals(object obj) {
            return obj is ModelTessellationAttributeEdgeKey other && Equals(other);
        }

        /// <summary>
        /// Gets a stable hash code for dictionary lookup.
        /// </summary>
        /// <returns>Hash code derived from both endpoint indices.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(FirstIndex, SecondIndex);
        }
    }
}
