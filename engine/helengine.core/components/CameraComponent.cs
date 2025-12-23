namespace helengine {
    /// <summary>
    /// Provides camera state for rendering scenes in 2D and 3D.
    /// </summary>
    public class CameraComponent : Component, ICamera {
        byte cameraDrawOrder;

        /// <summary>
        /// Gets or sets the draw order bucket for the camera.
        /// </summary>
        public byte CameraDrawOrder {
            get { return cameraDrawOrder; }
            set {
                if (cameraDrawOrder != value) {
                    if (Parent != null && Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveCamera(this);
                        cameraDrawOrder = value;
                        Core.Instance.ObjectManager.RegisterCamera(this);
                    } else {
                        cameraDrawOrder = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the viewport rectangle.
        /// </summary>
        public float4 Viewport { get; set; }

        /// <summary>
        /// Gets the 2D render buckets registered for this camera.
        /// </summary>
        public RenderBucket2D[] RenderBuckets2D { get { return registry2D.Buckets; } }

        /// <summary>
        /// Gets the 3D render buckets registered for this camera.
        /// </summary>
        public RenderBucket3D[][][] RenderBuckets3D { get { return registry3D.Buckets; } }

        /// <summary>
        /// Gets or sets the layer mask this camera renders.
        /// </summary>
        public ushort LayerMask { get; set; }

        Camera2DRegistry registry2D;
        Camera3DRegistry registry3D;

        /// <summary>
        /// Initializes a new camera component with default buckets and viewport.
        /// </summary>
        public CameraComponent() {
            LayerMask = 0b11111111;
            Viewport = new float4(0, 0, 1, 1);

            registry2D = new Camera2DRegistry(4, 64, 256);
            // 3D: 4 variants, 4 order buckets, 4 state bins per bucket
            registry3D = new Camera3DRegistry(4, 4, 4, 64, 512);
        }

        /// <summary>
        /// Registers the camera when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterCamera(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the camera based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterCamera(this);
            } else {
                Core.Instance.ObjectManager.RemoveCamera(this);
            }
        }

        // Internal accessors for ObjectManager registries
        /// <summary>
        /// Gets the 2D render registry associated with this camera.
        /// </summary>
        /// <returns>2D registry reference.</returns>
        internal Camera2DRegistry Get2DRegistry() => registry2D;
        /// <summary>
        /// Gets the 3D render registry associated with this camera.
        /// </summary>
        /// <returns>3D registry reference.</returns>
        internal Camera3DRegistry Get3DRegistry() => registry3D;
    }
}
