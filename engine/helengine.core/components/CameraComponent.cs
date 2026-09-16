namespace helengine {
    /// <summary>
    /// Provides camera state for rendering scenes in 2D and 3D.
    /// </summary>
    public class CameraComponent : Component, ICamera {
        /// <summary>
        /// Cached camera draw order value.
        /// </summary>
        byte CameraDrawOrderValue;

        /// <summary>
        /// Cached layer mask used to decide which drawables are registered to this camera.
        /// </summary>
        ushort LayerMaskValue;

        /// <summary>
        /// Cached viewport rectangle used by render backends and editor previews.
        /// </summary>
        float4 ViewportValue;

        /// <summary>
        /// Cached authored render intent resolved by active backends.
        /// </summary>
        CameraRenderSettings RenderSettingsValue;

        /// <summary>
        /// Cached near clip-plane distance used for perspective projection creation.
        /// </summary>
        float FieldOfViewValue;

        float NearPlaneDistanceValue;

        /// <summary>
        /// Cached far clip-plane distance used for perspective projection creation.
        /// </summary>
        float FarPlaneDistanceValue;

        /// <summary>
        /// 2D render list for this camera.
        /// </summary>
        RenderList2D RenderList2D;

        /// <summary>
        /// 3D render list for this camera.
        /// </summary>
        RenderList3D RenderList3D;

        /// <summary>
        /// Raised whenever the authored viewport rectangle changes.
        /// </summary>
        public event Action ViewportChanged;

        /// <summary>
        /// Initializes a new camera component with default lists and viewport.
        /// </summary>
        public CameraComponent() {
            LayerMask = 0b11111111;
            ViewportValue = new float4(0, 0, 1, 1);
            FieldOfViewValue = CameraProjectionUtils.DefaultFieldOfView;
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
            get { return CameraDrawOrderValue; }
            set {
                if (CameraDrawOrderValue != value) {
                    if (Parent != null && Parent.IsHierarchyEnabled) {
                        OwnerCore.ObjectManager.RemoveCamera(this);
                        CameraDrawOrderValue = value;
                        RegisterWithObjectManagerIfNeeded();
                    } else {
                        CameraDrawOrderValue = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the viewport rectangle.
        /// </summary>
        [EditorPropertyHidden]
        public float4 Viewport {
            get { return ViewportValue; }
            set {
                if (ViewportValue.X != value.X || ViewportValue.Y != value.Y || ViewportValue.Z != value.Z || ViewportValue.W != value.W) {
                    ViewportValue = value;
                    RaiseViewportChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the vertical field of view, in radians, used for perspective projection creation.
        /// </summary>
        [EditorPropertyDisplayName("Field Of View")]
        [EditorPropertyOrder(2)]
        public float FieldOfView {
            get { return FieldOfViewValue; }
            set { FieldOfViewValue = CameraProjectionUtils.ClampFieldOfView(value); }
        }

        /// <summary>
        /// Gets or sets the near clip-plane distance used for perspective projection creation.
        /// </summary>
        [EditorPropertyDisplayName("Near Plane Distance")]
        [EditorPropertyOrder(3)]
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
        [EditorPropertyOrder(4)]
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
        [EditorPropertyOrder(5)]
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
        public IRenderQueue2D RenderQueue2D { get { return RenderList2D; } }

        /// <summary>
        /// Gets the 3D render queue registered for this camera.
        /// </summary>
        [EditorPropertyHidden]
        public IRenderQueue3D RenderQueue3D { get { return RenderList3D; } }

        /// <summary>
        /// Gets or sets the layer mask this camera renders.
        /// </summary>
        [EditorPropertyDisplayName("Layer Mask")]
        [EditorPropertyOrder(1)]
        public ushort LayerMask {
            get { return LayerMaskValue; }
            set {
                if (LayerMaskValue != value) {
                    if (Parent != null && Parent.IsHierarchyEnabled) {
                        OwnerCore.ObjectManager.RemoveCamera(this);
                        LayerMaskValue = value;
                        RegisterWithObjectManagerIfNeeded();
                    } else {
                        LayerMaskValue = value;
                    }
                }
            }
        }

        /// <summary>
        /// Allocates render lists using the core initialization options.
        /// </summary>
        void InitializeLists() {
            if (OwnerCore == null) {
                // Components may be configured before attachment. The owning entity
                // supplies the authoritative capacities when lifecycle registration runs.
                RenderList2D = new RenderList2D(0);
                RenderList3D = new RenderList3D(0);
                return;
            }
            if (OwnerCore.InitializationOptions == null) {
                throw new InvalidOperationException("Core initialization options must be set before creating camera lists.");
            }

            CoreInitializationOptions settings = OwnerCore.InitializationOptions;
            settings.Normalize();
            int renderList2DInitialCapacity = settings.RenderList2DInitialCapacity;
            int renderList3DInitialCapacity = settings.RenderList3DInitialCapacity;
            if (RenderList2D == null) {
                RenderList2D = new RenderList2D(renderList2DInitialCapacity);
            } else if (RenderList2D.Capacity < renderList2DInitialCapacity) {
                RenderList2D previous = RenderList2D;
                RenderList2D = new RenderList2D(renderList2DInitialCapacity);
                for (int index = 0; index < previous.Count; index++) {
                    RenderList2D.Add(previous[index]);
                }
                previous.Dispose();
            }
            if (RenderList3D == null) {
                RenderList3D = new RenderList3D(renderList3DInitialCapacity);
            } else if (RenderList3D.Capacity < renderList3DInitialCapacity) {
                RenderList3D previous = RenderList3D;
                RenderList3D = new RenderList3D(renderList3DInitialCapacity);
                for (int index = 0; index < previous.Count; index++) {
                    RenderList3D.Add(previous[index]);
                }
                previous.Dispose();
            }
        }

        /// <summary>
        /// Registers the camera when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            InitializeLists();
            RegisterWithObjectManagerIfNeeded();
        }

        /// <summary>
        /// Registers or unregisters the camera based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                RegisterWithObjectManagerIfNeeded();
            } else {
                OwnerCore.ObjectManager.RemoveCamera(this);
            }
        }

        /// <summary>
        /// Unregisters the camera from the object manager when the owning entity removes the component.
        /// </summary>
        /// <param name="entity">Entity losing the camera component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            OwnerCore.ObjectManager.RemoveCamera(this);
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
        /// Registers this camera with the object manager when the owning entity is enabled and no editor suppression marker is attached.
        /// </summary>
        void RegisterWithObjectManagerIfNeeded() {
            if (Parent == null || !Parent.IsHierarchyEnabled) {
                return;
            }
            if (HasEditorSceneCameraSuppressionMarker()) {
                return;
            }

            OwnerCore.ObjectManager.RegisterCamera(this);
        }

        /// <summary>
        /// Returns whether the owning entity currently carries one editor scene-camera suppression marker.
        /// </summary>
        /// <returns>True when the camera should stay out of the runtime camera list during editor authoring; otherwise false.</returns>
        bool HasEditorSceneCameraSuppressionMarker() {
            if (Parent == null || Parent.Components == null) {
                return false;
            }

            for (int componentIndex = 0; componentIndex < Parent.Components.Count; componentIndex++) {
                Component component = Parent.Components[componentIndex];
                if (component != null && component.IsEditorSceneCameraSuppressionMarker) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Releases per-camera render queues and render settings owned by this camera component.
        /// </summary>
        public override void Dispose() {
            NativeOwnership.DisposeAndDelete(RenderList2D);
            NativeOwnership.DisposeAndDelete(RenderList3D);
            NativeOwnership.Delete(RenderSettingsValue);
            RenderList2D = null;
            RenderList3D = null;
            RenderSettingsValue = null;
            RenderTarget = null;
            base.Dispose();
        }
    }
}
