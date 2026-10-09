namespace helengine.timeline {
    /// <summary>
    /// One sound of the flattened timeline in root-timeline seconds. Sounds play at their natural rate: a nested
    /// timeline's speed moves and shortens the clip but does not change the pitch.
    /// </summary>
    public sealed class FlattenedAudioClip {
        /// <summary>
        /// Gets or sets the root slot of the emitter, or empty when the sound has none.
        /// </summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the sound starts.
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets when the sound is cut.
        /// </summary>
        public double EndSeconds { get; set; }

        /// <summary>
        /// Gets or sets the seconds of the asset skipped at the start.
        /// </summary>
        public double ClipInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the linear gain.
        /// </summary>
        public double Gain { get; set; } = 1;

        /// <summary>
        /// Gets or sets the authored fade-in duration (kept for consumers that can fade; the game runtime ignores it).
        /// </summary>
        public double EaseInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the authored fade-out duration (kept for consumers that can fade; the game runtime ignores it).
        /// </summary>
        public double EaseOutSeconds { get; set; }

        /// <summary>
        /// Gets or sets the audio asset.
        /// </summary>
        public SceneAssetReference Audio { get; set; }
    }
}
