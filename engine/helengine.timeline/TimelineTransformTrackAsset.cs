namespace helengine.timeline {
    /// <summary>
    /// Animates the transform of the slot's target with keyframed clips.
    /// </summary>
    public class TimelineTransformTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Transform"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Transform;
            }
        }

        /// <summary>
        /// Gets or sets whether keyframes replace or offset the target's own transform.
        /// </summary>
        public TimelineTransformMode Mode { get; set; } = TimelineTransformMode.Absolute;

        /// <summary>
        /// Gets or sets the clips in start order.
        /// </summary>
        public List<TimelineTransformClipAsset> Clips { get; set; } = new List<TimelineTransformClipAsset>();

        /// <summary>
        /// Gets the number of transform clips.
        /// </summary>
        public override int ClipCount {
            get {
                return Clips.Count;
            }
        }

        /// <summary>
        /// Returns one transform clip through the shared clip base.
        /// </summary>
        /// <param name="index">Zero-based clip index.</param>
        /// <returns>The clip at that index.</returns>
        public override TimelineClipAsset GetClip(int index) {
            return Clips[index];
        }
    }
}
