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

        public RenderBucket2D[] RenderBuckets2D { get { return registry2D.Buckets; } }
        public RenderBucket3D[][][] RenderBuckets3D { get { return registry3D.Buckets; } }

        public ushort LayerMask { get; set; }

        Camera2DRegistry registry2D;
        Camera3DRegistry registry3D;

        public CameraComponent() {
            LayerMask = 0b11111111;
            Viewport = new float4(0, 0, 1, 1);

            registry2D = new Camera2DRegistry(4, 64, 256);
            // 3D: 4 variants, 4 order buckets, 4 state bins per bucket
            registry3D = new Camera3DRegistry(4, 4, 4, 64, 512);
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

        // Internal accessors for ObjectManager registries
        internal Camera2DRegistry Get2DRegistry() => registry2D;
        internal Camera3DRegistry Get3DRegistry() => registry3D;
    }
}
