namespace helengine.timeline {
    /// <summary>
    /// One animation of a flattened animation track in root-timeline seconds. <see cref="Speed"/> already includes the
    /// speed of enclosing nested timelines.
    /// </summary>
    public sealed class FlattenedAnimationClip {
        /// <summary>
        /// Gets or sets when the animation starts.
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets when the animation stops advancing.
        /// </summary>
        public double EndSeconds { get; set; }

        /// <summary>
        /// Gets or sets the animation time shown at <see cref="StartSeconds"/>.
        /// </summary>
        public double ClipInSeconds { get; set; }

        /// <summary>
        /// Gets or sets animation seconds per root-timeline second.
        /// </summary>
        public double Speed { get; set; } = 1;

        /// <summary>
        /// Gets or sets the animation clip asset.
        /// </summary>
        public SceneAssetReference Animation { get; set; }
    }
}
