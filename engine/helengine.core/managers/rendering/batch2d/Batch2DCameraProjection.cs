namespace helengine {
    /// <summary>
    /// Creates the shared pixel-to-clip projection used by batched 2D renderers.
    /// </summary>
    public static class Batch2DCameraProjection {
        /// <summary>
        /// Maps a finite positive pixel viewport to clip space with a top-left origin and Y-down coordinates.
        /// </summary>
        /// <param name="pixelViewport">Viewport origin X/Y followed by positive width/height.</param>
        /// <returns>A row-vector projection mapping the viewport top-left to (-1,+1) and bottom-right to (+1,-1).</returns>
        public static float4x4 Create(float4 pixelViewport) {
            if (!IsFinite(pixelViewport.X) || !IsFinite(pixelViewport.Y) ||
                !IsFinite(pixelViewport.Z) || !IsFinite(pixelViewport.W) ||
                pixelViewport.Z <= 0f || pixelViewport.W <= 0f) {
                throw new ArgumentException("Pixel viewport values must be finite and its dimensions must be positive.", nameof(pixelViewport));
            }

            double inverseWidth = 1d / pixelViewport.Z;
            double inverseHeight = 1d / pixelViewport.W;
            float scaleX = (float)(2d * inverseWidth);
            float scaleY = (float)(-2d * inverseHeight);
            float translateX = (float)(-1d - 2d * pixelViewport.X * inverseWidth);
            float translateY = (float)(1d + 2d * pixelViewport.Y * inverseHeight);

            float4x4 projection = new float4x4(
                scaleX, 0f, 0f, 0f,
                0f, scaleY, 0f, 0f,
                0f, 0f, 1f, 0f,
                translateX, translateY, 0f, 1f);
            if (!IsFinite(projection.M11) || !IsFinite(projection.M22) ||
                !IsFinite(projection.M41) || !IsFinite(projection.M42)) {
                throw new ArgumentException("Pixel viewport produces projection coefficients outside the finite float range.", nameof(pixelViewport));
            }

            return projection;
        }

        /// <summary>
        /// Determines whether a viewport component can participate in finite projection arithmetic.
        /// </summary>
        /// <param name="value">Component to check.</param>
        /// <returns>True when the value is neither infinite nor NaN.</returns>
        static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
