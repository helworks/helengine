namespace helengine.timeline {
    /// <summary>
    /// Merges several segment lists that drive the same target (for example an outer track and a nested timeline's track
    /// on the same slot and channel) into one non-overlapping list. At every moment the source whose current segment —
    /// the one playing, or the last one it finished and now holds — started last wins; among equal starts the source
    /// listed later wins. Runs that a source wins with the same segment are kept whole, so curves are not fragmented.
    /// </summary>
    static class TimelineCurveMerger {
        /// <summary>
        /// Merges sources given in priority order (later sources win ties).
        /// </summary>
        /// <param name="sources">Sorted, non-overlapping segment lists.</param>
        /// <returns>The merged list.</returns>
        public static List<FlattenedCurveSegment> Merge(List<List<FlattenedCurveSegment>> sources) {
            if (sources.Count == 1) {
                return sources[0];
            }

            List<double> breakpoints = CollectBreakpoints(sources);
            List<FlattenedCurveSegment> output = new List<FlattenedCurveSegment>();
            TimelineCurveMergePiece current = null;
            for (int index = 0; index + 1 < breakpoints.Count; index++) {
                double start = breakpoints[index];
                double end = breakpoints[index + 1];
                TimelineCurveMergePiece winner = FindWinner(sources, start, end);
                if (winner == null) {
                    Flush(sources, current, output);
                    current = null;
                } else if (current != null && current.Source == winner.Source && current.Segment == winner.Segment && current.Held == winner.Held && current.EndSeconds >= start - TimelineValidator.TimeTolerance) {
                    current.EndSeconds = end;
                } else {
                    winner.Implicit = winner.Held && current != null && !current.Held && current.Source == winner.Source && current.Segment == winner.Segment;
                    Flush(sources, current, output);
                    current = winner;
                }
            }

            Flush(sources, current, output);
            return output;
        }

        /// <summary>
        /// Collects every segment start and end, sorted, without near-duplicates.
        /// </summary>
        /// <param name="sources">Sources to scan.</param>
        /// <returns>Sorted breakpoints.</returns>
        static List<double> CollectBreakpoints(List<List<FlattenedCurveSegment>> sources) {
            List<double> all = new List<double>();
            for (int source = 0; source < sources.Count; source++) {
                for (int index = 0; index < sources[source].Count; index++) {
                    all.Add(sources[source][index].StartSeconds);
                    all.Add(sources[source][index].EndSeconds);
                }
            }

            all.Sort();
            List<double> unique = new List<double>();
            for (int index = 0; index < all.Count; index++) {
                if (unique.Count == 0 || all[index] - unique[unique.Count - 1] > TimelineValidator.TimeTolerance) {
                    unique.Add(all[index]);
                }
            }
            return unique;
        }

        /// <summary>
        /// Finds which source drives the target between two consecutive breakpoints.
        /// </summary>
        /// <param name="sources">Sources in priority order.</param>
        /// <param name="start">Interval start.</param>
        /// <param name="end">Interval end.</param>
        /// <returns>The winning run, or null when no source has started.</returns>
        static TimelineCurveMergePiece FindWinner(List<List<FlattenedCurveSegment>> sources, double start, double end) {
            double middle = (start + end) / 2;
            TimelineCurveMergePiece winner = null;
            double winnerStart = double.NegativeInfinity;
            for (int source = 0; source < sources.Count; source++) {
                List<FlattenedCurveSegment> segments = sources[source];
                int found = -1;
                for (int index = 0; index < segments.Count; index++) {
                    if (segments[index].StartSeconds <= middle) {
                        found = index;
                    }
                }
                if (found < 0 || segments[found].StartSeconds < winnerStart) {
                    continue;
                }

                winnerStart = segments[found].StartSeconds;
                winner = new TimelineCurveMergePiece {
                    Source = source,
                    Segment = found,
                    Held = middle >= segments[found].EndSeconds,
                    StartSeconds = start,
                    EndSeconds = end
                };
            }
            return winner;
        }

        /// <summary>
        /// Emits a finished run: the trimmed curve, a constant hold, or nothing for a hold the runtime performs itself.
        /// </summary>
        /// <param name="sources">Sources in priority order.</param>
        /// <param name="piece">Run to emit (ignored when null).</param>
        /// <param name="output">Merged list.</param>
        static void Flush(List<List<FlattenedCurveSegment>> sources, TimelineCurveMergePiece piece, List<FlattenedCurveSegment> output) {
            if (piece == null || piece.Implicit) {
                return;
            }

            FlattenedCurveSegment segment = sources[piece.Source][piece.Segment];
            if (piece.Held) {
                output.Add(FlattenedCurveSegment.Constant(piece.StartSeconds, piece.EndSeconds, segment.EndValue));
            } else {
                output.Add(segment.Trim(piece.StartSeconds, piece.EndSeconds));
            }
        }
    }
}
