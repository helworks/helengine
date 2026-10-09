namespace helengine.timeline.runtime {
    /// <summary>
    /// Default <see cref="ITimelineAssetSource"/>: loads timeline audio and animation through the core's
    /// <see cref="RuntimeSceneAssetReferenceResolver"/>, the same path scene components use, so the assets are owned and
    /// released with the loaded scene.
    /// </summary>
    public sealed class SceneTimelineAssetSource : ITimelineAssetSource {
        /// <summary>
        /// Core resolver that performs the loads.
        /// </summary>
        readonly RuntimeSceneAssetReferenceResolver Resolver;

        /// <summary>
        /// Initializes the source over one core resolver.
        /// </summary>
        /// <param name="resolver">Resolver of the core that runs the player.</param>
        public SceneTimelineAssetSource([NativeRetainsBorrow] RuntimeSceneAssetReferenceResolver resolver) {
            if (resolver == null) {
                throw new ArgumentNullException(nameof(resolver));
            }

            Resolver = resolver;
        }

        /// <summary>
        /// Loads one audio asset through the scene resolver.
        /// </summary>
        /// <param name="reference">Reference stored in the cooked timeline.</param>
        /// <returns>The scene-owned audio asset.</returns>
        [NativeBorrowedReturn]
        public AudioAsset ResolveAudio(SceneAssetReference reference) {
            return Resolver.ResolveAudio(reference);
        }

        /// <summary>
        /// Loads one animation clip through the scene resolver.
        /// </summary>
        /// <param name="reference">Reference stored in the cooked timeline.</param>
        /// <returns>The scene-owned animation clip.</returns>
        [NativeBorrowedReturn]
        public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
            return Resolver.ResolveAnimationClip(reference);
        }
    }
}
