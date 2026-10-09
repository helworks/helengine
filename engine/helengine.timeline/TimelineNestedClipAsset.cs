namespace helengine.timeline {
    /// <summary>
    /// A nested timeline placed as a clip: either a reference to a <c>.htimeline</c> asset or an inline definition, played
    /// from the clip start (skipping <see cref="TimelineClipAsset.ClipInSeconds"/> of inner time) at <see cref="Speed"/>,
    /// with each inner slot mapped to a slot of the containing timeline. Cookers flatten nested clips away.
    /// </summary>
    public class TimelineNestedClipAsset : TimelineClipAsset {
        /// <summary>
        /// Gets or sets the referenced timeline asset; null when <see cref="Definition"/> is used instead.
        /// </summary>
        public SceneAssetReference Timeline { get; set; }

        /// <summary>
        /// Gets or sets the inline timeline definition; null when <see cref="Timeline"/> is used instead.
        /// </summary>
        public TimelineAsset Definition { get; set; }

        /// <summary>
        /// Gets or sets the inner-time speed multiplier; two plays the nested timeline twice as fast.
        /// </summary>
        public double Speed { get; set; } = 1;

        /// <summary>
        /// Gets or sets the mapping from every inner slot to a slot of the containing timeline.
        /// </summary>
        public List<TimelineSlotMappingAsset> SlotMappings { get; set; } = new List<TimelineSlotMappingAsset>();
    }
}
