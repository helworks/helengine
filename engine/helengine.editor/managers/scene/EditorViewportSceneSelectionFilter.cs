namespace helengine.editor {
    /// <summary>
    /// Filters scene-selection candidates so internal editor infrastructure is not selectable in the viewport.
    /// </summary>
    public static class EditorViewportSceneSelectionFilter {
        /// <summary>
        /// Determines whether one drawable should participate in viewport scene selection.
        /// </summary>
        /// <param name="drawable">Drawable candidate to evaluate.</param>
        /// <returns>True when the drawable belongs to a selectable scene entity.</returns>
        public static bool ShouldIncludeDrawableForSelection(IDrawable3D drawable) {
            if (drawable == null) {
                return false;
            }

            return ShouldSelectEntity(drawable.Parent);
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
    }
}
