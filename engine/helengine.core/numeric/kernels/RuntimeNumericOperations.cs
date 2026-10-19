namespace helengine {
    /// <summary>Uses the compiled Real evaluator for live geometry and layout without policy branches or double intermediates.</summary>
    public readonly struct RuntimeNumericOperations : INumericOperations<RealEvaluation> {
        /// <inheritdoc/>
        public RealEvaluation Read(Real value) { return value; }
        /// <inheritdoc/>
        public Real Write(RealEvaluation value) { return value; }
        /// <inheritdoc/>
        public RealEvaluation Number(double value) { return (RealEvaluation)value; }
        /// <inheritdoc/>
        public RealEvaluation Add(RealEvaluation left, RealEvaluation right) { return left + right; }
        /// <inheritdoc/>
        public RealEvaluation Subtract(RealEvaluation left, RealEvaluation right) { return left - right; }
        /// <inheritdoc/>
        public RealEvaluation Multiply(RealEvaluation left, RealEvaluation right) { return left * right; }
        /// <inheritdoc/>
        public RealEvaluation Divide(RealEvaluation left, RealEvaluation right) { return left / right; }
        /// <inheritdoc/>
        public RealEvaluation Sqrt(RealEvaluation value) { return RealMath.Sqrt(value); }
        /// <inheritdoc/>
        public RealEvaluation Abs(RealEvaluation value) { return RealMath.Abs(value); }
        /// <inheritdoc/>
        public RealEvaluation Min(RealEvaluation left, RealEvaluation right) { return RealMath.Min(left, right); }
        /// <inheritdoc/>
        public bool LessOrEqual(RealEvaluation left, RealEvaluation right) { return left <= right; }
        /// <inheritdoc/>
        public bool IsFinite(RealEvaluation value) { return RealEvaluation.IsFinite(value); }
        /// <inheritdoc/>
        public int RoundToInt(RealEvaluation value) { return (int)RealMath.Round(value); }
        /// <inheritdoc/>
        public RealEvaluation SnapMagnitude(RealEvaluation value, TextNativePixelMode mode) { return TextNativePixelEffectUtils.SnapOutlineScale(value, mode); }
    }
}
