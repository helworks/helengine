namespace helengine {
    /// <summary>Resolves layout factors identically for cooking, loading and dynamic recomposition.</summary>
    public static class ViewportLayoutMath {
        /// <summary>Calculates fit, stretch or physical-density factors from an explicit presentation target.</summary>
        /// <param name="scalingMode">Existing viewport scaling mode.</param>
        /// <param name="referenceSize">Authored reference canvas.</param>
        /// <param name="targetSize">Effective framebuffer or camera rectangle.</param>
        /// <param name="pixelAspect">Displayed ratio of each target pixel.</param>
        /// <param name="uiScale">User multiplier for physical-density layout.</param>
        /// <param name="dpi">Known output density, required only for Density.</param>
        /// <param name="referenceDpi">Authored physical density.</param>
        /// <returns>Resolved factors without capturing participant values.</returns>
        public static ViewportLayoutMetrics Resolve(int scalingMode, int2 referenceSize, int2 targetSize, RealEvaluation pixelAspect, RealEvaluation uiScale, RealEvaluation dpi, RealEvaluation referenceDpi) {
            return LayoutNumericKernel<RealEvaluation, RuntimeNumericOperations>.Resolve(scalingMode, referenceSize, targetSize, pixelAspect, uiScale, dpi, referenceDpi, new RuntimeNumericOperations());
        }

        /// <summary>Resolves the same formulas under an explicit cook policy without changing host precision.</summary>
        public static ViewportLayoutMetrics Resolve(int scalingMode, int2 referenceSize, int2 targetSize, double pixelAspect, double uiScale, double dpi, double referenceDpi, NumericPrecisionPolicy policy) {
            CookNumericOperations math = new CookNumericOperations(policy);
            return LayoutNumericKernel<double, CookNumericOperations>.Resolve(scalingMode, referenceSize, targetSize, math.Number(pixelAspect), math.Number(uiScale), math.Number(dpi), math.Number(referenceDpi), math);
        }
    }
}
