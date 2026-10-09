namespace helengine.timeline {
    /// <summary>
    /// A scalar function of time that <see cref="TimelineCurveLinearizer"/> can approximate with straight lines.
    /// </summary>
    interface ITimelineScalarFunction {
        /// <summary>
        /// Evaluates the function.
        /// </summary>
        /// <param name="seconds">Time to sample.</param>
        /// <returns>The value.</returns>
        double Evaluate(double seconds);
    }
}
