namespace helengine.timeline {
    /// <summary>
    /// Approximates curves with straight lines by adaptive subdivision. The seconds variant serves the flattener (blend
    /// regions); the tick variant serves the cooker and guarantees the error bound at every integer tick, which is all
    /// the runtime ever samples.
    /// </summary>
    static class TimelineCurveLinearizer {
        /// <summary>
        /// Deepest subdivision of the seconds variant (2^14 pieces at most per span).
        /// </summary>
        const int MaxDepth = 14;

        /// <summary>
        /// Interior sample count used to measure the error of one straight piece in the seconds variant.
        /// </summary>
        const int ErrorSamples = 8;

        /// <summary>
        /// Appends linear segments approximating a function over a span of seconds within a tolerance (checked at
        /// <see cref="ErrorSamples"/> interior points of every piece).
        /// </summary>
        /// <param name="function">Function to approximate.</param>
        /// <param name="startSeconds">Span start.</param>
        /// <param name="endSeconds">Span end.</param>
        /// <param name="tolerance">Largest accepted absolute error.</param>
        /// <param name="output">List receiving the linear segments.</param>
        public static void LinearizeSeconds(ITimelineScalarFunction function, double startSeconds, double endSeconds, double tolerance, List<FlattenedCurveSegment> output) {
            Subdivide(function, startSeconds, function.Evaluate(startSeconds), endSeconds, function.Evaluate(endSeconds), tolerance, 0, output);
        }

        /// <summary>
        /// Returns the integer ticks at which to break a function sampled at ticks [startTick, endTick] into straight
        /// pieces so that every tick lies within the tolerance of its piece. The result starts with startTick and ends
        /// with endTick.
        /// </summary>
        /// <param name="values">Function values at ticks startTick..endTick (index 0 is startTick).</param>
        /// <param name="tolerance">Largest accepted absolute error at any tick.</param>
        /// <returns>Break indices into <paramref name="values"/>, ascending.</returns>
        public static List<int> LinearizeTicks(double[] values, double tolerance) {
            List<int> breaks = new List<int>();
            breaks.Add(0);
            SubdivideTicks(values, 0, values.Length - 1, tolerance, breaks);
            return breaks;
        }

        /// <summary>
        /// Recursive step of <see cref="LinearizeSeconds"/>.
        /// </summary>
        /// <param name="function">Function to approximate.</param>
        /// <param name="start">Piece start time.</param>
        /// <param name="startValue">Function value at the start.</param>
        /// <param name="end">Piece end time.</param>
        /// <param name="endValue">Function value at the end.</param>
        /// <param name="tolerance">Largest accepted error.</param>
        /// <param name="depth">Current depth.</param>
        /// <param name="output">List receiving the segments.</param>
        static void Subdivide(ITimelineScalarFunction function, double start, double startValue, double end, double endValue, double tolerance, int depth, List<FlattenedCurveSegment> output) {
            double worst = 0;
            for (int sample = 1; sample < ErrorSamples; sample++) {
                double fraction = (double)sample / ErrorSamples;
                double seconds = start + (end - start) * fraction;
                double error = Math.Abs(function.Evaluate(seconds) - (startValue + (endValue - startValue) * fraction));
                if (error > worst) {
                    worst = error;
                }
            }

            if (worst <= tolerance || depth >= MaxDepth) {
                output.Add(new FlattenedCurveSegment { StartSeconds = start, EndSeconds = end, From = startValue, To = endValue });
                return;
            }

            double middle = (start + end) / 2;
            double middleValue = function.Evaluate(middle);
            Subdivide(function, start, startValue, middle, middleValue, tolerance, depth + 1, output);
            Subdivide(function, middle, middleValue, end, endValue, tolerance, depth + 1, output);
        }

        /// <summary>
        /// Recursive step of <see cref="LinearizeTicks"/>: accepts the straight piece [first, last] when every tick lies
        /// within the tolerance, otherwise splits at the worst tick.
        /// </summary>
        /// <param name="values">Sampled values.</param>
        /// <param name="first">Index of the piece start.</param>
        /// <param name="last">Index of the piece end.</param>
        /// <param name="tolerance">Largest accepted error.</param>
        /// <param name="breaks">Break list receiving the piece end.</param>
        static void SubdivideTicks(double[] values, int first, int last, double tolerance, List<int> breaks) {
            int worstIndex = -1;
            double worst = tolerance;
            for (int index = first + 1; index < last; index++) {
                double fraction = (double)(index - first) / (last - first);
                double error = Math.Abs(values[index] - (values[first] + (values[last] - values[first]) * fraction));
                if (error > worst) {
                    worst = error;
                    worstIndex = index;
                }
            }

            if (worstIndex < 0) {
                breaks.Add(last);
                return;
            }

            SubdivideTicks(values, first, worstIndex, tolerance, breaks);
            SubdivideTicks(values, worstIndex, last, tolerance, breaks);
        }
    }
}
