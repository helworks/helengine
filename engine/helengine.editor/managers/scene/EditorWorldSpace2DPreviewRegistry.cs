namespace helengine.editor {
    /// <summary>
    /// Stores the editor-only mapping between authored 2D scene entities and their internal world-space preview proxy entities.
    /// </summary>
    public static class EditorWorldSpace2DPreviewRegistry {
        /// <summary>
        /// Source-to-preview lookup table for currently active world-space 2D preview proxies.
        /// </summary>
        static readonly Dictionary<Entity, EditorEntity> PreviewEntitiesBySourceEntity = new Dictionary<Entity, EditorEntity>();

        /// <summary>
        /// Preview-to-source lookup table for currently active world-space 2D preview proxies.
        /// </summary>
        static readonly Dictionary<EditorEntity, Entity> SourceEntitiesByPreviewEntity = new Dictionary<EditorEntity, Entity>();

        /// <summary>
        /// Registers one authored source entity and its corresponding internal preview proxy.
        /// </summary>
        /// <param name="sourceEntity">Authored scene entity that owns the real 2D component.</param>
        /// <param name="previewEntity">Internal editor preview proxy entity that mirrors the source.</param>
        public static void Register(Entity sourceEntity, EditorEntity previewEntity) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }
            if (previewEntity == null) {
                throw new ArgumentNullException(nameof(previewEntity));
            }

            RemoveBySourceEntity(sourceEntity);
            RemoveByPreviewEntity(previewEntity);
            PreviewEntitiesBySourceEntity[sourceEntity] = previewEntity;
            SourceEntitiesByPreviewEntity[previewEntity] = sourceEntity;
        }

        /// <summary>
        /// Removes one mapping by its authored source entity when present.
        /// </summary>
        /// <param name="sourceEntity">Authored scene entity whose preview mapping should be removed.</param>
        public static void RemoveBySourceEntity(Entity sourceEntity) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }

            if (!PreviewEntitiesBySourceEntity.TryGetValue(sourceEntity, out EditorEntity previewEntity)) {
                return;
            }

            PreviewEntitiesBySourceEntity.Remove(sourceEntity);
            SourceEntitiesByPreviewEntity.Remove(previewEntity);
        }

        /// <summary>
        /// Removes one mapping by its internal preview proxy entity when present.
        /// </summary>
        /// <param name="previewEntity">Internal preview proxy entity whose mapping should be removed.</param>
        public static void RemoveByPreviewEntity(EditorEntity previewEntity) {
            if (previewEntity == null) {
                throw new ArgumentNullException(nameof(previewEntity));
            }

            if (!SourceEntitiesByPreviewEntity.TryGetValue(previewEntity, out Entity sourceEntity)) {
                return;
            }

            SourceEntitiesByPreviewEntity.Remove(previewEntity);
            PreviewEntitiesBySourceEntity.Remove(sourceEntity);
        }

        /// <summary>
        /// Resolves the current internal preview proxy entity for one authored source entity.
        /// </summary>
        /// <param name="sourceEntity">Authored scene entity whose preview proxy should be resolved.</param>
        /// <returns>Preview proxy entity when present; otherwise null.</returns>
        public static EditorEntity ResolvePreviewEntity(Entity sourceEntity) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }

            PreviewEntitiesBySourceEntity.TryGetValue(sourceEntity, out EditorEntity previewEntity);
            return previewEntity;
        }

        /// <summary>
        /// Resolves the authored source entity currently mirrored by one internal preview proxy entity.
        /// </summary>
        /// <param name="previewEntity">Internal preview proxy entity whose source should be resolved.</param>
        /// <returns>Authored source entity when present; otherwise null.</returns>
        public static Entity ResolveSourceEntity(EditorEntity previewEntity) {
            if (previewEntity == null) {
                throw new ArgumentNullException(nameof(previewEntity));
            }

            SourceEntitiesByPreviewEntity.TryGetValue(previewEntity, out Entity sourceEntity);
            return sourceEntity;
        }

        /// <summary>
        /// Removes every currently registered preview mapping.
        /// </summary>
        public static void Clear() {
            PreviewEntitiesBySourceEntity.Clear();
            SourceEntitiesByPreviewEntity.Clear();
        }
    }
}
