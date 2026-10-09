namespace helengine.timeline.runtime {
    /// <summary>
    /// Resolves the audio and animation assets a cooked timeline references. A <see cref="TimelinePlayerComponent"/> asks
    /// once per referenced clip when it binds; by default it uses the core's scene asset resolver
    /// (<see cref="SceneTimelineAssetSource"/>), and tests or tools can supply their own.
    /// </summary>
    public interface ITimelineAssetSource {
        /// <summary>
        /// Resolves one audio asset.
        /// </summary>
        /// <param name="reference">Reference stored in the cooked timeline.</param>
        /// <returns>The loaded audio asset; implementations throw when it cannot be loaded.</returns>
        [NativeBorrowedReturn]
        AudioAsset ResolveAudio(SceneAssetReference reference);

        /// <summary>
        /// Resolves one animation clip asset.
        /// </summary>
        /// <param name="reference">Reference stored in the cooked timeline.</param>
        /// <returns>The loaded animation clip; implementations throw when it cannot be loaded.</returns>
        [NativeBorrowedReturn]
        AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference);
    }
}
