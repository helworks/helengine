using helengine.timeline.runtime;

namespace helengine.timeline {
    /// <summary>
    /// One flattened transform component or value channel of one root slot. All authored tracks (including tracks of
    /// nested timelines) that drive the same target are merged into it: blends are already cross-faded and, where two
    /// tracks overlap, the one whose current segment started last wins. Segments are sorted and never overlap; the
    /// channel holds the end value of a segment until the next one starts and has no value before the first.
    /// </summary>
    public sealed class FlattenedCurveTrack {
        /// <summary>
        /// Gets or sets <see cref="TimelineTrackKind.Transform"/> or <see cref="TimelineTrackKind.Value"/>.
        /// </summary>
        public TimelineTrackKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the root slot the track drives.
        /// </summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value channel name (value tracks only).
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the transform component (transform tracks only).
        /// </summary>
        public CookedTimelineTransformChannel TransformChannel { get; set; }

        /// <summary>
        /// Gets or sets how a transform track combines with the bound transform (transform tracks only).
        /// </summary>
        public TimelineTransformMode Mode { get; set; }

        /// <summary>
        /// Gets the segments in root-timeline seconds.
        /// </summary>
        public List<FlattenedCurveSegment> Segments { get; } = new List<FlattenedCurveSegment>();

        /// <summary>
        /// Samples the track the way players do: the segment containing the time, or the end value of the last segment
        /// before it.
        /// </summary>
        /// <param name="seconds">Root-timeline time.</param>
        /// <param name="value">The value when the track has one.</param>
        /// <returns>False before the first segment starts.</returns>
        public bool TryEvaluate(double seconds, out double value) {
            if (Segments.Count == 0 || Segments[0].StartSeconds > seconds) {
                value = 0;
                return false;
            }

            value = TimelinePiecewise.Evaluate(Segments, seconds);
            return true;
        }
    }
}
