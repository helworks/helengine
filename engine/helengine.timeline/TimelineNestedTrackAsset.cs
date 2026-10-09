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
        /// Gets the number of nested timeline clips.
        /// </summary>
        public override int ClipCount {
            get {
                return Clips.Count;
            }
        }

        /// <summary>
        /// Returns one nested timeline clip through the shared clip base.
        /// </summary>
        /// <param name="index">Zero-based clip index.</param>
        /// <returns>The clip at that index.</returns>
        public override TimelineClipAsset GetClip(int index) {
            return Clips[index];
        }
    }
}
