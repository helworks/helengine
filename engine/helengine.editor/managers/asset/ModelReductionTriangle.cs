namespace helengine.editor {
    /// <summary>Keeps triangle winding and authored material ownership during endpoint collapse.</summary>
    sealed class ModelReductionTriangle {
        /// <summary>Ordered attribute vertex indices.</summary>
        public readonly int[] Vertices = new int[3];
        /// <summary>Original submesh index, retained through all simplification steps.</summary>
        public int Submesh;
        /// <summary>Whether this triangle remains part of the final surface.</summary>
        public bool Active = true;
    }
}
