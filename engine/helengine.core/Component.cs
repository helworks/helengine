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
        /// Gets whether this component is the editor-owned suppression marker that disables gameplay update execution during scene authoring.
        /// </summary>
        public virtual bool IsEditorUpdateExecutionSuppressionMarker => false;

        /// <summary>
        /// Associates the component with one entity before any runtime lifecycle callbacks are considered.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        internal void AttachToEntity(Entity entity) {
            Parent = entity;
        }

        /// <summary>
        /// Clears the parent association after the component has finished its detach lifecycle.
        /// </summary>
        internal void DetachFromEntity() {
            Parent = null;
        }
        
        /// <summary>
        /// Called when the component is allowed to run its attach lifecycle.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public virtual void ComponentAdded(Entity entity) {
        }

        /// <summary>
        /// Called when the component is allowed to run its detach lifecycle.
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
