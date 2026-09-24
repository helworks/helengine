namespace helengine {
    /// <summary>
    /// Captures the shader, borrowed texture, and scissor state shared by consecutive quads.
    /// </summary>
    public struct Batch2DRun {
        /// <summary>Gets the built-in shader variant used for this run.</summary>
        public Batch2DVariant Variant { get; private set; }
        /// <summary>Gets the borrowed texture sampled by textured runs, when present.</summary>
        public RuntimeTexture Texture { get; private set; }
        /// <summary>Gets the scissor's left pixel coordinate.</summary>
        public int ScissorX { get; private set; }
        /// <summary>Gets the scissor's top pixel coordinate.</summary>
        public int ScissorY { get; private set; }
        /// <summary>Gets the nonnegative scissor width in pixels.</summary>
        public int ScissorWidth { get; private set; }
        /// <summary>Gets the nonnegative scissor height in pixels.</summary>
        public int ScissorHeight { get; private set; }

        /// <summary>Creates a run key and rejects unknown variants or negative scissor extents.</summary>
        /// <param name="variant">The built-in shader variant for the run.</param>
        /// <param name="texture">The borrowed texture required by a textured run.</param>
        /// <param name="scissorX">The scissor's left pixel coordinate.</param>
        /// <param name="scissorY">The scissor's top pixel coordinate.</param>
        /// <param name="scissorWidth">The scissor width in pixels.</param>
        /// <param name="scissorHeight">The scissor height in pixels.</param>
        public Batch2DRun(Batch2DVariant variant, [NativeRetainsBorrow] RuntimeTexture texture,
            int scissorX, int scissorY, int scissorWidth, int scissorHeight) {
            if (variant != Batch2DVariant.Textured && variant != Batch2DVariant.RoundedShape) {
                throw new ArgumentOutOfRangeException(nameof(variant), "The batch shader variant is not supported.");
            }
            if (scissorWidth < 0) {
                throw new ArgumentOutOfRangeException(nameof(scissorWidth), "Scissor width cannot be negative.");
            }
            if (scissorHeight < 0) {
                throw new ArgumentOutOfRangeException(nameof(scissorHeight), "Scissor height cannot be negative.");
            }

            Variant = variant;
            Texture = texture;
            ScissorX = scissorX;
            ScissorY = scissorY;
            ScissorWidth = scissorWidth;
            ScissorHeight = scissorHeight;
        }
    }
}
