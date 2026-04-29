namespace helengine {
    /// <summary>
    /// Base class for entity components that participate in the engine lifecycle.
    /// </summary>
    public class Component {
        /// <summary>
        /// Gets the entity this component is attached to.
        /// </summary>
        public Entity Parent { get; private set; }
        
        /// <summary>
        /// Called when the component is added to an entity.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public virtual void ComponentAdded(Entity entity) {
            Parent = entity;
        }

        /// <summary>
        /// Called when the component is removed from an entity.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public virtual void ComponentRemoved(Entity entity) {
        }

        /// <summary>
        /// Called when the parent entity is enabled or disabled.
        /// </summary>
        /// <param name="newEnabled">True when enabled; false when disabled.</param>
        public virtual void ParentEnabledChange(bool newEnabled) {
        }

        /// <summary>
        /// Called when the parent entity toggles static state.
        /// </summary>
        /// <param name="newEnabled">True when marked static; otherwise false.</param>
        public virtual void ParentStaticChange(bool newEnabled) {
        }
    }
}
