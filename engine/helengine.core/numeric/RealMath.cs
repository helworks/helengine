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
        /// <summary>Pi represented at the selected arithmetic width.</summary>
        public static RealEvaluation PI { get { return new RealEvaluation(RealArithmeticMath.PI); } }

        /// <summary>Calculates a tangent with the configured math implementation.</summary>
        /// <param name="value">Angle in radians.</param>
        public static RealEvaluation Tan(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Tan(value.Value)); }

        /// <summary>Calculates inverse sine without widening the configured arithmetic.</summary>
        /// <param name="value">Sine value.</param>
        public static RealEvaluation Asin(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Asin(value.Value)); }

        /// <summary>Calculates magnitude without changing numeric representation.</summary>
        /// <param name="value">Operand.</param>
        public static RealEvaluation Abs(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Abs(value.Value)); }

        /// <summary>Checks infinity in the selected arithmetic representation.</summary>
        /// <param name="value">Operand to classify.</param>
        public static bool IsPositiveInfinity(RealEvaluation value) { return value.Value == RealArithmeticScalar.PositiveInfinity; }
        /// <summary>Applies round through the selected arithmetic backend.</summary>
        /// <param name="value">Input value.</param>
        public static RealEvaluation Round(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Round(value.Value)); }

        /// <summary>Applies ceiling through the selected arithmetic backend.</summary>
        /// <param name="value">Input value.</param>
        public static RealEvaluation Ceiling(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Ceiling(value.Value)); }

        /// <summary>Applies floor through the selected arithmetic backend.</summary>
        /// <param name="value">Input value.</param>
        public static RealEvaluation Floor(RealEvaluation value) { return new RealEvaluation(RealArithmeticMath.Floor(value.Value)); }

        /// <summary>Selects the minimum using selected-width evaluation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation Min(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(RealArithmeticMath.Min(left.Value, right.Value)); }

        /// <summary>Retains exact integral pixel calculations without a floating conversion.</summary>
        /// <param name="left">First integer operand.</param>
        /// <param name="right">Second integer operand.</param>
        public static int Min(int left, int right) { return left < right ? left : right; }

        /// <summary>Selects the maximum using selected-width evaluation.</summary>
        /// <param name="left">First numeric operand.</param>
        /// <param name="right">Second numeric operand.</param>
        public static RealEvaluation Max(RealEvaluation left, RealEvaluation right) { return new RealEvaluation(RealArithmeticMath.Max(left.Value, right.Value)); }

        /// <summary>Retains exact integral pixel calculations without a floating conversion.</summary>
        /// <param name="left">First integer operand.</param>
        /// <param name="right">Second integer operand.</param>
        public static int Max(int left, int right) { return left > right ? left : right; }

        /// <summary>Returns the sign using the configured arithmetic backend.</summary>
        /// <param name="value">Operand to classify.</param>
        public static int Sign(RealEvaluation value) { return RealArithmeticMath.Sign(value.Value); }
        /// <summary>Rounds using an explicit midpoint rule at the selected evaluation precision.</summary>
        /// <param name="value">Input value.</param>
        /// <param name="mode">Midpoint rounding rule.</param>
        public static RealEvaluation Round(RealEvaluation value, MidpointRounding mode) { return new RealEvaluation(RealArithmeticMath.Round(value.Value, mode)); }
        /// <summary>Bounds a selected-width value without narrowing intermediate operands through storage.</summary>
        /// <param name="value">Evaluation value to bound.</param>
        /// <param name="minimum">Inclusive lower bound.</param>
        /// <param name="maximum">Inclusive upper bound.</param>
        public static RealEvaluation Clamp(RealEvaluation value, RealEvaluation minimum, RealEvaluation maximum) {
            if (minimum > maximum) { throw new ArgumentException("The minimum cannot exceed the maximum."); }
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
