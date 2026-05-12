namespace helengine {
    /// <summary>
    /// Describes one resolved anchor layout space using a local origin and size in pixels.
    /// </summary>
    public sealed class AnchorSpace {
        /// <summary>
        /// Initializes one resolved anchor space.
        /// </summary>
        /// <param name="size">Size of the anchor space in local pixels.</param>
        /// <param name="origin">Origin of the anchor space in the parent local coordinate system.</param>
        public AnchorSpace(int2 size, float2 origin) {
            Size = size;
            Origin = origin;
        }

        /// <summary>
        /// Gets the size of the anchor space in local pixels.
        /// </summary>
        public int2 Size { get; }

        /// <summary>
        /// Gets the origin of the anchor space in the parent local coordinate system.
        /// </summary>
        public float2 Origin { get; }
    }
}
