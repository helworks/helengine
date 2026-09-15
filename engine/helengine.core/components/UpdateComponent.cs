namespace helengine {
    /// <summary>
    /// Base component that participates in the engine update loop.
    /// </summary>
    public class UpdateComponent : Component, IUpdateable {
        /// <summary>
        /// Stores the update order used for sequencing.
        /// </summary>
        byte UpdateOrderValue;

        /// <summary>
        /// Gets or sets the update order for this component.
        /// </summary>
        public byte UpdateOrder {
            get { return UpdateOrderValue; }
            set {
                if (UpdateOrderValue != value) {
                    if (Parent != null && Parent.IsInitialized && Parent.IsHierarchyEnabled && ComponentExecutionPolicy.ShouldRunComponentLifecycle(this, Parent)) {
                        OwnerCore.ObjectManager.RemoveFromUpdate(this, UpdateOrderValue);
                        UpdateOrderValue = value;
                        OwnerCore.ObjectManager.RegisterForUpdate(this);
                    } else {
                        UpdateOrderValue = value;
                    }
                }
            }
        }

        /// <summary>
        /// Records the component attachment before its parent hierarchy has necessarily completed initialization.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
        }

        /// <summary>
        /// Registers the component for updates once its parent hierarchy is fully materialized and enabled.
        /// </summary>
        /// <param name="entity">Owning entity whose hierarchy completed initialization.</param>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);

            if (entity.IsHierarchyEnabled && ComponentExecutionPolicy.ShouldRunComponentLifecycle(this, entity)) {
                OwnerCore.ObjectManager.RegisterForUpdate(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the component based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            if (Parent == null || !Parent.IsInitialized || !ComponentExecutionPolicy.ShouldRunComponentLifecycle(this, Parent)) {
                return;
            }

            if (newEnabled) {
                OwnerCore.ObjectManager.RegisterForUpdate(this);
            } else {
                OwnerCore.ObjectManager.RemoveFromUpdate(this, UpdateOrderValue);
            }
        }

        /// <summary>
        /// Performs per-frame update logic.
        /// </summary>
        public virtual void Update() {

        }
    }
}
