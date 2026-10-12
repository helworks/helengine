namespace helengine.editor {
    /// <summary>Tracks all attribute wedges at one exact geometric position.</summary>
    sealed class ModelReductionGroup {
        /// <summary>Existing endpoint position; reduction never moves it across a UV or material seam.</summary>
        public float3 Position;
        /// <summary>Attribute vertices currently referenced at this position.</summary>
        public readonly HashSet<int> Vertices = new HashSet<int>();
        /// <summary>Active incident triangle identifiers.</summary>
        public readonly HashSet<int> Faces = new HashSet<int>();
        /// <summary>Plane-error contribution of all collapsed geometric vertices.</summary>
        public readonly ModelReductionQuadric Quadric = new ModelReductionQuadric();
        /// <summary>Changes whenever local topology changes, invalidating queued candidates.</summary>
        public int Revision;
    }
}
