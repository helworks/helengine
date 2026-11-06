namespace helengine.editor {
    /// <summary>
    /// One timed three-component keyframe (position or scale).
    /// </summary>
    public sealed class ImportedSceneVectorKey {
        /// <summary>
        /// Creates the immutable ImportedSceneVectorKey from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="timeSeconds">The key time in seconds.</param>
        /// <param name="value">The key value.</param>
        public ImportedSceneVectorKey(double timeSeconds, float3 value) {
            TimeSeconds = timeSeconds;
            Value = value;
        }

        /// <summary>
        /// Gets the key time in seconds.
        /// </summary>
        public double TimeSeconds { get; }

        /// <summary>
        /// Gets the key value.
        /// </summary>
        public float3 Value { get; }
    }
}
