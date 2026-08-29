namespace helengine {
    /// <summary>
    /// Creates authored scene entities for non-editor hosts.
    /// </summary>
    public class RuntimeEntityFactory : IEntityFactory {
        readonly Core OwnerCore;

        /// <summary>
        /// Initializes an entity factory bound to one explicit runtime core.
        /// </summary>
        public RuntimeEntityFactory(Core ownerCore) {
            OwnerCore = ownerCore ?? throw new ArgumentNullException(nameof(ownerCore));
        }

        /// <summary>
        /// Creates one authored root entity.
        /// </summary>
        /// <param name="name">Display name requested for the created entity.</param>
        /// <returns>Created authored entity.</returns>
        public Entity Create(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Entity name must be provided.", nameof(name));
            }

            Entity entity = new Entity(OwnerCore) {
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
        [NativeBorrowedReturn]
        public Entity CreateChild(Entity parent, string name) {
            if (parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }
            if (!ReferenceEquals(parent.OwnerCore, OwnerCore)) {
                throw new InvalidOperationException("The parent entity belongs to a different runtime core.");
            }

            Entity entity = Create(name);
            int childIndex = parent.Children.Count;
            parent.AddChild(entity);
            return parent.Children[childIndex];
        }
    }
}
