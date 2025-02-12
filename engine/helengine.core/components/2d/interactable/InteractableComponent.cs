namespace helengine {
    public class InteractableComponent : Component, IInteractable2D {
        public int2 Size { get; set; }

        public event Action<int2, int2, PointerInteraction> CursorEvent;

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterInteractable(this);
            }
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterInteractable(this);
            } else {
                Core.Instance.ObjectManager.RemoveInteractable(this);
            }
        }

        public virtual void OnCursor(int2 relPos, int2 delta, PointerInteraction state) {
            CursorEvent?.Invoke(relPos, delta, state);
        }
    }
}
