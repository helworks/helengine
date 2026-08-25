namespace helengine.editor {
    /// <summary>
    /// Applies canonical authored-reference replacements to editor save metadata after a load.
    /// </summary>
    public sealed class SceneAssetReferenceHealingService {
        /// <summary>Applies replacements recursively to entity save-state components.</summary>
        /// <param name="roots">Loaded scene root entities.</param>
        /// <param name="replacements">Old-to-canonical reference map.</param>
        /// <returns>True when any save metadata changed.</returns>
        public bool Apply(IEnumerable<EditorEntity> roots, IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> replacements) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }
            if (replacements == null) {
                throw new ArgumentNullException(nameof(replacements));
            }
            bool changed = false;
            foreach (EditorEntity root in roots) {
                changed |= ApplyEntity(root, replacements);
            }
            return changed;
        }

        /// <summary>Applies replacements to one entity and its descendants.</summary>
        static bool ApplyEntity(EditorEntity entity, IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> replacements) {
            if (entity == null) {
                return false;
            }
            bool changed = false;
            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is EntitySaveComponent saveComponent) {
                    foreach (EntityComponentSaveState state in saveComponent.EnumerateComponentStates()) {
                        changed |= state.ReplaceAssetReferences(replacements);
                    }
                }
            }
            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                changed |= ApplyEntity((EditorEntity)entity.Children[childIndex], replacements);
            }
            return changed;
        }
    }
}
