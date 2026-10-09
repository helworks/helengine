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
        /// Gets the number of animation placements.
        /// </summary>
        public override int ClipCount {
            get {
                return Clips.Count;
            }
        }

        /// <summary>
        /// Returns one animation placement through the shared clip base.
        /// </summary>
        /// <param name="index">Zero-based clip index.</param>
        /// <returns>The clip at that index.</returns>
        public override TimelineClipAsset GetClip(int index) {
            return Clips[index];
        }
    }
}
