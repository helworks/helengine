namespace helengine {
    /// <summary>Runs shared geometry and layout kernels using one request's selected widths in the wide authoring host.</summary>
    public readonly struct CookNumericOperations : INumericOperations<double> {
        /// <summary>Evaluator owned by the cook request.</summary>
        readonly NumericArithmetic Arithmetic;
        /// <summary>Captures the immutable build policy.</summary>
        public CookNumericOperations(NumericPrecisionPolicy policy) { Arithmetic = new NumericArithmetic(policy); }
        /// <inheritdoc/>
        public double Read(Real value) { return Number(Arithmetic.Store(value.ToDouble())); }
        /// <inheritdoc/>
        public Real Write(double value) { return Real.FromDouble(Arithmetic.Store(value)); }
        /// <inheritdoc/>
        public double Number(double value) { return Arithmetic.Policy.ArithmeticBits == 32 ? (double)(float)value : value; }
        /// <inheritdoc/>
        public double Add(double left, double right) { return Arithmetic.Add(left, right); }
        /// <inheritdoc/>
        public double Subtract(double left, double right) { return Arithmetic.Subtract(left, right); }
        /// <inheritdoc/>
        public double Multiply(double left, double right) { return Arithmetic.Multiply(left, right); }
        /// <inheritdoc/>
        public double Divide(double left, double right) { return Arithmetic.Divide(left, right); }
        /// <inheritdoc/>
        public double Sqrt(double value) { return Arithmetic.Sqrt(value); }
        /// <inheritdoc/>
        public double Abs(double value) { return Math.Abs(value); }
        /// <inheritdoc/>
        public double Min(double left, double right) { return Math.Min(left, right); }
        /// <inheritdoc/>
        public bool LessOrEqual(double left, double right) { return left <= right; }
        /// <inheritdoc/>
        public bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        /// <inheritdoc/>
        public int RoundToInt(double value) { return (int)Math.Round(value); }
        /// <inheritdoc/>
        public double SnapMagnitude(double value, TextNativePixelMode mode) {
            if (value <= 0d) { return value; }
            double snapped;
            if (mode == TextNativePixelMode.RoundNearest) { snapped = Math.Round(value, MidpointRounding.AwayFromZero); }
            else if (mode == TextNativePixelMode.RoundDown) { snapped = Math.Floor(value); }
            else if (mode == TextNativePixelMode.RoundUp) { snapped = Math.Ceiling(value); }
            else { throw new ArgumentOutOfRangeException(nameof(mode), "Effect snapping requires a native-pixel mode."); }
            return Math.Max(1d, snapped);
        }
    }
}
