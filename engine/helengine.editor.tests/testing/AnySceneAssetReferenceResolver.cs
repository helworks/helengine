namespace helengine.editor.tests.testing {
    /// <summary>
    /// Resolves any scene asset reference to one reusable dummy runtime asset for load-path regression tests.
    /// </summary>
    internal sealed class AnySceneAssetReferenceResolver : ISceneAssetReferenceResolver {
        /// <summary>
        /// Shared runtime model returned for every resolved model reference.
        /// </summary>
        readonly RuntimeModel Model;

        /// <summary>
        /// Shared runtime material returned for every resolved material reference.
        /// </summary>
        readonly RuntimeMaterial ResolvedMaterial;

        /// <summary>
        /// Shared font asset returned for every resolved font reference.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Shared runtime texture returned for every resolved texture reference.
        /// </summary>
        readonly RuntimeTexture Texture;

        /// <summary>
        /// Shared animation clip returned for every resolved animation-clip reference.
        /// </summary>
        readonly AnimationClipAsset AnimationClip;

        /// <summary>
        /// Initializes the reusable dummy runtime assets used for permissive scene loading.
        /// </summary>
        public AnySceneAssetReferenceResolver() {
            Model = new TestRuntimeModel();
            ResolvedMaterial = new TestRuntimeMaterial();
            Font = new FontAsset(new FontInfo("Test", 16, 4f), null, new Dictionary<char, FontChar>(), 16f, 1, 1);
            Texture = new ManagedRuntimeTexture {
                Width = 1,
                Height = 1
            };
            AnimationClip = new AnimationClipAsset {
                Id = "Animations/Test.hanim",
                Duration = 1f
            };
        }

        /// <summary>
        /// Resolves one model reference to the shared dummy runtime model.
        /// </summary>
        /// <param name="reference">Ignored scene asset reference.</param>
        /// <returns>Shared dummy runtime model.</returns>
        public RuntimeModel ResolveModel(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return Model;
        }

        /// <summary>
        /// Resolves one material reference to the shared dummy runtime material.
        /// </summary>
        /// <param name="reference">Ignored scene asset reference.</param>
        /// <returns>Shared dummy runtime material.</returns>
        public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return ResolvedMaterial;
        }

        /// <summary>
        /// Resolves one font reference to the shared dummy font asset.
        /// </summary>
        /// <param name="reference">Ignored scene asset reference.</param>
        /// <returns>Shared dummy font asset.</returns>
        public FontAsset ResolveFont(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return Font;
        }

        /// <summary>
        /// Resolves one texture reference to the shared dummy runtime texture.
        /// </summary>
        /// <param name="reference">Ignored scene asset reference.</param>
        /// <returns>Shared dummy runtime texture.</returns>
        public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return Texture;
        }

        /// <summary>
        /// Resolves one animation-clip reference to the shared dummy clip.
        /// </summary>
        /// <param name="reference">Ignored scene asset reference.</param>
        /// <returns>Shared dummy animation clip.</returns>
        public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return AnimationClip;
        }
    }
}
