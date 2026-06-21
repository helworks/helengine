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
        /// Cached viewport rectangle used by render backends and editor previews.
        /// </summary>
        float4 viewportValue;

        /// <summary>
        /// Cached authored render intent resolved by active backends.
        /// </summary>
        CameraRenderSettings RenderSettingsValue;

        /// <summary>
        /// Cached near clip-plane distance used for perspective projection creation.
        /// </summary>
        float NearPlaneDistanceValue;

        /// <summary>
        /// Cached far clip-plane distance used for perspective projection creation.
        /// </summary>
        float FarPlaneDistanceValue;

        /// <summary>
        /// 2D render list for this camera.
        /// </summary>
        RenderList2D renderList2D;

        /// <summary>
        /// 3D render list for this camera.
        /// </summary>
        RenderList3D renderList3D;

        /// <summary>
        /// Raised whenever the authored viewport rectangle changes.
        /// </summary>
        public event Action ViewportChanged;

        /// <summary>
        /// Initializes a new camera component with default lists and viewport.
        /// </summary>
        public CameraComponent() {
            LayerMask = 0b11111111;
            viewportValue = new float4(0, 0, 1, 1);
            NearPlaneDistanceValue = 0.1f;
            FarPlaneDistanceValue = 100f;
            ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            RenderSettings = new CameraRenderSettings();

            InitializeLists();
        }

        /// <summary>
        /// Gets or sets the draw order for the camera.
        /// </summary>
        [EditorPropertyDisplayName("Draw Order")]
        [EditorPropertyOrder(0)]
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
        [EditorPropertyHidden]
        public float4 Viewport {
            get { return viewportValue; }
            set {
                if (viewportValue.X != value.X || viewportValue.Y != value.Y || viewportValue.Z != value.Z || viewportValue.W != value.W) {
                    viewportValue = value;
                    RaiseViewportChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the near clip-plane distance used for perspective projection creation.
        /// </summary>
        [EditorPropertyDisplayName("Near Plane Distance")]
        [EditorPropertyOrder(2)]
        public float NearPlaneDistance {
            get { return NearPlaneDistanceValue; }
            set {
                NearPlaneDistanceValue = CameraProjectionUtils.ClampNearPlaneDistance(value, FarPlaneDistanceValue);
                FarPlaneDistanceValue = CameraProjectionUtils.ClampFarPlaneDistance(NearPlaneDistanceValue, FarPlaneDistanceValue);
            }
        }

        /// <summary>
        /// Gets or sets the far clip-plane distance used for perspective projection creation.
        /// </summary>
        [EditorPropertyDisplayName("Far Plane Distance")]
        [EditorPropertyOrder(3)]
        public float FarPlaneDistance {
            get { return FarPlaneDistanceValue; }
            set { FarPlaneDistanceValue = CameraProjectionUtils.ClampFarPlaneDistance(NearPlaneDistanceValue, value); }
        }

        /// <summary>
        /// Gets or sets the render target that receives this camera's output; null renders to the main back buffer.
        /// </summary>
        [EditorPropertyHidden]
        [ScenePersistenceIgnore]
        public RenderTarget RenderTarget { get; set; }

        /// <summary>
        /// Gets or sets the clear settings applied before this camera renders.
        /// </summary>
        [EditorPropertyDisplayName("Clear Settings")]
        [EditorPropertyOrder(4)]
        public CameraClearSettings ClearSettings { get; set; }

        /// <summary>
        /// Gets or sets the authored render intent used by planning and backend execution.
        /// </summary>
        [EditorPropertyHidden]
        public CameraRenderSettings RenderSettings {
            get { return RenderSettingsValue; }
            set {
                CameraRenderSettings newValue = value ?? throw new ArgumentNullException(nameof(value));
                if (RenderSettingsValue != null && !ReferenceEquals(RenderSettingsValue, newValue)) {
                    NativeOwnership.Delete(RenderSettingsValue);
                }

                RenderSettingsValue = newValue;
            }
        }

        /// <summary>
        /// Gets the 2D render queue registered for this camera.
        /// </summary>
        [EditorPropertyHidden]
        public IRenderQueue2D RenderQueue2D { get { return renderList2D; } }

        /// <summary>
        /// Gets the 3D render queue registered for this camera.
        /// </summary>
        [EditorPropertyHidden]
        public IRenderQueue3D RenderQueue3D { get { return renderList3D; } }

        /// <summary>
        /// Gets or sets the layer mask this camera renders.
        /// </summary>
        [EditorPropertyDisplayName("Layer Mask")]
        [EditorPropertyOrder(1)]
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

        /// <summary>
        /// Unregisters the camera from the object manager when the owning entity removes the component.
        /// </summary>
        /// <param name="entity">Entity losing the camera component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            Core.Instance.ObjectManager.RemoveCamera(this);
        }

        /// <summary>
        /// Raises the viewport changed event after the stored rectangle is updated.
        /// </summary>
        void RaiseViewportChanged() {
            if (ViewportChanged != null) {
                ViewportChanged();
            }
        }

        /// <summary>
        /// Releases per-camera render queues and render settings owned by this camera component.
        /// </summary>
        public override void Dispose() {
            NativeOwnership.DisposeAndDelete(renderList2D);
            NativeOwnership.DisposeAndDelete(renderList3D);
            NativeOwnership.Delete(RenderSettingsValue);
            renderList2D = null;
            renderList3D = null;
            RenderSettingsValue = null;
            RenderTarget = null;
            base.Dispose();
        }
    }
}
