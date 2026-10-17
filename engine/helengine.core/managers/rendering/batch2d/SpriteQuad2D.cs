namespace helengine {
    /// <summary>Resolved pixel positions preserving each sprite corner's texture-coordinate association.</summary>
    public struct SpriteQuad2D {
        /// <summary>Position sampled at the source rectangle's top-left UV.</summary>
        public float2 TopLeft;
        /// <summary>Position sampled at the source rectangle's top-right UV.</summary>
        public float2 TopRight;
        /// <summary>Position sampled at the source rectangle's bottom-right UV.</summary>
        public float2 BottomRight;
        /// <summary>Position sampled at the source rectangle's bottom-left UV.</summary>
        public float2 BottomLeft;
    }
}
