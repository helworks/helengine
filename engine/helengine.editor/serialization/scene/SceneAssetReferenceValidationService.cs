namespace helengine.editor {
    /// <summary>
    /// Validates persisted scene asset references against the editor-supported reference surface.
    /// </summary>
    public static class SceneAssetReferenceValidationService {
        /// Packaged runtime relative path emitted for the generated engine cube model during scene packaging.
        /// </summary>
        const string PackagedCubeModelRelativePath = "cooked/engine/models/cube.hasset";

        /// <summary>
        /// Rooted packaged runtime path emitted for the generated engine cube model when the active runtime contract allows rooted packaged references.
        /// </summary>
        const string RootedPackagedCubeModelRelativePath = "/cooked/engine/models/cube.hasset";

        /// <summary>
        /// Packaged runtime relative path emitted for the generated engine plane model during scene packaging.
        /// </summary>
        const string PackagedPlaneModelRelativePath = "cooked/engine/models/plane.hasset";

        /// <summary>
        /// Rooted packaged runtime path emitted for the generated engine plane model when the active runtime contract allows rooted packaged references.
        /// </summary>
        const string RootedPackagedPlaneModelRelativePath = "/cooked/engine/models/plane.hasset";

        /// <summary>
        /// Packaged runtime relative path emitted for the generated engine sphere model during scene packaging.
        /// </summary>
        const string PackagedSphereModelRelativePath = "cooked/engine/models/sphere.hasset";

        /// <summary>
        /// Rooted packaged runtime path emitted for the generated engine sphere model when the active runtime contract allows rooted packaged references.
        /// </summary>
        const string RootedPackagedSphereModelRelativePath = "/cooked/engine/models/sphere.hasset";

        /// <summary>
        /// Packaged runtime relative path emitted for the generated engine standard material during scene packaging.
        /// </summary>
        const string PackagedStandardMaterialRelativePath = "cooked/engine/materials/standard.hasset";

        /// <summary>
        /// Rooted packaged runtime path emitted for the generated engine standard material when the active runtime contract allows rooted packaged references.
        /// </summary>
        const string RootedPackagedStandardMaterialRelativePath = "/cooked/engine/materials/standard.hasset";

        /// <summary>
        /// Validates every named scene asset reference stored in one component save-state.
        /// </summary>
        /// <param name="component">Component that owns the save-state.</param>
        /// <param name="saveState">Save-state containing authored scene asset references.</param>
        public static void ValidateComponentSaveState(Component component, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (saveState == null) {
                return;
            }

            foreach (KeyValuePair<string, SceneAssetReference> pair in saveState.EnumerateNamedAssetReferences()) {
                ValidateNamedReference(component, pair.Key, pair.Value, string.Empty);
            }

            foreach (EntityComponentPlatformOverrideState overrideState in saveState.EnumeratePlatformOverrides()) {
                ValidatePlatformOverride(component, overrideState);
            }
        }

        /// <summary>
        /// Validates one typed scene asset reference in the context of one serialized member.
        /// </summary>
        /// <param name="valueType">Asset-backed member type that owns the reference.</param>
        /// <param name="reference">Scene asset reference to validate.</param>
        /// <param name="context">Human-readable owner context used in failure messages.</param>
        public static void ValidateTypedReference(Type valueType, SceneAssetReference reference, string context) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (string.IsNullOrWhiteSpace(context)) {
                throw new ArgumentException("Reference context must be provided.", nameof(context));
            }

            if (valueType == typeof(FontAsset)) {
                ValidateFontReference(reference, context);
                return;
            }
            if (valueType == typeof(RuntimeTexture)) {
                ValidateTextureReference(reference, context);
                return;
            }
            if (valueType == typeof(RuntimeModel)) {
                ValidateModelReference(reference, context);
                return;
            }
            if (valueType == typeof(RuntimeMaterial)) {
                ValidateMaterialReference(reference, context);
                return;
            }
            if (valueType == typeof(AnimationClipAsset)) {
                ValidateAnimationClipReference(reference, context);
                return;
            }

            throw new InvalidOperationException($"Unsupported asset-backed member type '{valueType.FullName}' for {context}.");
        }

        /// <summary>
        /// Validates every named scene asset reference stored in one platform override payload.
        /// </summary>
        /// <param name="component">Component that owns the platform override.</param>
        /// <param name="overrideState">Platform override payload to validate.</param>
        static void ValidatePlatformOverride(Component component, EntityComponentPlatformOverrideState overrideState) {
            if (overrideState == null) {
                return;
            }

            foreach (KeyValuePair<string, SceneAssetReference> pair in overrideState.EnumerateNamedAssetReferences()) {
                ValidateNamedReference(component, pair.Key, pair.Value, overrideState.PlatformId);
            }
        }

        /// <summary>
        /// Validates one named scene asset reference stored against one component.
        /// </summary>
        /// <param name="component">Component that owns the reference.</param>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="reference">Scene asset reference to validate.</param>
        /// <param name="platformId">Optional platform id when the reference belongs to one platform override.</param>
        static void ValidateNamedReference(Component component, string referenceName, SceneAssetReference reference, string platformId) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (string.IsNullOrWhiteSpace(referenceName) || reference == null) {
                return;
            }

            Type valueType = ResolveReferenceValueType(component, referenceName);
            if (valueType == null) {
                return;
            }

            ValidateTypedReference(valueType, reference, BuildReferenceContext(component, referenceName, platformId));
        }

        /// <summary>
        /// Resolves the supported asset-backed member type represented by one component reference name.
        /// </summary>
        /// <param name="component">Component that owns the reference.</param>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <returns>Resolved asset-backed member type when the reference name is known; otherwise null.</returns>
        static Type ResolveReferenceValueType(Component component, string referenceName) {
            if (component is MeshComponent) {
                if (string.Equals(referenceName, "Model", StringComparison.Ordinal)) {
                    return typeof(RuntimeModel);
                }
                if (referenceName.StartsWith("Materials[", StringComparison.Ordinal)) {
                    return typeof(RuntimeMaterial);
                }
            } else if (component is SpriteComponent &&
                       string.Equals(referenceName, TextureAssetScenePersistenceSupport.TextureReferenceName, StringComparison.Ordinal)) {
                return typeof(RuntimeTexture);
            } else if ((component is TextComponent || component is FPSComponent) &&
                       string.Equals(referenceName, FontAssetScenePersistenceSupport.FontReferenceName, StringComparison.Ordinal)) {
                return typeof(FontAsset);
            }

            return null;
        }

        /// <summary>
        /// Builds one human-readable owner context for validation failures.
        /// </summary>
        /// <param name="component">Component that owns the reference.</param>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="platformId">Optional platform id when the reference belongs to one platform override.</param>
        /// <returns>Human-readable owner context.</returns>
        static string BuildReferenceContext(Component component, string referenceName, string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return $"component '{component.GetType().FullName}' reference '{referenceName}'";
            }

            return $"component '{component.GetType().FullName}' reference '{referenceName}' in platform override '{platformId}'";
        }

        /// <summary>
        /// Validates one font scene asset reference.
        /// </summary>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateFontReference(SceneAssetReference reference, string context) {
            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                ValidateFileSystemReference("font", reference, context);
                return;
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.Generated) {
                throw new InvalidOperationException($"Unsupported font reference source kind '{reference.SourceKind}' for {context}.");
            }

            ValidateGeneratedReferenceHeader("font", reference, context);
            if (!string.Equals(reference.ProviderId, EditorSceneAssetReferenceFactory.ProviderIdValue, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated font provider '{reference.ProviderId}' for {context}.");
            }
            if (!string.Equals(reference.AssetId, EditorSceneAssetReferenceFactory.EditorUiFontAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated font asset id '{reference.AssetId}' for {context}.");
            }
            if (!string.Equals(reference.RelativePath, EditorSceneAssetReferenceFactory.EditorUiFontRelativePath, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated font path '{reference.RelativePath}' for {context}.");
            }
        }

        /// <summary>
        /// Validates one texture scene asset reference.
        /// </summary>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateTextureReference(SceneAssetReference reference, string context) {
            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                ValidateFileSystemReference("texture", reference, context);
                return;
            }

            throw new InvalidOperationException($"Generated texture references are not supported for {context}.");
        }

        /// <summary>
        /// Validates one model scene asset reference.
        /// </summary>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateModelReference(SceneAssetReference reference, string context) {
            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                ValidateFileSystemReference("model", reference, context);
                return;
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.Generated) {
                throw new InvalidOperationException($"Unsupported model reference source kind '{reference.SourceKind}' for {context}.");
            }

            ValidateGeneratedReferenceHeader("model", reference, context);
            if (!string.Equals(reference.ProviderId, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated model provider '{reference.ProviderId}' for {context}.");
            }
            if (IsSupportedGeneratedModelReference(reference)) {
                return;
            }

            throw new InvalidOperationException(
                $"Unsupported generated model asset id '{reference.AssetId}' for {context}. " +
                $"SourceKind='{reference.SourceKind}', RelativePath='{reference.RelativePath}', ProviderId='{reference.ProviderId}'.");
        }

        /// <summary>
        /// Validates one material scene asset reference.
        /// </summary>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateMaterialReference(SceneAssetReference reference, string context) {
            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                ValidateFileSystemReference("material", reference, context);
                return;
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.Generated) {
                throw new InvalidOperationException($"Unsupported material reference source kind '{reference.SourceKind}' for {context}.");
            }

            ValidateGeneratedReferenceHeader("material", reference, context);
            if (!string.Equals(reference.ProviderId, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material provider '{reference.ProviderId}' for {context}.");
            }
            if (!IsSupportedGeneratedMaterialReference(reference)) {
                throw new InvalidOperationException(
                    $"Unsupported generated material asset id '{reference.AssetId}' for {context}. " +
                    $"SourceKind='{reference.SourceKind}', RelativePath='{reference.RelativePath}', ProviderId='{reference.ProviderId}'.");
            }
        }

        /// <summary>
        /// Validates one animation-clip scene asset reference.
        /// </summary>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateAnimationClipReference(SceneAssetReference reference, string context) {
            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                ValidateFileSystemReference("animation clip", reference, context);
                return;
            }

            throw new InvalidOperationException($"Generated animation-clip references are not supported for {context}.");
        }

        /// <summary>
        /// Validates one file-backed reference shape.
        /// </summary>
        /// <param name="assetKind">Human-readable asset kind used in failure messages.</param>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateFileSystemReference(string assetKind, SceneAssetReference reference, string context) {
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException($"File-backed {assetKind} references must include a relative path for {context}.");
            }
            if (!string.IsNullOrEmpty(reference.ProviderId) || !string.IsNullOrEmpty(reference.AssetId)) {
                throw new InvalidOperationException($"File-backed {assetKind} references must not include generated identifiers for {context}.");
            }
        }

        /// <summary>
        /// Validates the common generated-reference header fields shared by supported generated assets.
        /// </summary>
        /// <param name="assetKind">Human-readable asset kind used in failure messages.</param>
        /// <param name="reference">Reference to validate.</param>
        /// <param name="context">Human-readable owner context.</param>
        static void ValidateGeneratedReferenceHeader(string assetKind, SceneAssetReference reference, string context) {
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException($"Generated {assetKind} references must include a relative path for {context}.");
            }
            if (string.IsNullOrWhiteSpace(reference.ProviderId)) {
                throw new InvalidOperationException($"Generated {assetKind} references must include a provider id for {context}.");
            }
            if (string.IsNullOrWhiteSpace(reference.AssetId)) {
                throw new InvalidOperationException($"Generated {assetKind} references must include an asset id for {context}.");
            }
        }

        /// <summary>
        /// Returns whether two generated scene asset references describe the same supported generated asset.
        /// </summary>
        /// <param name="candidate">Candidate reference under validation.</param>
        /// <param name="supportedReference">Supported generated reference shape.</param>
        /// <returns>True when the references match exactly.</returns>
        static bool MatchesGeneratedReference(SceneAssetReference candidate, SceneAssetReference supportedReference) {
            return string.Equals(candidate.ProviderId, supportedReference.ProviderId, StringComparison.Ordinal) &&
                   string.Equals(candidate.AssetId, supportedReference.AssetId, StringComparison.Ordinal) &&
                   string.Equals(candidate.RelativePath, supportedReference.RelativePath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether one generated model reference targets a sanctioned engine primitive in either authored-scene or packaged-runtime form.
        /// </summary>
        /// <param name="reference">Generated model reference under validation.</param>
        /// <returns>True when the reference describes one supported engine primitive model.</returns>
        static bool IsSupportedGeneratedModelReference(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel()) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.CreatePlaneModel()) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.CreateSphereModel()) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.ModelUtils.GeneratedCubeModelId, PackagedCubeModelRelativePath) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.ModelUtils.GeneratedPlaneModelId, PackagedPlaneModelRelativePath) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.ModelUtils.GeneratedSphereModelId, PackagedSphereModelRelativePath) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.ModelUtils.GeneratedCubeModelId, RootedPackagedCubeModelRelativePath) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.ModelUtils.GeneratedPlaneModelId, RootedPackagedPlaneModelRelativePath) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.ModelUtils.GeneratedSphereModelId, RootedPackagedSphereModelRelativePath);
        }

        /// <summary>
        /// Returns whether one generated material reference targets the sanctioned engine standard material in either authored-scene or packaged-runtime form.
        /// </summary>
        /// <param name="reference">Generated material reference under validation.</param>
        /// <returns>True when the reference describes the supported generated engine standard material.</returns>
        static bool IsSupportedGeneratedMaterialReference(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.CreateStandardMaterial()) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.EngineSceneAssetReferenceFactory.StandardMaterialAssetId, PackagedStandardMaterialRelativePath) ||
                   MatchesGeneratedReference(reference, global::helengine.EngineSceneAssetReferenceFactory.ProviderIdValue, global::helengine.EngineSceneAssetReferenceFactory.StandardMaterialAssetId, RootedPackagedStandardMaterialRelativePath);
        }

        /// <summary>
        /// Returns whether one generated scene asset reference matches the supplied provider, asset id, and relative path triplet exactly.
        /// </summary>
        /// <param name="candidate">Candidate reference under validation.</param>
        /// <param name="providerId">Expected provider identifier.</param>
        /// <param name="assetId">Expected generated asset identifier.</param>
        /// <param name="relativePath">Expected generated relative path.</param>
        /// <returns>True when all generated reference fields match exactly.</returns>
        static bool MatchesGeneratedReference(SceneAssetReference candidate, string providerId, string assetId, string relativePath) {
            if (candidate == null) {
                throw new ArgumentNullException(nameof(candidate));
            }

            return string.Equals(candidate.ProviderId, providerId, StringComparison.Ordinal) &&
                   string.Equals(candidate.AssetId, assetId, StringComparison.Ordinal) &&
                   string.Equals(candidate.RelativePath, relativePath, StringComparison.Ordinal);
        }
    }
}
