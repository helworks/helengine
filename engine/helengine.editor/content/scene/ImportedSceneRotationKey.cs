namespace helengine.editor {
    /// <summary>
    /// One timed rotation keyframe.
    /// </summary>
    public sealed class ImportedSceneRotationKey {
        /// <summary>
        /// Creates the immutable ImportedSceneRotationKey from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="timeSeconds">The key time in seconds.</param>
        /// <param name="value">The key rotation as an xyzw quaternion.</param>
        public ImportedSceneRotationKey(double timeSeconds, float4 value) {
            TimeSeconds = timeSeconds;
            Value = value;
        }

        /// <summary>
        /// Gets the key time in seconds.
        /// </summary>
        public double TimeSeconds { get; }

        /// <summary>
        /// Gets the key rotation as an xyzw quaternion.
        /// </summary>
        public float4 Value { get; }
    }
}
