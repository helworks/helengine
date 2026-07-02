namespace helengine {
    /// <summary>
    /// Stores one timed <see cref="float4"/> keyframe for rotation animation tracks.
    /// </summary>
    public class RotationKeyframeAsset {
        /// <summary>
        /// Gets or sets the stable editor-only frame identifier used to target this keyframe from platform overrides.
        /// </summary>
        public string FrameId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the keyframe time in seconds.
        /// </summary>
        public float Time { get; set; }

        /// <summary>
        /// Gets or sets the quaternion value evaluated at <see cref="Time"/>.
        /// </summary>
        public float4 Value { get; set; }

        /// <summary>
        /// Gets or sets the interpolation used to reach the next keyframe.
        /// </summary>
        public AnimationInterpolationMode InterpolationMode { get; set; }

        /// <summary>
        /// Initializes an empty rotation keyframe for serializers and object initializers.
        /// </summary>
        public RotationKeyframeAsset() { }

        /// <summary>
        /// Initializes one typed rotation keyframe with time, value, and interpolation data.
        /// </summary>
        /// <param name="time">Keyframe time in seconds.</param>
        /// <param name="value">Quaternion value stored by the keyframe.</param>
        /// <param name="interpolationMode">Interpolation used to reach the next keyframe.</param>
        public RotationKeyframeAsset(float time, float4 value, AnimationInterpolationMode interpolationMode) {
            Time = time;
            Value = value;
            InterpolationMode = interpolationMode;
        }
    }
}
