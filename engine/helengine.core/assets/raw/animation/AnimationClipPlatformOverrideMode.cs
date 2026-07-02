namespace helengine {
    /// <summary>
    /// Selects how one platform resolves a clip relative to the authored base timeline.
    /// </summary>
    public enum AnimationClipPlatformOverrideMode {
        /// <summary>
        /// Uses the base clip without any platform-specific changes.
        /// </summary>
        InheritBase = 0,

        /// <summary>
        /// Replaces the entire clip with one platform-authored timeline.
        /// </summary>
        ReplaceWholeClip = 1,

        /// <summary>
        /// Merges one platform-authored set of frame overrides and inserted frames onto the base clip.
        /// </summary>
        OverrideFrames = 2
    }
}
