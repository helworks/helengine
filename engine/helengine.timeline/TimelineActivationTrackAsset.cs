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
        /// Gets the active intervals through the shared clip base (the same list instance), or null when the list is missing.
        /// </summary>
        public override IReadOnlyList<TimelineClipAsset> ClipView {
            get {
                return Clips;
            }
        }
    }
}
