namespace helengine {
    /// <summary>
    /// Resolves authored camera viewports into pixel-space rectangles for active render targets.
    /// </summary>
    public static class CameraViewportResolver {
        /// <summary>
        /// Resolves one authored viewport against the supplied target dimensions.
        /// </summary>
        /// <param name="viewport">Authored viewport expressed either in pixels or normalized target fractions.</param>
        /// <param name="targetWidth">Target width in pixels.</param>
        /// <param name="targetHeight">Target height in pixels.</param>
        /// <returns>Viewport rectangle expressed in pixel-space coordinates.</returns>
        public static float4 ResolveViewport(float4 viewport, double targetWidth, double targetHeight) {
            if (targetWidth <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(targetWidth), "Target width must be greater than zero.");
            }
            if (targetHeight <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(targetHeight), "Target height must be greater than zero.");
            }

            double offsetX = viewport.X;
            double offsetY = viewport.Y;
            double width = viewport.Z;
            double height = viewport.W;
            if (width <= 1.0 && height <= 1.0) {
                offsetX *= targetWidth;
                width *= targetWidth;
                if (UsesStackedDualScreenViewportUnits(viewport, targetWidth, targetHeight)) {
                    double screenHeight = targetHeight * 0.5d;
                    offsetY *= screenHeight;
                    height *= screenHeight;
                } else {
                    offsetY *= targetHeight;
                    height *= targetHeight;
                }
            }

            return new float4((float)offsetX, (float)offsetY, (float)width, (float)height);
        }

        /// <summary>
        /// Returns whether the supplied normalized viewport should be interpreted against stacked dual-screen vertical units.
        /// </summary>
        /// <param name="viewport">Authored viewport expressed in normalized units.</param>
        /// <param name="targetWidth">Target width in pixels.</param>
        /// <param name="targetHeight">Target height in pixels.</param>
        /// <returns>True when the target represents two equal stacked screens and the viewport uses per-screen vertical units.</returns>
        static bool UsesStackedDualScreenViewportUnits(float4 viewport, double targetWidth, double targetHeight) {
            double expectedStackedHeight = targetWidth * 1.5d;
            if (Math.Abs(targetHeight - expectedStackedHeight) > 0.5d) {
                return false;
            }

            return viewport.Y >= 0.0f && (viewport.Y + viewport.W) <= 2.0f;
        }
    }
}
