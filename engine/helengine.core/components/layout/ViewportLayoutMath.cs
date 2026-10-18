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
        public static ViewportLayoutMetrics Resolve(int scalingMode, int2 referenceSize, int2 targetSize, double pixelAspect, double uiScale, double dpi, double referenceDpi) {
            if (targetSize.X <= 0 || targetSize.Y <= 0 || referenceSize.X <= 0 || referenceSize.Y <= 0) {
                throw new InvalidOperationException("Viewport layout requires positive reference and target dimensions.");
            }
            if (!(pixelAspect > 0d) || double.IsInfinity(pixelAspect)) {
                throw new InvalidOperationException("Viewport layout requires a finite positive pixel aspect.");
            }
            ViewportLayoutMetrics result = new ViewportLayoutMetrics {
                ScaleX = 1d, ScaleY = 1d, GlyphScale = 1d,
                Origin = new float2(0f, 0f), AnchorSize = targetSize, PixelAspect = pixelAspect
            };
            if (scalingMode == ViewportComponent.NoScalingMode) {
                return result;
            } else if (scalingMode == ViewportComponent.DensityScalingMode) {
                if (!(dpi > 0d) || double.IsInfinity(dpi) || !(referenceDpi > 0d) || double.IsInfinity(referenceDpi) || !(uiScale > 0d) || double.IsInfinity(uiScale)) {
                    throw new InvalidOperationException("Density layout requires finite positive output DPI, reference DPI and UI scale.");
                }
                result.ScaleX = dpi / referenceDpi * uiScale;
                result.ScaleY = result.ScaleX;
                result.GlyphScale = result.ScaleX;
                return result;
            } else if (scalingMode == ViewportComponent.ReferenceCanvasScalingMode) {
                result.AnchorSize = DisplayAspectFitUtils.ResolveFittedSize(targetSize.X, targetSize.Y, referenceSize.X, referenceSize.Y, pixelAspect);
                result.Origin = new float2((float)((targetSize.X - result.AnchorSize.X) * 0.5d), (float)((targetSize.Y - result.AnchorSize.Y) * 0.5d));
            } else if (scalingMode != ViewportComponent.ReferenceCanvasStretchScalingMode) {
                throw new InvalidOperationException("Unknown viewport scaling mode.");
            }
            result.ScaleX = (double)result.AnchorSize.X / referenceSize.X;
            result.ScaleY = (double)result.AnchorSize.Y / referenceSize.Y;
            result.GlyphScale = scalingMode == ViewportComponent.ReferenceCanvasStretchScalingMode
                ? result.ScaleY : DisplayAspectFitUtils.ResolveGlyphScale(result.ScaleX, result.ScaleY, pixelAspect);
            return result;
        }
    }
}
