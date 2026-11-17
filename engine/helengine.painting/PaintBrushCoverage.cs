namespace helengine.painting {
    /// <summary>Portable reference for the inward smoothstep edge used by the round-brush GPU shader.</summary>
    public static class PaintBrushCoverage {
        /// <summary>Returns geometric coverage at a normalized radius, excluding opacity and pixel antialiasing.</summary>
        public static double Evaluate(double distance, double blur) {
            if (!double.IsFinite(distance) || distance < 0 || !double.IsFinite(blur) || blur < 0 || blur > 1) {
                throw new ArgumentOutOfRangeException("Brush coverage requires a nonnegative finite radius and normalized blur.");
            }
            if (distance >= 1) { return 0; }
            if (blur == 0 || distance <= 1 - blur) { return 1; }
            double t = (distance - (1 - blur)) / blur;
            return 1 - t * t * (3 - 2 * t);
        }
    }
}
