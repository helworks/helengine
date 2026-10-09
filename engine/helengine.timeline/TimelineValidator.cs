namespace helengine.timeline {
    /// <summary>
    /// Checks a timeline definition before it is saved, cooked or compiled: identities and names, time ranges, keyframe
    /// order, clip overlaps (allowed only inside blend ramps), known slots, cues and curves, slot kinds per track kind,
    /// value ranges of known channels, and nesting (no cycles, at most <see cref="MaxNestingDepth"/> levels). Every
    /// problem is reported with a JSON path so a planner model can correct the exact field.
    /// </summary>
    /// <remarks>
    /// Slot kinds per track kind: transform, value and activation tracks accept any slot kind (entity, text, media, rect);
    /// animation tracks need an entity slot; audio tracks take no slot or an entity slot (the emitter); event and nested
    /// timeline tracks take no slot (nested clips map slots themselves, and a mapped inner slot must have the same kind as
    /// its outer slot).
    /// </remarks>
    public static class TimelineValidator {
        /// <summary>
        /// Deepest allowed nesting of timeline clips; the root timeline is level zero.
        /// </summary>
        public const int MaxNestingDepth = 8;

        /// <summary>
        /// Longest allowed timeline duration in seconds.
        /// </summary>
        public const double MaxDurationSeconds = 3600;

        /// <summary>
        /// Largest allowed linear gain of an audio clip.
        /// </summary>
        public const double MaxGain = 4;

        /// <summary>
        /// Largest allowed speed multiplier of animation and nested timeline clips.
        /// </summary>
        public const double MaxSpeed = 16;

        /// <summary>
        /// Tolerance in seconds used when comparing times, so values that only differ by floating-point rounding pass.
        /// </summary>
        public const double TimeTolerance = 1e-9;

        /// <summary>
        /// Validates a timeline and returns every problem found.
        /// </summary>
        /// <param name="timeline">Timeline to check.</param>
        /// <param name="resolver">Loads timelines referenced by nested clips; null leaves references unfollowed (their slot
        /// mappings and contents are then not checked).</param>
        /// <returns>Located diagnostics in document order; empty when the timeline is valid.</returns>
        public static IReadOnlyList<TimelineDiagnostic> Validate(TimelineAsset timeline, ITimelineAssetResolver resolver) {
            if (timeline == null) {
                throw new ArgumentNullException(nameof(timeline));
            }
            TimelineValidationPass pass = new TimelineValidationPass(resolver);
            return pass.Run(timeline);
        }

        /// <summary>
        /// Validates a timeline and throws when it has any problem.
        /// </summary>
        /// <param name="timeline">Timeline to check.</param>
        /// <param name="resolver">Loads timelines referenced by nested clips; may be null.</param>
        /// <exception cref="TimelineFormatException">The timeline has at least one problem.</exception>
        public static void EnsureValid(TimelineAsset timeline, ITimelineAssetResolver resolver) {
            IReadOnlyList<TimelineDiagnostic> diagnostics = Validate(timeline, resolver);
            if (diagnostics.Count > 0) {
                throw new TimelineFormatException(diagnostics);
            }
        }
    }
}
