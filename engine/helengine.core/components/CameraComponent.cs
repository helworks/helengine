namespace helengine {
    /// <summary>
    /// Provides camera state for rendering scenes in 2D and 3D.
    /// </summary>
    public class CameraComponent : Component, ICamera {
        /// <summary>
        /// Cached camera draw order value.
        /// </summary>
        byte cameraDrawOrder;

        /// <summary>
        /// Cached layer mask used to decide which drawables are registered to this camera.
        /// </summary>
        ushort layerMask;

        /// <summary>
        /// Cached authored render intent resolved by active backends.
        /// </summary>
        CameraRenderSettings RenderSettingsValue;

        /// <summary>
        /// 2D render list for this camera.
        /// </summary>
        RenderList2D renderList2D;

        /// <summary>
        /// 3D render list for this camera.
        /// </summary>
        RenderList3D renderList3D;

        /// <summary>
        /// Initializes a new camera component with default lists and viewport.
        /// </summary>
        public CameraComponent() {
            LayerMask = 0b11111111;
            Viewport = new float4(0, 0, 1, 1);
            ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            RenderSettings = new CameraRenderSettings();

            InitializeLists();
        }

        /// <summary>
        /// Gets or sets the draw order for the camera.
        /// </summary>
        public byte CameraDrawOrder {
            get { return cameraDrawOrder; }
            set {
                if (cameraDrawOrder != value) {
                    if (Parent != null && Parent.IsHierarchyEnabled) {
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
        /// Gets or sets the render target that receives this camera's output; null renders to the main back buffer.
        /// </summary>
        public RenderTarget RenderTarget { get; set; }

        /// <summary>
        /// Gets or sets the clear settings applied before this camera renders.
        /// </summary>
        public CameraClearSettings ClearSettings { get; set; }

        /// <summary>
        /// Gets or sets the authored render intent used by planning and backend execution.
        /// </summary>
        public CameraRenderSettings RenderSettings {
            get { return RenderSettingsValue; }
            set { RenderSettingsValue = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets the 2D render queue registered for this camera.
        /// </summary>
        public IRenderQueue2D RenderQueue2D { get { return renderList2D; } }

        /// <summary>
        /// Gets the 3D render queue registered for this camera.
        /// </summary>
        public IRenderQueue3D RenderQueue3D { get { return renderList3D; } }

        /// <summary>
        /// Gets or sets the layer mask this camera renders.
        /// </summary>
        public ushort LayerMask {
            get { return layerMask; }
            set {
                if (layerMask != value) {
                    if (Parent != null && Parent.IsHierarchyEnabled) {
                        Core.Instance.ObjectManager.RemoveCamera(this);
                        layerMask = value;
                        Core.Instance.ObjectManager.RegisterCamera(this);
                    } else {
                        layerMask = value;
                    }
                }
            }
        }

        /// <summary>
        /// Allocates render lists using the core initialization options.
        /// </summary>
        void InitializeLists() {
            if (Core.Instance == null || Core.Instance.InitializationOptions == null) {
                throw new InvalidOperationException("Core initialization options must be set before creating camera lists.");
            }

            CoreInitializationOptions settings = Core.Instance.InitializationOptions;
            settings.Normalize();

            renderList2D = new RenderList2D(settings.RenderList2DInitialCapacity);
            renderList3D = new RenderList3D(settings.RenderList3DInitialCapacity);
        }

        /// <summary>
        /// Registers the camera when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
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
    }
}
