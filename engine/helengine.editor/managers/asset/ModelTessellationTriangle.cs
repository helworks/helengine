namespace helengine.editor {
    /// <summary>
    /// Stores one mutable indexed triangle and the source submesh it belongs to during model tessellation.
    /// </summary>
    internal sealed class ModelTessellationTriangle {
        /// <summary>
        /// Gets or sets the first vertex index in winding order.
        /// </summary>
        public int FirstIndex { get; set; }

        /// <summary>
        /// Gets or sets the second vertex index in winding order.
        /// </summary>
        public int SecondIndex { get; set; }

        /// <summary>
        /// Gets or sets the third vertex index in winding order.
        /// </summary>
        public int ThirdIndex { get; set; }

        /// <summary>
        /// Gets the source submesh index, or negative one when the source has no submesh metadata.
        /// </summary>
        public int SubmeshIndex { get; }

        /// <summary>
        /// Initializes one mutable triangle.
        /// </summary>
        /// <param name="firstIndex">First vertex index in winding order.</param>
        /// <param name="secondIndex">Second vertex index in winding order.</param>
        /// <param name="thirdIndex">Third vertex index in winding order.</param>
        /// <param name="submeshIndex">Source submesh index, or negative one without submesh metadata.</param>
        public ModelTessellationTriangle(int firstIndex, int secondIndex, int thirdIndex, int submeshIndex) {
            FirstIndex = firstIndex;
            SecondIndex = secondIndex;
            ThirdIndex = thirdIndex;
            SubmeshIndex = submeshIndex;
        }
    }
}
