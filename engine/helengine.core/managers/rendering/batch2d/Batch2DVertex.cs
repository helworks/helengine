namespace helengine {
    /// <summary>
    /// Stores one packed vertex shared by textured sprites and rounded shape batches.
    /// </summary>
    public struct Batch2DVertex {
        /// <summary>Pixel position as x, y, depth, and homogeneous coordinate.</summary>
        public float4 Position;
        /// <summary>Texture UV coordinates followed by shape-local pixel coordinates.</summary>
        public float4 TexLocal;
        /// <summary>Straight-alpha texture tint or rounded-shape fill color.</summary>
        public float4 Color;
        /// <summary>Shape half-width, half-height, corner radius, and border width.</summary>
        public float4 Shape;
        /// <summary>Corner enable values ordered top-left, top-right, bottom-left, bottom-right.</summary>
        public float4 Corners;
        /// <summary>Straight-alpha rounded-shape border color.</summary>
        public float4 BorderColor;
    }
}
