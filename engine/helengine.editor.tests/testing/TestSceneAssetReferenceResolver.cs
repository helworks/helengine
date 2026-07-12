using helengine.editor;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Resolves pre-registered scene asset references for deterministic serialization tests.
    /// </summary>
    internal class TestSceneAssetReferenceResolver : ISceneAssetReferenceResolver, IEditorOwnedAssetTrackingSceneAssetReferenceResolver {
        /// <summary>
        /// Runtime models keyed by their stable scene asset reference.
        /// </summary>
        readonly Dictionary<string, RuntimeModel> ModelsByReferenceKey;

        /// <summary>
        /// Runtime materials keyed by their stable scene asset reference.
        /// </summary>
        readonly Dictionary<string, RuntimeMaterial> MaterialsByReferenceKey;

        /// <summary>
        /// Runtime fonts keyed by their stable scene asset reference.
        /// </summary>
        readonly Dictionary<string, FontAsset> FontsByReferenceKey;

        /// <summary>
        /// Runtime textures keyed by their stable scene asset reference.
        /// </summary>
        readonly Dictionary<string, RuntimeTexture> TexturesByReferenceKey;

        /// <summary>
        /// Animation clips keyed by their stable scene asset reference.
        /// </summary>
        readonly Dictionary<string, AnimationClipAsset> AnimationClipsByReferenceKey;
        /// <summary>
        /// Tracks scene-owned runtime textures resolved during the active scene-load scope.
        /// </summary>
        List<RuntimeTexture> ActiveOwnedTextures;
        /// <summary>
        /// Tracks scene-owned font assets resolved during the active scene-load scope.
        /// </summary>
        List<FontAsset> ActiveOwnedFonts;
        /// <summary>
        /// Tracks scene-owned runtime models resolved during the active scene-load scope.
        /// </summary>
        List<RuntimeModel> ActiveOwnedModels;
        /// <summary>
        /// Tracks scene-owned runtime materials resolved during the active scene-load scope.
        /// </summary>
        List<RuntimeMaterial> ActiveOwnedMaterials;

        /// <summary>
        /// Initializes empty runtime lookup tables.
        /// </summary>
        public TestSceneAssetReferenceResolver() {
            ModelsByReferenceKey = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
            MaterialsByReferenceKey = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);
            FontsByReferenceKey = new Dictionary<string, FontAsset>(StringComparer.Ordinal);
            TexturesByReferenceKey = new Dictionary<string, RuntimeTexture>(StringComparer.Ordinal);
            AnimationClipsByReferenceKey = new Dictionary<string, AnimationClipAsset>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Registers one runtime model for a stable scene asset reference.
        /// </summary>
        /// <param name="reference">Stable reference to register.</param>
        /// <param name="runtimeModel">Runtime model resolved for the reference.</param>
        public void RegisterModel(SceneAssetReference reference, RuntimeModel runtimeModel) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (runtimeModel == null) {
                throw new ArgumentNullException(nameof(runtimeModel));
            }

            ModelsByReferenceKey[BuildReferenceKey(reference)] = runtimeModel;
        }

        /// <summary>
        /// Registers one runtime material for a stable scene asset reference.
        /// </summary>
        /// <param name="reference">Stable reference to register.</param>
        /// <param name="runtimeMaterial">Runtime material resolved for the reference.</param>
        public void RegisterMaterial(SceneAssetReference reference, RuntimeMaterial runtimeMaterial) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            MaterialsByReferenceKey[BuildReferenceKey(reference)] = runtimeMaterial;
        }

        /// <summary>
        /// Registers one runtime font for a stable scene asset reference.
        /// </summary>
        /// <param name="reference">Stable reference to register.</param>
        /// <param name="runtimeFont">Runtime font resolved for the reference.</param>
        public void RegisterFont(SceneAssetReference reference, FontAsset runtimeFont) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (runtimeFont == null) {
                throw new ArgumentNullException(nameof(runtimeFont));
            }

            FontsByReferenceKey[BuildReferenceKey(reference)] = runtimeFont;
        }

        /// <summary>
        /// Registers one runtime texture for a stable scene asset reference.
        /// </summary>
        /// <param name="reference">Stable reference to register.</param>
        /// <param name="runtimeTexture">Runtime texture resolved for the reference.</param>
        public void RegisterTexture(SceneAssetReference reference, RuntimeTexture runtimeTexture) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (runtimeTexture == null) {
                throw new ArgumentNullException(nameof(runtimeTexture));
            }

            TexturesByReferenceKey[BuildReferenceKey(reference)] = runtimeTexture;
        }

        /// <summary>
        /// Registers one animation clip for a stable scene asset reference.
        /// </summary>
        /// <param name="reference">Stable reference to register.</param>
        /// <param name="animationClip">Animation clip resolved for the reference.</param>
        public void RegisterAnimationClip(SceneAssetReference reference, AnimationClipAsset animationClip) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (animationClip == null) {
                throw new ArgumentNullException(nameof(animationClip));
            }

            AnimationClipsByReferenceKey[BuildReferenceKey(reference)] = animationClip;
        }

        /// <summary>
        /// Resolves one runtime model from the registered lookup table.
        /// </summary>
        /// <param name="reference">Stable reference to resolve.</param>
        /// <returns>Registered runtime model.</returns>
        public RuntimeModel ResolveModel(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string key = BuildReferenceKey(reference);
            if (!ModelsByReferenceKey.TryGetValue(key, out RuntimeModel runtimeModel)) {
                throw new InvalidOperationException($"Runtime model was not registered for '{key}'.");
            }

            TrackOwnedModel(runtimeModel);
            return runtimeModel;
        }

        /// <summary>
        /// Resolves one runtime material from the registered lookup table.
        /// </summary>
        /// <param name="reference">Stable reference to resolve.</param>
        /// <returns>Registered runtime material.</returns>
        public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string key = BuildReferenceKey(reference);
            if (!MaterialsByReferenceKey.TryGetValue(key, out RuntimeMaterial runtimeMaterial)) {
                throw new InvalidOperationException($"Runtime material was not registered for '{key}'.");
            }

            TrackOwnedMaterial(runtimeMaterial);
            return runtimeMaterial;
        }

        /// <summary>
        /// Resolves one runtime font from the registered lookup table.
        /// </summary>
        /// <param name="reference">Stable reference to resolve.</param>
        /// <returns>Registered runtime font.</returns>
        public FontAsset ResolveFont(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string key = BuildReferenceKey(reference);
            if (!FontsByReferenceKey.TryGetValue(key, out FontAsset runtimeFont)) {
                throw new InvalidOperationException($"Runtime font was not registered for '{key}'.");
            }

            TrackOwnedFont(runtimeFont);
            return runtimeFont;
        }

        /// <summary>
        /// Resolves one runtime texture from the registered lookup table.
        /// </summary>
        /// <param name="reference">Stable reference to resolve.</param>
        /// <returns>Registered runtime texture.</returns>
        public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string key = BuildReferenceKey(reference);
            if (!TexturesByReferenceKey.TryGetValue(key, out RuntimeTexture runtimeTexture)) {
                throw new InvalidOperationException($"Runtime texture was not registered for '{key}'.");
            }

            TrackOwnedTexture(runtimeTexture);
            return runtimeTexture;
        }

        /// <summary>
        /// Resolves one animation clip from the registered lookup table.
        /// </summary>
        /// <param name="reference">Stable reference to resolve.</param>
        /// <returns>Registered animation clip.</returns>
        public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string key = BuildReferenceKey(reference);
            if (!AnimationClipsByReferenceKey.TryGetValue(key, out AnimationClipAsset animationClip)) {
                throw new InvalidOperationException($"Animation clip was not registered for '{key}'.");
            }

            return animationClip;
        }

        /// <summary>
        /// Builds a stable dictionary key for one scene asset reference.
        /// </summary>
        /// <param name="reference">Reference to convert.</param>
        /// <returns>Stable lookup key.</returns>
        string BuildReferenceKey(SceneAssetReference reference) {
            return string.Concat(
                ((int)reference.SourceKind).ToString(),
                "|",
                reference.RelativePath ?? string.Empty,
                "|",
                reference.ProviderId ?? string.Empty,
                "|",
                reference.AssetId ?? string.Empty);
        }

        /// <summary>
        /// Starts one scene-owned asset tracking scope for the next load operation.
        /// </summary>
        public void BeginOwnedAssetTracking() {
            if (ActiveOwnedTextures != null || ActiveOwnedFonts != null || ActiveOwnedModels != null || ActiveOwnedMaterials != null) {
                throw new InvalidOperationException("Test scene asset tracking is already active.");
            }

            ActiveOwnedTextures = new List<RuntimeTexture>();
            ActiveOwnedFonts = new List<FontAsset>();
            ActiveOwnedModels = new List<RuntimeModel>();
            ActiveOwnedMaterials = new List<RuntimeMaterial>();
        }

        /// <summary>
        /// Completes the active scene-owned asset tracking scope and returns the resolved assets.
        /// </summary>
        /// <returns>Scene-owned runtime assets resolved during the active load scope.</returns>
        public RuntimeSceneOwnedAssetSet CompleteOwnedAssetTracking() {
            if (ActiveOwnedTextures == null || ActiveOwnedFonts == null || ActiveOwnedModels == null || ActiveOwnedMaterials == null) {
                throw new InvalidOperationException("Test scene asset tracking is not active.");
            }

            List<RuntimeTexture> ownedTextures = ActiveOwnedTextures;
            List<FontAsset> ownedFonts = ActiveOwnedFonts;
            List<RuntimeModel> ownedModels = ActiveOwnedModels;
            List<RuntimeMaterial> ownedMaterials = ActiveOwnedMaterials;
            ActiveOwnedTextures = null;
            ActiveOwnedFonts = null;
            ActiveOwnedModels = null;
            ActiveOwnedMaterials = null;
            return new RuntimeSceneOwnedAssetSet(
                ownedTextures,
                ownedFonts,
                Array.Empty<AudioAsset>(),
                ownedModels,
                ownedMaterials);
        }

        /// <summary>
        /// Cancels the active scene-owned asset tracking scope and returns the resolved assets.
        /// </summary>
        /// <returns>Scene-owned runtime assets resolved before the load failed.</returns>
        public RuntimeSceneOwnedAssetSet CancelOwnedAssetTracking() {
            if (ActiveOwnedTextures == null || ActiveOwnedFonts == null || ActiveOwnedModels == null || ActiveOwnedMaterials == null) {
                throw new InvalidOperationException("Test scene asset tracking is not active.");
            }

            List<RuntimeTexture> ownedTextures = ActiveOwnedTextures;
            List<FontAsset> ownedFonts = ActiveOwnedFonts;
            List<RuntimeModel> ownedModels = ActiveOwnedModels;
            List<RuntimeMaterial> ownedMaterials = ActiveOwnedMaterials;
            ActiveOwnedTextures = null;
            ActiveOwnedFonts = null;
            ActiveOwnedModels = null;
            ActiveOwnedMaterials = null;
            return new RuntimeSceneOwnedAssetSet(
                ownedTextures,
                ownedFonts,
                Array.Empty<AudioAsset>(),
                ownedModels,
                ownedMaterials);
        }

        /// <summary>
        /// Tracks one runtime texture in the active scene-owned asset scope.
        /// </summary>
        /// <param name="runtimeTexture">Runtime texture to track.</param>
        void TrackOwnedTexture(RuntimeTexture runtimeTexture) {
            if (runtimeTexture == null || ActiveOwnedTextures == null) {
                return;
            }

            if (!ActiveOwnedTextures.Contains(runtimeTexture)) {
                ActiveOwnedTextures.Add(runtimeTexture);
            }
        }

        /// <summary>
        /// Tracks one font asset in the active scene-owned asset scope.
        /// </summary>
        /// <param name="runtimeFont">Font asset to track.</param>
        void TrackOwnedFont(FontAsset runtimeFont) {
            if (runtimeFont == null || ActiveOwnedFonts == null) {
                return;
            }

            if (!ActiveOwnedFonts.Contains(runtimeFont)) {
                ActiveOwnedFonts.Add(runtimeFont);
            }
        }

        /// <summary>
        /// Tracks one runtime model in the active scene-owned asset scope.
        /// </summary>
        /// <param name="runtimeModel">Runtime model to track.</param>
        void TrackOwnedModel(RuntimeModel runtimeModel) {
            if (runtimeModel == null || ActiveOwnedModels == null) {
                return;
            }

            if (!ActiveOwnedModels.Contains(runtimeModel)) {
                ActiveOwnedModels.Add(runtimeModel);
            }
        }

        /// <summary>
        /// Tracks one runtime material in the active scene-owned asset scope.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material to track.</param>
        void TrackOwnedMaterial(RuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null || ActiveOwnedMaterials == null) {
                return;
            }

            if (!ActiveOwnedMaterials.Contains(runtimeMaterial)) {
                ActiveOwnedMaterials.Add(runtimeMaterial);
            }
        }
    }
}
