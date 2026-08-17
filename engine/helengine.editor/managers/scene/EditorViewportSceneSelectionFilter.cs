namespace helengine.editor {
    /// <summary>
    /// Filters scene-selection candidates so internal editor infrastructure is not selectable in the viewport.
    /// </summary>
    public static class EditorViewportSceneSelectionFilter {
        /// <summary>
        /// Determines whether one drawable should participate in viewport scene selection.
        /// </summary>
        /// <param name="drawable">Drawable candidate to evaluate.</param>
        /// <returns>True when the drawable resolves to a selectable scene entity.</returns>
        public static bool ShouldIncludeDrawableForSelection(IDrawable3D drawable) {
            if (drawable == null) {
                return false;
            }

            return ResolveSelectableEntity(drawable.Parent) != null;
        }

        /// <summary>
        /// Determines whether one entity should be selectable in the scene viewport.
        /// </summary>
        /// <param name="entity">Entity candidate to evaluate.</param>
        /// <returns>True when the entity and its parents are not marked as internal editor infrastructure.</returns>
        public static bool ShouldSelectEntity(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (current is EditorEntity editorEntity && editorEntity.InternalEntity) {
                    return false;
                }

                current = current.Parent;
            }

            return entity != null;
        }

        /// <summary>
        /// Resolves the nearest selectable scene entity for one candidate entity. Entities inside a blueprint instance
        /// resolve to the outermost instance root so viewport picks select whole prefab instances instead of their
        /// expanded children.
        /// </summary>
        /// <param name="entity">Entity candidate to resolve.</param>
        /// <returns>The selectable owner entity when one exists; otherwise null.</returns>
        public static Entity ResolveSelectableEntity(Entity entity) {
            Entity previewSourceEntity = EditorWorldSpace2DPreviewMapper.ResolveSourceSelectionEntity(entity);
            if (previewSourceEntity != null) {
                return ResolveBlueprintInstanceRoot(previewSourceEntity);
            }

            Entity current = entity;
            while (current != null) {
                if (current is EditorEntity editorEntity && editorEntity.InternalEntity) {
                    current = current.Parent;
                    continue;
                }

                return ResolveBlueprintInstanceRoot(current);
            }

            return null;
        }

        /// <summary>
        /// Resolves the outermost blueprint instance root that owns one entity.
        /// </summary>
        /// <param name="entity">Entity to resolve.</param>
        /// <returns>The outermost blueprint instance root, or the supplied entity when it is not inside one.</returns>
        static Entity ResolveBlueprintInstanceRoot(Entity entity) {
            Entity blueprintRoot = null;
            Entity current = entity;
            while (current != null) {
                if (HasBlueprintInstanceComponent(current)) {
                    blueprintRoot = current;
                }

                current = current.Parent;
            }

            return blueprintRoot ?? entity;
        }

        /// <summary>
        /// Returns whether one entity carries a blueprint instance marker component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when the entity is a blueprint instance root.</returns>
        static bool HasBlueprintInstanceComponent(Entity entity) {
            if (entity.Components == null) {
                return false;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is BlueprintInstanceComponent) {
                    return true;
                }
            }

            return false;
        }
    }
}
