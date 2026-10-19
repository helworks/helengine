namespace helengine.editor.tests.testing {
    /// <summary>Preserves xUnit assertions and their tolerances while explicitly transporting configurable scalar comparisons as doubles.</summary>
    public abstract class NumericAssert : Xunit.Assert {
        /// <summary>Retains xUnit's original fixed double comparison overload without routing primitive values through Real storage.</summary>
        /// <param name="expected">Expected fixed-width value.</param>
        /// <param name="actual">Actual fixed-width value.</param>
        /// <param name="precision">Original assertion precision or tolerance.</param>
        public new static void Equal(double expected, double actual, int precision) { Xunit.Assert.Equal(expected, actual, precision); }

        /// <summary>Retains xUnit's original fixed double comparison overload without routing primitive values through Real storage.</summary>
        /// <param name="expected">Expected fixed-width value.</param>
        /// <param name="actual">Actual fixed-width value.</param>
        /// <param name="tolerance">Original assertion precision or tolerance.</param>
        public new static void Equal(double expected, double actual, double tolerance) { Xunit.Assert.Equal(expected, actual, tolerance); }

        /// <summary>Retains xUnit's original fixed float comparison overload without routing primitive values through Real storage.</summary>
        /// <param name="expected">Expected fixed-width value.</param>
        /// <param name="actual">Actual fixed-width value.</param>
        /// <param name="precision">Original assertion precision or tolerance.</param>
        public new static void Equal(float expected, float actual, int precision) { Xunit.Assert.Equal(expected, actual, precision); }

        /// <summary>Retains xUnit's original fixed float comparison overload without routing primitive values through Real storage.</summary>
        /// <param name="expected">Expected fixed-width value.</param>
        /// <param name="actual">Actual fixed-width value.</param>
        /// <param name="tolerance">Original assertion precision or tolerance.</param>
        public new static void Equal(float expected, float actual, float tolerance) { Xunit.Assert.Equal(expected, actual, tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(double expected, Real actual, int precision) { Xunit.Assert.Equal(expected, actual.ToDouble(), precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(double expected, Real actual, double tolerance) { Xunit.Assert.Equal(expected, actual.ToDouble(), tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(double expected, RealEvaluation actual, int precision) { Xunit.Assert.Equal(expected, actual.ToDouble(), precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(double expected, RealEvaluation actual, double tolerance) { Xunit.Assert.Equal(expected, actual.ToDouble(), tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(Real expected, double actual, int precision) { Xunit.Assert.Equal(expected.ToDouble(), actual, precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(Real expected, double actual, double tolerance) { Xunit.Assert.Equal(expected.ToDouble(), actual, tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(Real expected, Real actual, int precision) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(Real expected, Real actual, double tolerance) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(Real expected, RealEvaluation actual, int precision) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(Real expected, RealEvaluation actual, double tolerance) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(RealEvaluation expected, double actual, int precision) { Xunit.Assert.Equal(expected.ToDouble(), actual, precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(RealEvaluation expected, double actual, double tolerance) { Xunit.Assert.Equal(expected.ToDouble(), actual, tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(RealEvaluation expected, Real actual, int precision) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(RealEvaluation expected, Real actual, double tolerance) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), tolerance); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="precision">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(RealEvaluation expected, RealEvaluation actual, int precision) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), precision); }

        /// <summary>Compares engine numeric values without inserting an additional storage-rounding boundary.</summary>
        /// <param name="expected">Expected numeric value.</param>
        /// <param name="actual">Actual numeric value.</param>
        /// <param name="tolerance">Unchanged xUnit precision or tolerance.</param>
        public static void Equal(RealEvaluation expected, RealEvaluation actual, double tolerance) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble(), tolerance); }

        /// <summary>Retains xUnit's exact comparable ranges for non-engine values.</summary>
        /// <typeparam name="T">Original comparable value type.</typeparam>
        /// <param name="actual">Value to check.</param>
        /// <param name="low">Inclusive minimum.</param>
        /// <param name="high">Inclusive maximum.</param>
        public new static void InRange<T>(T actual, T low, T high) where T : IComparable { Xunit.Assert.InRange(actual, low, high); }

        /// <summary>Checks an engine numeric range without narrowing the stored or evaluation representation.</summary>
        /// <param name="actual">Value to check.</param>
        /// <param name="low">Inclusive minimum.</param>
        /// <param name="high">Inclusive maximum.</param>
        public static void InRange(Real actual, Real low, Real high) { Xunit.Assert.InRange(actual.ToDouble(), low.ToDouble(), high.ToDouble()); }

        /// <summary>Checks an engine numeric range without narrowing the stored or evaluation representation.</summary>
        /// <param name="actual">Value to check.</param>
        /// <param name="low">Inclusive minimum.</param>
        /// <param name="high">Inclusive maximum.</param>
        public static void InRange(RealEvaluation actual, RealEvaluation low, RealEvaluation high) { Xunit.Assert.InRange(actual.ToDouble(), low.ToDouble(), high.ToDouble()); }

        /// <summary>Checks mixed scalar expression bounds without committing them to stored Real values.</summary>
        /// <param name="actual">Stored value being checked.</param>
        /// <param name="low">Evaluated lower bound.</param>
        /// <param name="high">Evaluated upper bound.</param>
        public static void InRange(Real actual, RealEvaluation low, RealEvaluation high) { Xunit.Assert.InRange(actual.ToDouble(), low.ToDouble(), high.ToDouble()); }

        /// <summary>Checks a transient scalar against stored bounds without narrowing the actual value.</summary>
        /// <param name="actual">Evaluated value being checked.</param>
        /// <param name="low">Stored lower bound.</param>
        /// <param name="high">Stored upper bound.</param>
        public static void InRange(RealEvaluation actual, Real low, Real high) { Xunit.Assert.InRange(actual.ToDouble(), low.ToDouble(), high.ToDouble()); }
        /// <summary>Compares a scalar expression with its stored result through explicit canonical transport.</summary>
        /// <param name="expected">Evaluated expected value.</param>
        /// <param name="actual">Stored actual value.</param>
        public static void EqualEvaluation(RealEvaluation expected, Real actual) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble()); }

        /// <summary>Compares a stored expected value with an expression without adding a storage conversion.</summary>
        /// <param name="expected">Stored expected value.</param>
        /// <param name="actual">Evaluated actual value.</param>
        public static void EqualEvaluation(Real expected, RealEvaluation actual) { Xunit.Assert.Equal(expected.ToDouble(), actual.ToDouble()); }
    }
}
