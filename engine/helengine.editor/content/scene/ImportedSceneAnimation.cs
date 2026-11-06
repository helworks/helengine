namespace helengine.editor {
    /// <summary>
    /// One imported animation: a set of per-node channels on a shared timeline measured in seconds.
    /// </summary>
    public sealed class ImportedSceneAnimation {
        /// <summary>
        /// Creates the immutable ImportedSceneAnimation from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="name">The animation name.</param>
        /// <param name="durationSeconds">The animation length in seconds.</param>
        /// <param name="channels">The per-node channels.</param>
        public ImportedSceneAnimation(string name, double durationSeconds, ImportedSceneAnimationChannel[] channels) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DurationSeconds = durationSeconds;
            Channels = channels ?? throw new ArgumentNullException(nameof(channels));
        }

        /// <summary>
        /// Gets the animation name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the animation length in seconds.
        /// </summary>
        public double DurationSeconds { get; }

        /// <summary>
        /// Gets the per-node channels.
        /// </summary>
        public ImportedSceneAnimationChannel[] Channels { get; }
    }
}
