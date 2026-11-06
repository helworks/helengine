using helengine.editor;

namespace helengine.animation.tests {
    /// <summary>
    /// Cooks clips through the plugin's <see cref="HierarchyAnimationClipCooker"/> and records which authored files the
    /// packager handed to it, so a packaging test can tell a cooked clip from one the packager copied raw.
    /// </summary>
    sealed class RecordingClipCooker : IPackagedAssetCooker {
        /// <summary>
        /// Cooker that does the real work.
        /// </summary>
        readonly HierarchyAnimationClipCooker Inner = new HierarchyAnimationClipCooker();

        /// <summary>
        /// Authored relative paths cooked so far, in call order.
        /// </summary>
        readonly List<string> CookedPaths = new List<string>();

        /// <summary>
        /// Gets the extension of the wrapped cooker.
        /// </summary>
        public string Extension => Inner.Extension;

        /// <summary>
        /// Gets the authored relative paths cooked so far, in call order.
        /// </summary>
        public IReadOnlyList<string> CookedRelativePaths => CookedPaths;

        /// <summary>
        /// Records the reference and cooks it through the wrapped cooker.
        /// </summary>
        /// <param name="reference">Authored clip reference.</param>
        /// <param name="context">Packaging context.</param>
        /// <returns>Packaged reference returned by the wrapped cooker.</returns>
        public SceneAssetReference Cook(SceneAssetReference reference, PackagedAssetCookContext context) {
            CookedPaths.Add(reference.RelativePath);
            return Inner.Cook(reference, context);
        }
    }
}
