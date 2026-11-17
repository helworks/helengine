namespace helengine.painting {
    /// <summary>Validates public painting data before it can mutate a session or allocate GPU resources.</summary>
    public static class PaintContract {
        /// <summary>Returns zero for a supported valid brush, one for invalid data, or two for unsupported behavior.</summary>
        public static int ValidateBrush([NativeNoEscape] PaintBrushDefinition brush) {
            if (brush == null) { return 1; }
            if (brush.IsSurfaceFill) {
                return brush.Version==1 && brush.Diameter==1 && brush.Blur==0 && !brush.Eraser
                    && IsChannel(brush.Red) && IsChannel(brush.Green) && IsChannel(brush.Blue) && IsChannel(brush.Opacity) ? 0 : 1;
            }
            if ((brush.Version != 1 && brush.Version != 2) || brush.Id != "cozy.round") { return 2; }
            if (!double.IsFinite(brush.Diameter) || brush.Diameter < 0.2 || brush.Diameter > (brush.Version == 1 ? 12 : 20)) { return 1; }
            if (!IsChannel(brush.Red) || !IsChannel(brush.Green) || !IsChannel(brush.Blue) || !IsChannel(brush.Opacity)) { return 1; }
            if (!IsChannel(brush.Blur) || (brush.Version == 1 && brush.Blur != 0)) { return 1; }
            return 0;
        }

        /// <summary>Checks a finite normalized channel before premultiplied raster operations.</summary>
        static bool IsChannel(double value) { return double.IsFinite(value) && value >= 0 && value <= 1; }

        /// <summary>Admits finite legacy input with normalized pressure and bounded document coordinates.</summary>
        public static int ValidateSample(PaintSample sample) {
            return double.IsFinite(sample.X) && double.IsFinite(sample.Y) && Math.Abs(sample.X)<=32768 && Math.Abs(sample.Y)<=32768
                && IsChannel(sample.Pressure) && double.IsFinite(sample.Time) && double.IsFinite(sample.Altitude)
                && double.IsFinite(sample.Azimuth) && sample.Index>=0 ? 0 : 1;
        }
    }
}
