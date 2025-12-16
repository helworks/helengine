namespace helengine {
    /// <summary>
    /// Base component that participates in the engine update loop.
    /// </summary>
    public class UpdateComponent : Component, IUpdateable {
        byte updateOrder;

        /// <summary>
        /// Gets or sets the update order bucket for this component.
        /// </summary>
        public byte UpdateOrder {
            get { return updateOrder; }
            set {
                if (updateOrder != value) {
                    if (Parent != null && Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromUpdate(this);
                        updateOrder = value;
                        Core.Instance.ObjectManager.RegisterForUpdate(this);
                    } else {
                        updateOrder = value;
                    }
                }
            }
        }

        /// <summary>
        /// Registers the component for updates when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForUpdate(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the component based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForUpdate(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromUpdate(this);
            }
        }

        /// <summary>
        /// Performs per-frame update logic.
        /// </summary>
        public virtual void Update() {

        }
    }
}
