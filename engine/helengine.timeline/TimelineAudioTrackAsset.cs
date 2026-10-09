namespace helengine.timeline {
    /// <summary>
    /// Plays audio clips. The slot is optional: empty plays the audio without a position, an entity slot makes the entity
    /// the emitter.
    /// </summary>
    public class TimelineAudioTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Audio"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Audio;
            }
        }

        /// <summary>
        /// Gets or sets the audio clips in start order.
        /// </summary>
        public List<TimelineAudioClipAsset> Clips { get; set; } = new List<TimelineAudioClipAsset>();

        /// <summary>
        /// Gets the audio clips through the shared clip base (the same list instance), or null when the list is missing.
        /// </summary>
        public override IReadOnlyList<TimelineClipAsset> ClipView {
            get {
                return Clips;
            }
        }
    }
}
