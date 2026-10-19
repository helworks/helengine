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
        public static float4 ResolveViewport(float4 viewport, RealEvaluation targetWidth, RealEvaluation targetHeight) {
            return LayoutNumericKernel<RealEvaluation, RuntimeNumericOperations>.CameraViewport(viewport, targetWidth, targetHeight, new RuntimeNumericOperations());
        }

        /// <summary>Resolves the same camera rectangle for a detached cook using its target precision.</summary>
        public static float4 ResolveViewport(float4 viewport, double targetWidth, double targetHeight, NumericPrecisionPolicy policy) {
            CookNumericOperations math = new CookNumericOperations(policy);
            return LayoutNumericKernel<double, CookNumericOperations>.CameraViewport(viewport, math.Number(targetWidth), math.Number(targetHeight), math);
        }
    }
}
