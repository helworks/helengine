namespace helengine {
    /// <summary>Evaluates engine numeric functions without unrequested double intermediates.</summary>
    public static class RealMath {
        /// <summary>Interpolates values using the configured arithmetic width for each operation.</summary>
        /// <param name="start">Initial value.</param>
        /// <param name="end">Final value.</param>
        /// <param name="amount">Interpolation amount in the selected evaluation format.</param>
        /// <returns>Transient result retained at evaluation precision.</returns>
        public static RealEvaluation Lerp(RealEvaluation start, RealEvaluation end, RealEvaluation amount) { return start + (end - start) * amount; }

        /// <summary>Evaluates a remainder at the selected arithmetic width.</summary>
        /// <param name="left">Dividend.</param>
        /// <param name="right">Divisor.</param>
        /// <returns>Selected-width arithmetic remainder.</returns>
        public static RealEvaluation Remainder(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(left.Value % right.Value); }

        /// <summary>Calculates an angle using selected-width inputs and output.</summary>
        /// <param name="y">Vertical component.</param>
        /// <param name="x">Horizontal component.</param>
        /// <returns>Angle retained at evaluation precision.</returns>
        public static RealEvaluation Atan2(RealEvaluation y, RealEvaluation x) { return new RealEvaluation(RealArithmeticMath.Atan2(y.Value, x.Value)); }

        /// <summary>Calculates a square root with the selected arithmetic backend.</summary>
        /// <param name="value">Input in the selected evaluation format.</param>
        /// <returns>Selected-width arithmetic result.</returns>
        public static RealEvaluation Sqrt(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Sqrt(value.Value)); }

        /// <summary>Calculates a sine with the selected arithmetic backend.</summary>
        /// <param name="value">Input in the selected evaluation format.</param>
        /// <returns>Selected-width arithmetic result.</returns>
        public static RealEvaluation Sin(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Sin(value.Value)); }

        /// <summary>Calculates a cosine with the selected arithmetic backend.</summary>
        /// <param name="value">Input in the selected evaluation format.</param>
        /// <returns>Selected-width arithmetic result.</returns>
        public static RealEvaluation Cos(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Cos(value.Value)); }
    }
}
