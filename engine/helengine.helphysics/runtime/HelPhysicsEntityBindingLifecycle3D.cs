namespace helengine {
    /// <summary>
    /// Observes normal entity component removal so a bound body's exact generation is released when its entity is disposed.
    /// </summary>
    sealed class HelPhysicsEntityBindingLifecycle3D : Component {
        /// <summary>
        /// Stores the binder that owns the association represented by this lifecycle component.
        /// </summary>
        readonly HelPhysicsSceneBinder3D Binder;

        /// <summary>
        /// Initializes one lifecycle observer for an explicit scene binder.
        /// </summary>
        /// <param name="binder">Binder to notify when the owning entity removes this component.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
        public HelPhysicsEntityBindingLifecycle3D(HelPhysicsSceneBinder3D binder) {
            Binder = binder ?? throw new ArgumentNullException(nameof(binder));
        }

        /// <summary>
        /// Invalidates and queues removal of the owning binding during normal detach or entity disposal.
        /// </summary>
        /// <param name="entity">Entity losing this lifecycle observer.</param>
        public override void ComponentRemoved(Entity entity) {
            Binder.NotifyBindingLifecycleRemoved(entity, this);
        }
    }
}
