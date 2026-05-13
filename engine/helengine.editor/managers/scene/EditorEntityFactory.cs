namespace helengine.editor {
    /// <summary>
    /// Creates authored scene entities for editor hosts.
    /// </summary>
    public class EditorEntityFactory : IEntityFactory {
        /// <summary>
        /// Creates one authored root entity.
        /// </summary>
        /// <param name="name">Display name assigned to the created entity.</param>
        /// <returns>Created authored editor entity.</returns>
        public Entity Create(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Entity name must be provided.", nameof(name));
            }

            EditorEntity entity = new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            EnsureUpdateExecutionSuppressionMarker(entity);
            return entity;
        }

        /// <summary>
        /// Creates one authored child entity and attaches it to the supplied parent.
        /// </summary>
        /// <param name="parent">Parent that will own the created child.</param>
        /// <param name="name">Display name assigned to the created child.</param>
        /// <returns>Created authored editor entity.</returns>
        public Entity CreateChild(Entity parent, string name) {
            if (parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }

            Entity entity = Create(name);
            parent.AddChild(entity);
            return entity;
        }

        /// <summary>
        /// Ensures authored editor entities carry the hidden marker that suppresses gameplay update execution during authoring.
        /// </summary>
        /// <param name="entity">Entity whose component collection should contain the marker.</param>
        void EnsureUpdateExecutionSuppressionMarker(EditorEntity entity) {
            if (entity == null || entity.Components == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is EditorUpdateExecutionSuppressionComponent) {
                    return;
                }
            }

            entity.AddComponent(new EditorUpdateExecutionSuppressionComponent());
        }
    }
}
