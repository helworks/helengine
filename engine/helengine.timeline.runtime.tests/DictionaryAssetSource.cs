namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Asset source double that serves in-memory assets by relative path and counts resolutions.
    /// </summary>
    public sealed class DictionaryAssetSource : ITimelineAssetSource {
        /// <summary>
        /// Audio assets by relative path.
        /// </summary>
        readonly Dictionary<string, AudioAsset> AudioByPath = new Dictionary<string, AudioAsset>(StringComparer.Ordinal);

        /// <summary>
        /// Animation clips by relative path.
        /// </summary>
        readonly Dictionary<string, AnimationClipAsset> AnimationsByPath = new Dictionary<string, AnimationClipAsset>(StringComparer.Ordinal);

        /// <summary>
        /// Gets how many resolutions were requested.
        /// </summary>
        public int Resolutions { get; private set; }

        /// <summary>
        /// Adds an audio asset.
        /// </summary>
        /// <param name="path">Relative path.</param>
        /// <param name="asset">Asset served for the path.</param>
        public void AddAudio(string path, AudioAsset asset) {
            AudioByPath.Add(path, asset);
        }

        /// <summary>
        /// Adds an animation clip.
        /// </summary>
        /// <param name="path">Relative path.</param>
        /// <param name="clip">Clip served for the path.</param>
        public void AddAnimation(string path, AnimationClipAsset clip) {
            AnimationsByPath.Add(path, clip);
        }

        /// <summary>
        /// Returns the audio asset registered for the reference path.
        /// </summary>
        /// <param name="reference">Reference to resolve.</param>
        /// <returns>The asset.</returns>
        public AudioAsset ResolveAudio(SceneAssetReference reference) {
            Resolutions++;
            return AudioByPath[reference.RelativePath];
        }

        /// <summary>
        /// Returns the animation clip registered for the reference path.
        /// </summary>
        /// <param name="reference">Reference to resolve.</param>
        /// <returns>The clip.</returns>
        public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
            Resolutions++;
            return AnimationsByPath[reference.RelativePath];
        }
    }
}
