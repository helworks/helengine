namespace helengine {
    /// <summary>Evaluates one cook request's explicit numeric policy independently of the editor's representation.</summary>
    public sealed class NumericArithmetic {
        /// <summary>Creates an isolated evaluator without changing global or ambient runtime precision.</summary>
        /// <param name="policy">Storage and arithmetic contract for the current cook request.</param>
        public NumericArithmetic(NumericPrecisionPolicy policy) {
            if (policy == null) { throw new ArgumentNullException(nameof(policy)); }
            else if (policy.StorageBits == 16 || policy.ArithmeticBits == 16) {
                throw new NotSupportedException($"Numeric policy '{policy.Identity}' has no implemented half-precision evaluator.");
            }
            Policy = policy;
        }

        /// <summary>Gets the immutable policy owned by this evaluator.</summary>
        public NumericPrecisionPolicy Policy { get; }

        /// <summary>Rounds transport data at a configurable stored destination.</summary>
        /// <param name="value">Value to commit to storage.</param>
        /// <returns>Canonical transport containing the selected-width stored value.</returns>
        public double Store(double value) { return Policy.StorageBits == 32 ? (double)(float)value : value; }

        /// <summary>Evaluates add at this request's arithmetic precision.</summary>
        /// <param name="left">First canonical transport operand.</param>
        /// <param name="right">Second canonical transport operand.</param>
        /// <returns>Operation result rounded to arithmetic precision, without a storage conversion.</returns>
        public double Add(double left, double right) {
            return Policy.ArithmeticBits == 32 ? (double)(float)((float)left + (float)right) : left + right;
        }

        /// <summary>Evaluates subtract at this request's arithmetic precision.</summary>
        /// <param name="left">First canonical transport operand.</param>
        /// <param name="right">Second canonical transport operand.</param>
        /// <returns>Operation result rounded to arithmetic precision, without a storage conversion.</returns>
        public double Subtract(double left, double right) {
            return Policy.ArithmeticBits == 32 ? (double)(float)((float)left - (float)right) : left - right;
        }

        /// <summary>Evaluates multiply at this request's arithmetic precision.</summary>
        /// <param name="left">First canonical transport operand.</param>
        /// <param name="right">Second canonical transport operand.</param>
        /// <returns>Operation result rounded to arithmetic precision, without a storage conversion.</returns>
        public double Multiply(double left, double right) {
            return Policy.ArithmeticBits == 32 ? (double)(float)((float)left * (float)right) : left * right;
        }

        /// <summary>Evaluates divide at this request's arithmetic precision.</summary>
        /// <param name="left">First canonical transport operand.</param>
        /// <param name="right">Second canonical transport operand.</param>
        /// <returns>Operation result rounded to arithmetic precision, without a storage conversion.</returns>
        public double Divide(double left, double right) {
            return Policy.ArithmeticBits == 32 ? (double)(float)((float)left / (float)right) : left / right;
        }

        /// <summary>Evaluates remainder at this request's arithmetic precision.</summary>
        /// <param name="left">First canonical transport operand.</param>
        /// <param name="right">Second canonical transport operand.</param>
        /// <returns>Operation result rounded to arithmetic precision, without a storage conversion.</returns>
        public double Remainder(double left, double right) {
            return Policy.ArithmeticBits == 32 ? (double)(float)((float)left % (float)right) : left % right;
        }

        /// <summary>Evaluates sqrt through the selected single or double arithmetic backend.</summary>
        /// <param name="value">Canonical transport operand.</param>
        /// <returns>Selected-width arithmetic result in canonical transport.</returns>
        public double Sqrt(double value) { return Policy.ArithmeticBits == 32 ? (double)MathF.Sqrt((float)value) : Math.Sqrt(value); }

        /// <summary>Evaluates sin through the selected single or double arithmetic backend.</summary>
        /// <param name="value">Canonical transport operand.</param>
        /// <returns>Selected-width arithmetic result in canonical transport.</returns>
        public double Sin(double value) { return Policy.ArithmeticBits == 32 ? (double)MathF.Sin((float)value) : Math.Sin(value); }

        /// <summary>Evaluates cos through the selected single or double arithmetic backend.</summary>
        /// <param name="value">Canonical transport operand.</param>
        /// <returns>Selected-width arithmetic result in canonical transport.</returns>
        public double Cos(double value) { return Policy.ArithmeticBits == 32 ? (double)MathF.Cos((float)value) : Math.Cos(value); }

        /// <summary>Evaluates atan2 without silently upgrading a single-precision cook request.</summary>
        /// <param name="y">Canonical vertical component.</param>
        /// <param name="x">Canonical horizontal component.</param>
        /// <returns>Selected-width angle in canonical transport.</returns>
        public double Atan2(double y, double x) { return Policy.ArithmeticBits == 32 ? (double)MathF.Atan2((float)y, (float)x) : Math.Atan2(y, x); }
    }
}
