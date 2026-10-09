namespace helengine.timeline {
    /// <summary>
    /// Describes how transform keyframes combine with the target's own transform.
    /// </summary>
    public enum TimelineTransformMode {
        /// <summary>
        /// Keyframe values replace the animated components of the local transform.
        /// </summary>
        Absolute = 0,

        /// <summary>
        /// Keyframe values are added to the transform the target had when the timeline bound it (scale multiplies).
        /// </summary>
        Offset = 1
    }
}
