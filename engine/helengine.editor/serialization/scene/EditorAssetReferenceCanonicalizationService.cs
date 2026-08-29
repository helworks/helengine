namespace helengine.editor {
    /// <summary>
    /// Canonicalizes file-backed references supplied by editor authoring tools before persistence.
    /// </summary>
    public sealed class EditorAssetReferenceCanonicalizationService : IDisposable {
        /// <summary>
        /// Shared project authoring boundary used for every reference in this service.
        /// </summary>
        readonly IEditorProjectAuthoringSession AuthoringSession;

        /// <summary>
        /// Initializes a project-scoped canonicalization service over the owning session.
        /// </summary>
        /// <param name="authoringSession">Session whose identity index and resolver are reused.</param>
        public EditorAssetReferenceCanonicalizationService(IEditorProjectAuthoringSession authoringSession) {
            AuthoringSession = authoringSession ?? throw new ArgumentNullException(nameof(authoringSession));
        }

        /// <summary>
        /// Releases canonicalization state. The authoring session owns the resolver.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Replaces path-only or stale file references in one component save state with current canonical references.
        /// </summary>
        /// <param name="component">Component owning the save state.</param>
        /// <param name="saveState">Save state to canonicalize.</param>
        /// <returns>True when at least one reference changed.</returns>
        public bool Canonicalize(Component component, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveState == null) {
                return false;
            }

            Dictionary<SceneAssetReference, SceneAssetReference> replacements = new Dictionary<SceneAssetReference, SceneAssetReference>();
            foreach (KeyValuePair<string, SceneAssetReference> pair in saveState.EnumerateNamedAssetReferences()) {
                if (TryCanonicalize(component, pair.Key, pair.Value, out SceneAssetReference canonical) && !ReferenceEquals(pair.Value, canonical)) {
                    replacements[pair.Value] = canonical;
                }
            }

            foreach (EntityComponentPlatformOverrideState overrideState in saveState.EnumeratePlatformOverrides()) {
                foreach (KeyValuePair<string, SceneAssetReference> overridePair in overrideState.EnumerateNamedAssetReferences()) {
                    if (TryCanonicalize(component, overridePair.Key, overridePair.Value, out SceneAssetReference overrideCanonical) && !ReferenceEquals(overridePair.Value, overrideCanonical)) {
                        replacements[overridePair.Value] = overrideCanonical;
                    }
                }
            }

            return replacements.Count > 0 && saveState.ReplaceAssetReferences(replacements);
        }

        /// <summary>
        /// Resolves the expected authored asset category for one component reference slot.
        /// </summary>
        /// <param name="component">Component owning the reference.</param>
        /// <param name="referenceName">Reference slot name.</param>
        /// <param name="expectedKind">Resolved asset category.</param>
        /// <returns>True when the slot is an editor-supported authored reference.</returns>
        public static bool TryGetExpectedKind(Component component, string referenceName, out AssetEntryKind expectedKind) {
            expectedKind = AssetEntryKind.Unknown;
            if (component == null || string.IsNullOrWhiteSpace(referenceName)) {
                return false;
            }

            if (component is MeshComponent) {
                if (string.Equals(referenceName, "Model", StringComparison.Ordinal)) {
                    expectedKind = AssetEntryKind.Model;
                    return true;
                }
                if (referenceName.StartsWith("Materials[", StringComparison.Ordinal)) {
                    expectedKind = AssetEntryKind.Material;
                    return true;
                }
            } else if (component is SpriteComponent &&
                       string.Equals(referenceName, TextureAssetScenePersistenceSupport.TextureReferenceName, StringComparison.Ordinal)) {
                expectedKind = AssetEntryKind.Image;
                return true;
            } else if ((component is TextComponent || component is FPSComponent || component is DebugComponent) &&
                       string.Equals(referenceName, FontAssetScenePersistenceSupport.FontReferenceName, StringComparison.Ordinal)) {
                expectedKind = AssetEntryKind.Font;
                return true;
            } else if (component is AnimationPlayerComponent &&
                       string.Equals(referenceName, nameof(AnimationPlayerComponent.Clip), StringComparison.Ordinal)) {
                // Animation clips are authored files but do not have a specialized browser category yet.
                // Resolve them through the generic file identity index so they still receive the same
                // assetId -> path -> content-hash contract as every other file-backed reference.
                expectedKind = AssetEntryKind.File;
                return true;
            } else if (component is BlueprintInstanceComponent &&
                       string.Equals(referenceName, nameof(BlueprintInstanceComponent.BlueprintAssetReference), StringComparison.Ordinal)) {
                expectedKind = AssetEntryKind.Blueprint;
                return true;
            } else if (component is AudioSourceComponent &&
                       string.Equals(referenceName, nameof(AudioSourceComponent.Clip), StringComparison.Ordinal)) {
                expectedKind = AssetEntryKind.Audio;
                return true;
            }

            return false;
        }

        bool TryCanonicalize(Component component, string referenceName, SceneAssetReference reference, out SceneAssetReference canonical) {
            canonical = reference;
            if (reference == null || reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem ||
                !TryGetExpectedKind(component, referenceName, out AssetEntryKind expectedKind)) {
                return false;
            }

            try {
                canonical = AuthoringSession.ResolveReference(reference, expectedKind).CanonicalReference;
                return canonical != null;
            } catch (InvalidOperationException) {
                // Unresolvable references remain untouched; the current resolver heals them
                // when the target is present and must not make unrelated scene saves fail.
                canonical = reference;
                return false;
            }
        }
    }
}
