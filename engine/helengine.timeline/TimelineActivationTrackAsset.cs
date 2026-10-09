namespace helengine.timeline {
    /// <summary>
    /// Marks the intervals during which the slot's target is active; outside every clip the target is inactive while the
    /// timeline plays. Activation clips have no blend ramps and no clip-in.
    /// </summary>
    public class TimelineActivationTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Activation"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Activation;
            }
        }

        /// <summary>
        /// Gets or sets the active intervals in start order.
        /// </summary>
        public List<TimelineClipAsset> Clips { get; set; } = new List<TimelineClipAsset>();

        /// <summary>
        /// Gets the number of active intervals.
        /// </summary>
        public override int ClipCount {
            get {
                return Clips.Count;
            }
        }

        /// <summary>
        /// Returns one active interval.
        /// </summary>
        /// <param name="index">Zero-based clip index.</param>
        /// <returns>The clip at that index.</returns>
        public override TimelineClipAsset GetClip(int index) {
            return Clips[index];
        }
    }
}
