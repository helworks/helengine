namespace helengine.timeline {
    /// <summary>
    /// One span of a flattened transform or value channel, in seconds of the root timeline. Between
    /// <see cref="StartSeconds"/> and <see cref="EndSeconds"/> the value follows the catalog curve <see cref="Curve"/>
    /// from <see cref="From"/> to <see cref="To"/>, restricted to the progress range
    /// [<see cref="ProgressStart"/>, <see cref="ProgressEnd"/>]: a segment cut by a nested-timeline window or by a
    /// higher-priority track keeps its original curve exactly instead of being resampled.
    /// </summary>
    public sealed class FlattenedCurveSegment {
        /// <summary>
        /// Largest progress difference still treated as the curve's own start or end (absorbs rounding of trimmed times).
        /// </summary>
        const double ProgressTolerance = 1e-9;

        /// <summary>
        /// Gets or sets the root-timeline time at which the span starts.
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets the root-timeline time at which the span ends.
        /// </summary>
        public double EndSeconds { get; set; }

        /// <summary>
        /// Gets or sets the value at curve progress 0.
        /// </summary>
        public double From { get; set; }

        /// <summary>
        /// Gets or sets the value at curve progress 1.
        /// </summary>
        public double To { get; set; }

        /// <summary>
        /// Gets or sets the catalog curve id.
        /// </summary>
        public string Curve { get; set; } = CurveCatalog.Linear;

        /// <summary>
        /// Gets or sets the curve progress at <see cref="StartSeconds"/> (0 unless the segment was cut).
        /// </summary>
        public double ProgressStart { get; set; }

        /// <summary>
        /// Gets or sets the curve progress at <see cref="EndSeconds"/> (1 unless the segment was cut).
        /// </summary>
        public double ProgressEnd { get; set; } = 1;

        /// <summary>
        /// Gets whether the segment covers its whole curve.
        /// </summary>
        public bool IsWholeCurve {
            get {
                return ProgressStart <= ProgressTolerance && ProgressEnd >= 1 - ProgressTolerance;
            }
        }

        /// <summary>
        /// Gets the value at <see cref="StartSeconds"/>.
        /// </summary>
        public double StartValue {
            get {
                return ValueAtProgress(ProgressStart);
            }
        }

        /// <summary>
        /// Gets the value at <see cref="EndSeconds"/>, which the channel holds afterwards.
        /// </summary>
        public double EndValue {
            get {
                return ValueAtProgress(ProgressEnd);
            }
        }

        /// <summary>
        /// Creates a constant span.
        /// </summary>
        /// <param name="startSeconds">Start time.</param>
        /// <param name="endSeconds">End time.</param>
        /// <param name="value">Value held across the span.</param>
        /// <returns>The segment.</returns>
        public static FlattenedCurveSegment Constant(double startSeconds, double endSeconds, double value) {
            return new FlattenedCurveSegment { StartSeconds = startSeconds, EndSeconds = endSeconds, From = value, To = value };
        }

        /// <summary>
        /// Evaluates the segment at a root-timeline time, clamped to the span.
        /// </summary>
        /// <param name="seconds">Time to sample.</param>
        /// <returns>The value.</returns>
        public double Evaluate(double seconds) {
            if (EndSeconds <= StartSeconds || seconds >= EndSeconds) {
                return EndValue;
            } else if (seconds <= StartSeconds) {
                return StartValue;
            }

            double fraction = (seconds - StartSeconds) / (EndSeconds - StartSeconds);
            return ValueAtProgress(ProgressStart + (ProgressEnd - ProgressStart) * fraction);
        }

        /// <summary>
        /// Evaluates the curve at a progress value.
        /// </summary>
        /// <param name="progress">Curve progress.</param>
        /// <returns>The value.</returns>
        public double ValueAtProgress(double progress) {
            return From + (To - From) * CurveCatalog.Evaluate(Curve, progress);
        }

        /// <summary>
        /// Returns the part of the segment between two times (clamped to the span), keeping the original curve through
        /// the progress range.
        /// </summary>
        /// <param name="startSeconds">Start of the part.</param>
        /// <param name="endSeconds">End of the part.</param>
        /// <returns>The trimmed copy.</returns>
        public FlattenedCurveSegment Trim(double startSeconds, double endSeconds) {
            double start = startSeconds < StartSeconds ? StartSeconds : startSeconds;
            double end = endSeconds > EndSeconds ? EndSeconds : endSeconds;
            return new FlattenedCurveSegment {
                StartSeconds = start,
                EndSeconds = end,
                From = From,
                To = To,
                Curve = Curve,
                ProgressStart = ProgressAt(start),
                ProgressEnd = ProgressAt(end)
            };
        }

        /// <summary>
        /// Returns a copy moved into another time frame: <c>t' = offset + t * scale</c>.
        /// </summary>
        /// <param name="offsetSeconds">Time added after scaling.</param>
        /// <param name="scale">Time scale (positive).</param>
        /// <returns>The mapped copy.</returns>
        public FlattenedCurveSegment Map(double offsetSeconds, double scale) {
            return new FlattenedCurveSegment {
                StartSeconds = offsetSeconds + StartSeconds * scale,
                EndSeconds = offsetSeconds + EndSeconds * scale,
                From = From,
                To = To,
                Curve = Curve,
                ProgressStart = ProgressStart,
                ProgressEnd = ProgressEnd
            };
        }

        /// <summary>
        /// Converts a time inside the span to curve progress.
        /// </summary>
        /// <param name="seconds">Time inside the span.</param>
        /// <returns>The progress.</returns>
        double ProgressAt(double seconds) {
            if (EndSeconds <= StartSeconds) {
                return ProgressEnd;
            }

            double fraction = (seconds - StartSeconds) / (EndSeconds - StartSeconds);
            return ProgressStart + (ProgressEnd - ProgressStart) * fraction;
        }
    }
}
