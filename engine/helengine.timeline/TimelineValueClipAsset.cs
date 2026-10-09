namespace helengine.timeline {
    /// <summary>
    /// A value clip: scalar keyframes for the channel named by its track.
    /// </summary>
    public class TimelineValueClipAsset : TimelineClipAsset {
        /// <summary>
        /// Gets or sets the keyframes in clip time; at least one is required.
        /// </summary>
        public List<TimelineKeyframeAsset> Keyframes { get; set; } = new List<TimelineKeyframeAsset>();
    }
}
