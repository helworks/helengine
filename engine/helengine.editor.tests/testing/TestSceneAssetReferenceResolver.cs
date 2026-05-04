using helengine.editor;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Resolves pre-registered scene asset references for deterministic serialization tests.
    /// </summary>
    internal class TestSceneAssetReferenceResolver : ISceneAssetReferenceResolver {
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
        /// Initializes empty runtime lookup tables.
        /// </summary>
        public TestSceneAssetReferenceResolver() {
            ModelsByReferenceKey = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
            MaterialsByReferenceKey = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);
            FontsByReferenceKey = new Dictionary<string, FontAsset>(StringComparer.Ordinal);
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

            return runtimeFont;
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
    }
}
