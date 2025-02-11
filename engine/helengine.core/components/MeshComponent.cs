namespace helengine {
    public class MeshComponent : Component, IDrawable3D {
        byte renderOrder3D;

        public RuntimeModel? Model { get; set; }

        public byte RenderOrder3D {
            get { return renderOrder3D; }
            set {
                if (renderOrder3D != value) {
                    if (Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromRender3D(this);
                        renderOrder3D = value;
                        Core.Instance.ObjectManager.RegisterForRender3D(this);
                    } else {
                        renderOrder3D = value;
                    }
                }
            }
        }

        public MeshComponent() {
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            }
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromRender3D(this);
            }
        }
    }
}
