namespace helengine.editor {
    /// <summary>
    /// Validates blueprint authoring constraints shared by blueprint load and save services.
    /// </summary>
    public static class BlueprintValidationService {
        /// <summary>
        /// Resolves the single editable blueprint root from the current live editor state.
        /// </summary>
        /// <param name="entities">Live entities currently owned by the object manager.</param>
        /// <returns>Single editable root entity.</returns>
        public static EditorEntity ResolveSingleEditableRoot(IReadOnlyList<Entity> entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            List<EditorEntity> editableRoots = new List<EditorEntity>();
            for (int i = 0; i < entities.Count; i++) {
                if (entities[i] is not EditorEntity editorEntity) {
                    continue;
                }
                if (editorEntity.Parent != null) {
                    continue;
                }
                if (editorEntity.InternalEntity) {
                    continue;
                }
                if (editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
                    continue;
                }

                editableRoots.Add(editorEntity);
            }

            if (editableRoots.Count != 1) {
                throw new InvalidOperationException("Blueprint authoring state must contain exactly one editable root entity.");
            }

            return editableRoots[0];
        }

        /// <summary>
        /// Validates one editable blueprint root before it is saved.
        /// </summary>
        /// <param name="rootEntity">Editable root entity that will be serialized.</param>
        public static void ValidateRootForSave(EditorEntity rootEntity) {
            if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            }
            if (rootEntity.Parent != null) {
                throw new InvalidOperationException("Blueprint root entity must not have a parent.");
            }
            if (rootEntity.InternalEntity) {
                throw new InvalidOperationException("Blueprint root entity must be user-authored.");
            }

            ValidateNoNestedBlueprintInstances(rootEntity);
        }

        /// <summary>
        /// Validates one deserialized blueprint asset payload before it is materialized.
        /// </summary>
        /// <param name="asset">Blueprint asset payload to validate.</param>
        public static void ValidateAsset(BlueprintAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (asset.RootEntity == null) {
                throw new InvalidOperationException("Blueprint assets must define exactly one root entity.");
            }
        }

        /// <summary>
        /// Rejects nested blueprint-instance components inside blueprint source content in v1.
        /// </summary>
        /// <param name="entity">Entity subtree to scan.</param>
        static void ValidateNoNestedBlueprintInstances(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components != null) {
                for (int i = 0; i < entity.Components.Count; i++) {
                    Component component = entity.Components[i];
                    if (component == null) {
                        continue;
                    }

                    Type componentType = component.GetType();
                    if (string.Equals(componentType.Name, "BlueprintInstanceComponent", StringComparison.Ordinal) ||
                        string.Equals(componentType.FullName, "helengine.editor.BlueprintInstanceComponent", StringComparison.Ordinal)) {
                        throw new InvalidOperationException("Blueprint sources may not contain nested blueprint instances in v1.");
                    }
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int i = 0; i < entity.Children.Count; i++) {
                if (entity.Children[i] is EditorEntity childEntity) {
                    ValidateNoNestedBlueprintInstances(childEntity);
                }
            }
        }
    }
}
