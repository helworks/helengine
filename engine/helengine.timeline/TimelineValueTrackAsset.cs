namespace helengine.timeline {
    /// <summary>
    /// Animates one named value channel of the slot's target, such as <c>opacity</c>, <c>reveal</c>, <c>intensity</c> or
    /// <c>fov</c>. A game target receives the value through its receiver component; the video pipeline maps known channels
    /// to composition properties.
    /// </summary>
    public class TimelineValueTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Value"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Value;
            }
        }

        /// <summary>
        /// Gets or sets the channel name: lowercase letters, digits and underscores, starting with a letter.
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the clips in start order.
        /// </summary>
        public List<TimelineValueClipAsset> Clips { get; set; } = new List<TimelineValueClipAsset>();

        /// <summary>
        /// Gets the number of value clips.
        /// </summary>
        public override int ClipCount {
            get {
                return Clips.Count;
            }
        }

        /// <summary>
        /// Returns one value clip through the shared clip base.
        /// </summary>
        /// <param name="index">Zero-based clip index.</param>
        /// <returns>The clip at that index.</returns>
        public override TimelineClipAsset GetClip(int index) {
            return Clips[index];
        }
    }
}
