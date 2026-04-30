namespace helengine {
    /// <summary>
    /// Defines how one animation keyframe transitions into the next keyframe.
    /// </summary>
    public enum AnimationInterpolationMode {
        /// <summary>
        /// Holds the current keyframe value until the next keyframe time is reached.
        /// </summary>
        Step = 0,

        /// <summary>
        /// Interpolates linearly between the current keyframe and the next keyframe.
        /// </summary>
        Linear = 1
    }
}
