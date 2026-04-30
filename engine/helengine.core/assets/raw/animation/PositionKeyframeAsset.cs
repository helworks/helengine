namespace helengine {
    /// <summary>
    /// Stores one timed <see cref="float3"/> keyframe for transform animation tracks.
    /// </summary>
    public class PositionKeyframeAsset {
        /// <summary>
        /// Gets or sets the keyframe time in seconds.
        /// </summary>
        public float Time { get; set; }

        /// <summary>
        /// Gets or sets the transform value evaluated at <see cref="Time"/>.
        /// </summary>
        public float3 Value { get; set; }

        /// <summary>
        /// Gets or sets the interpolation used to reach the next keyframe.
        /// </summary>
        public AnimationInterpolationMode InterpolationMode { get; set; }

        /// <summary>
        /// Initializes an empty keyframe for serializers and object initializers.
        /// </summary>
        public PositionKeyframeAsset() { }

        /// <summary>
        /// Initializes one typed transform keyframe with time, value, and interpolation data.
        /// </summary>
        /// <param name="time">Keyframe time in seconds.</param>
        /// <param name="value">Transform value stored by the keyframe.</param>
        /// <param name="interpolationMode">Interpolation used to reach the next keyframe.</param>
        public PositionKeyframeAsset(float time, float3 value, AnimationInterpolationMode interpolationMode) {
            Time = time;
            Value = value;
            InterpolationMode = interpolationMode;
        }
    }
}
