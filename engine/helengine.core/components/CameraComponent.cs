namespace helengine {
    public class CameraComponent : Component, ICamera {
        byte cameraDrawOrder;

        public byte CameraDrawOrder {
            get { return cameraDrawOrder; }
            set {
                if (cameraDrawOrder != value) {
                    if (Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveCamera(this);
                        cameraDrawOrder = value;
                        Core.Instance.ObjectManager.RegisterCamera(this);
                    } else {
                        cameraDrawOrder = value;
                    }
                }
            }
        }

        public float4 Viewport { get; set; }

        public CameraComponent() {
            Viewport = new float4(0, 0, 1, 1);
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterCamera(this);
            }
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterCamera(this);
            } else {
                Core.Instance.ObjectManager.RemoveCamera(this);
            }
        }
    }
}
