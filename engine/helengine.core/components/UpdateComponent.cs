namespace helengine {
    public class UpdateComponent : Component, IUpdateable {
        byte updateOrder;

        public byte UpdateOrder {
            get { return updateOrder; }
            set {
                if (updateOrder != value) {
                    if (Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromUpdate(this);
                        updateOrder = value;
                        Core.Instance.ObjectManager.RegisterForUpdate(this);
                    } else {
                        updateOrder = value;
                    }
                }
            }
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForUpdate(this);
            }
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForUpdate(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromUpdate(this);
            }
        }

        public virtual void Update() {

        }
    }
}
