namespace helengine.timeline {
    /// <summary>
    /// Plays animation clip assets on the slot's entity; overlapping clips inside blend ramps cross-fade.
    /// </summary>
    public class TimelineAnimationTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Animation"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Animation;
            }
        }

        /// <summary>
        /// Gets or sets the animation placements in start order.
        /// </summary>
        public List<TimelineAnimationClipAsset> Clips { get; set; } = new List<TimelineAnimationClipAsset>();

        /// <summary>
        /// Gets the animation placements through the shared clip base (the same list instance), or null when the list is missing.
        /// </summary>
        public override IReadOnlyList<TimelineClipAsset> ClipView {
            get {
                return Clips;
            }
        }
    }
}
