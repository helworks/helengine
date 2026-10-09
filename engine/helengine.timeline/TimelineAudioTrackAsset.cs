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
        /// Gets the number of audio clips.
        /// </summary>
        public override int ClipCount {
            get {
                return Clips.Count;
            }
        }

        /// <summary>
        /// Returns one audio clip through the shared clip base.
        /// </summary>
        /// <param name="index">Zero-based clip index.</param>
        /// <returns>The clip at that index.</returns>
        public override TimelineClipAsset GetClip(int index) {
            return Clips[index];
        }
    }
}
