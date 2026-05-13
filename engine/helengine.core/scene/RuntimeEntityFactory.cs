namespace helengine {
    /// <summary>
    /// Creates authored scene entities for non-editor hosts.
    /// </summary>
    public class RuntimeEntityFactory : IEntityFactory {
        /// <summary>
        /// Creates one authored root entity.
        /// </summary>
        /// <param name="name">Display name requested for the created entity.</param>
        /// <returns>Created authored entity.</returns>
        public Entity Create(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Entity name must be provided.", nameof(name));
            }

            Entity entity = new Entity {
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            return entity;
        }

        /// <summary>
        /// Creates one authored child entity and attaches it to the supplied parent.
        /// </summary>
        /// <param name="parent">Parent that will own the created child.</param>
        /// <param name="name">Display name requested for the created child.</param>
        /// <returns>Created child entity.</returns>
        public Entity CreateChild(Entity parent, string name) {
            if (parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }

            Entity entity = Create(name);
            parent.AddChild(entity);
            return entity;
        }
    }
}
