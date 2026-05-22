namespace helengine {
    /// <summary>
    /// Base class for entity components that participate in the engine lifecycle.
    /// </summary>
    public class Component : IDisposable {
        /// <summary>
        /// Stores the attached parent entity while the component is live.
        /// </summary>
        Entity parent;

        /// <summary>
        /// Tracks whether the component has completed disposal and should reject further use.
        /// </summary>
        bool isDisposed;

        /// <summary>
        /// Gets the entity this component is attached to.
        /// </summary>
        public Entity Parent {
            get {
                ThrowIfDisposed();
                return parent;
            }
            private set { parent = value; }
        }

        /// <summary>
        /// Gets whether this component is the editor-owned suppression marker that disables gameplay update execution during scene authoring.
        /// </summary>
        public virtual bool IsEditorUpdateExecutionSuppressionMarker => false;

        /// <summary>
        /// Gets the raw attached parent entity for internal lifecycle flows that must complete during disposal.
        /// </summary>
        internal Entity ParentUnsafe => parent;

        /// <summary>
        /// Gets whether disposal has completed and the component should reject further use.
        /// </summary>
        internal bool IsDisposed => isDisposed;

        /// <summary>
        /// Associates the component with one entity before any runtime lifecycle callbacks are considered.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        internal void AttachToEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            ThrowIfDisposed();
            Parent = entity;
        }

        /// <summary>
        /// Clears the parent association after the component has finished its detach lifecycle.
        /// </summary>
        internal void DetachFromEntity() {
            Parent = null;
        }

        /// <summary>
        /// Throws when the component was already disposed and can no longer participate in runtime ownership flows.
        /// </summary>
        internal void ThrowIfDisposed() {
            if (isDisposed) {
                throw new ObjectDisposedException(nameof(Component), "Disposed components cannot be used.");
            }
        }

        /// <summary>
        /// Called when the component is allowed to run its attach lifecycle.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public virtual void ComponentAdded(Entity entity) {
        }

        /// <summary>
        /// Called once after the parent entity hierarchy has finished initialization and the component can safely resolve related entities and components.
        /// </summary>
        /// <param name="entity">Entity that owns the initialized component.</param>
        public virtual void ComponentInitialized(Entity entity) {
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

        /// <summary>
        /// Releases runtime-owned resources held directly by the component before the native backend deletes the component instance.
        /// </summary>
        public virtual void Dispose() {
            isDisposed = true;
        }
    }
}
