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

        public List<int>[] RenderIndices2D { get; set; }
        public List<int>[][] RenderIndices3D { get; set; }

        public CameraComponent() {
            Viewport = new float4(0, 0, 1, 1);

            RenderIndices2D = new List<int>[4];
            for (int i = 0; i < RenderIndices2D.Length; i++) {
                RenderIndices2D[i] = new List<int>();
            }

            RenderIndices3D = new List<int>[4][];
            for (int i = 0; i < RenderIndices3D.Length; i++) {
                RenderIndices3D[i] = new List<int>[4];
                for (int j = 0; j < 4; j++) {
                    RenderIndices3D[i][j] = new List<int>();
                }
            }
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
