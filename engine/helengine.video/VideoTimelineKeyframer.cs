using helengine.timeline;

namespace helengine.video {
    /// <summary>
    /// Turns one composition property of an overlay timeline slot, given as a function of timeline time, into keyframes
    /// with catalog curves. The time span is cut at every instant where a contributing track changes; across each piece the
    /// property keeps the curve of the single segment driving it when that segment spans the piece whole, and is otherwise
    /// split into straight pieces until it stays within the tolerance (partial curves, products of several channels,
    /// clamped values). Jumps (activation steps, discontinuous segments) become a hold one millisecond before the jump.
    /// </summary>
    public static class VideoTimelineKeyframer {
        /// <summary>
        /// Longest hold inserted before a jump, in seconds.
        /// </summary>
        const double StepSeconds = 0.001;

        /// <summary>
        /// Points checked inside a piece to accept a curve.
        /// </summary>
        const int Checks = 8;

        /// <summary>
        /// Deepest subdivision of one piece.
        /// </summary>
        const int MaxDepth = 10;

        /// <summary>
        /// Builds the keyframes of one property across a span.
        /// </summary>
        /// <param name="value">Property value at a timeline instant.</param>
        /// <param name="tracks">Tracks the property depends on (curve choice).</param>
        /// <param name="breaks">Instants where the property may change or jump.</param>
        /// <param name="start">Span start in timeline seconds.</param>
        /// <param name="end">Span end in timeline seconds.</param>
        /// <param name="tolerance">Largest accepted error in property units.</param>
        /// <returns>Keyframes in increasing time covering the changes between <paramref name="start"/> and <paramref name="end"/>; runs of equal values are merged, so a constant property yields one keyframe.</returns>
        public static List<VideoTimelineSample> Build(Func<double, double> value, IReadOnlyList<FlattenedCurveTrack> tracks, IEnumerable<double> breaks, double start, double end, double tolerance) {
            List<double> cuts = new List<double>();
            foreach (double time in breaks.Where(time => time > start + 1e-9 && time < end - 1e-9).Append(start).Append(end).OrderBy(time => time)) {
                if (cuts.Count == 0 || time - cuts[^1] > 1e-9) {
                    cuts.Add(time);
                }
            }
            List<VideoTimelineSample> samples = new List<VideoTimelineSample>();
            double previousEnd = double.NaN;
            for (int index = 0; index + 1 < cuts.Count; index++) {
                double a = cuts[index], b = cuts[index + 1];
                double nudge = Math.Min(1e-10, (b - a) / 1000);
                double first = value(a + nudge), last = value(b - nudge);
                if (samples.Count > 0 && Math.Abs(previousEnd - first) > tolerance) {
                    double hold = a - Math.Min(StepSeconds, (a - samples[^1].Time) / 2);
                    samples.Add(new VideoTimelineSample(hold, previousEnd, CurveCatalog.Linear));
                }
                Fit(value, a, b, first, last, VideoTimelineSlotMotion.CurveOn(tracks, a, b), tolerance, 0, samples);
                previousEnd = last;
            }
            samples.Add(new VideoTimelineSample(end, previousEnd, CurveCatalog.Linear));
            return Simplify(samples, tolerance);
        }

        /// <summary>
        /// Emits the keyframe starting one piece, subdividing the piece into straight halves while the candidate curve
        /// misses the property by more than the tolerance.
        /// </summary>
        /// <param name="value">Property value at a timeline instant.</param>
        /// <param name="a">Piece start.</param>
        /// <param name="b">Piece end.</param>
        /// <param name="first">Value just after the start.</param>
        /// <param name="last">Value just before the end.</param>
        /// <param name="curve">Candidate curve.</param>
        /// <param name="tolerance">Largest accepted error.</param>
        /// <param name="depth">Subdivision depth.</param>
        /// <param name="samples">Keyframes being built.</param>
        static void Fit(Func<double, double> value, double a, double b, double first, double last, string curve, double tolerance, int depth, List<VideoTimelineSample> samples) {
            bool accepted = true;
            for (int check = 1; check < Checks && accepted; check++) {
                double progress = (double)check / Checks;
                double predicted = first + (last - first) * CurveCatalog.Evaluate(curve, progress);
                accepted = Math.Abs(value(a + (b - a) * progress) - predicted) <= tolerance;
            }
            if (accepted || depth >= MaxDepth) {
                samples.Add(new VideoTimelineSample(a, first, curve));
                return;
            }
            double middle = (a + b) / 2, center = value(middle);
            Fit(value, a, middle, first, center, CurveCatalog.Linear, tolerance, depth + 1, samples);
            Fit(value, middle, b, center, last, CurveCatalog.Linear, tolerance, depth + 1, samples);
        }

        /// <summary>
        /// Drops keyframes inside runs of equal values, which hold the value whatever their curve, and equal keyframes at
        /// either end (a composition animation holds its first and last values).
        /// </summary>
        /// <param name="samples">Keyframes in time order.</param>
        /// <param name="tolerance">Largest difference treated as equal.</param>
        /// <returns>Simplified keyframes.</returns>
        static List<VideoTimelineSample> Simplify(List<VideoTimelineSample> samples, double tolerance) {
            List<VideoTimelineSample> output = new List<VideoTimelineSample>();
            for (int index = 0; index < samples.Count; index++) {
                bool inside = index > 0 && index + 1 < samples.Count;
                if (inside && Math.Abs(samples[index].Value - output[^1].Value) <= tolerance * 0.01 && Math.Abs(samples[index + 1].Value - samples[index].Value) <= tolerance * 0.01) {
                    continue;
                }
                output.Add(samples[index]);
            }
            while (output.Count > 1 && Math.Abs(output[^1].Value - output[^2].Value) <= tolerance * 0.01) {
                output.RemoveAt(output.Count - 1);
            }
            while (output.Count > 1 && Math.Abs(output[0].Value - output[1].Value) <= tolerance * 0.01) {
                output.RemoveAt(0);
            }
            return output;
        }
    }
}
