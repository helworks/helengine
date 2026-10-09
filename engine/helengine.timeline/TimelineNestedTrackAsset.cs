namespace helengine.timeline {
    /// <summary>
    /// Places nested timelines as clips. The track has no slot of its own; each clip maps the nested slots.
    /// </summary>
    public class TimelineNestedTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Timeline"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Timeline;
            }
        }

        /// <summary>
        /// Gets or sets the nested timeline clips in start order.
        /// </summary>
        public List<TimelineNestedClipAsset> Clips { get; set; } = new List<TimelineNestedClipAsset>();

        /// <summary>
        /// Gets the nested timeline clips through the shared clip base (the same list instance), or null when the list is missing.
        /// </summary>
        public override IReadOnlyList<TimelineClipAsset> ClipView {
            get {
                return Clips;
            }
        }
    }
}
