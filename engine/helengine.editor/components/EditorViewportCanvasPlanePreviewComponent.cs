namespace helengine.editor {
    /// <summary>
    /// Maintains the offscreen 2D preview camera and world-space plane used to display the simulated canvas in the scene viewport.
    /// </summary>
    public sealed class EditorViewportCanvasPlanePreviewComponent : UpdateComponent {
        /// <summary>
        /// Camera shown in the scene viewport that must render the world-space canvas plane.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Viewport-local simulated canvas settings that define target size and plane scale.
        /// </summary>
        readonly EditorViewportCanvasPreviewSettings Settings;
        /// <summary>
        /// Renderer used to create preview render targets and runtime mesh resources.
        /// </summary>
        readonly RenderManager3D Render3D;

        /// <summary>
        /// Standalone editor entity that owns the offscreen preview camera.
        /// </summary>
        EditorEntity PreviewCameraEntity;
        /// <summary>
        /// Offscreen camera component used to render the authoritative 2D scene into a sampleable target.
        /// </summary>
        CameraComponent PreviewCameraComponent;
        /// <summary>
        /// Standalone editor entity that displays the preview texture as a world-space plane.
        /// </summary>
        EditorEntity PlaneEntityValue;
        /// <summary>
        /// Mesh component attached to the world-space preview plane.
        /// </summary>
        MeshComponent PlaneMeshComponent;
        /// <summary>
        /// Runtime material used by the preview plane.
        /// </summary>
        RuntimeMaterial PlaneMaterial;
        /// <summary>
        /// Current offscreen render target used by the preview camera.
        /// </summary>
        RenderTarget PreviewRenderTargetValue;
        /// <summary>
        /// Last canvas width applied to the render target and plane transform.
        /// </summary>
        int CurrentCanvasWidth;
        /// <summary>
        /// Last canvas height applied to the render target and plane transform.
        /// </summary>
        int CurrentCanvasHeight;
        /// <summary>
        /// Last pixels-per-world-unit value applied to the plane transform.
        /// </summary>
        int CurrentPixelsPerWorldUnit;
        /// <summary>
        /// Tracks whether the preview entities were created.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Initializes a new preview component for one scene viewport camera and simulated canvas settings source.
        /// </summary>
        /// <param name="sceneCamera">Scene viewport camera that will render the world-space preview plane.</param>
        /// <param name="settings">Viewport-local simulated canvas settings.</param>
        /// <param name="render3D">Renderer used to allocate preview resources.</param>
        public EditorViewportCanvasPlanePreviewComponent(
            CameraComponent sceneCamera,
            EditorViewportCanvasPreviewSettings settings,
            RenderManager3D render3D) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Render3D = render3D ?? throw new ArgumentNullException(nameof(render3D));
        }

        /// <summary>
        /// Gets the current offscreen render target used by the preview camera.
        /// </summary>
        public RenderTarget PreviewRenderTarget => PreviewRenderTargetValue;

        /// <summary>
        /// Gets the offscreen camera component used to render the preview texture.
        /// </summary>
        public CameraComponent PreviewCamera => PreviewCameraComponent;

        /// <summary>
        /// Gets the world-space plane entity that displays the preview texture.
        /// </summary>
        public EditorEntity PlaneEntity => PlaneEntityValue;

        /// <summary>
        /// Creates the offscreen preview camera, render target, and world-space plane when attached to the scene camera entity.
        /// </summary>
        /// <param name="entity">Owning scene camera entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            if (IsInitialized) {
                return;
            }

            PreviewCameraEntity = CreatePreviewCameraEntity();
            PreviewCameraComponent = CreatePreviewCameraComponent();
            PreviewCameraEntity.AddComponent(PreviewCameraComponent);

            EnsureRenderTargetMatchesSettings();
            PlaneEntityValue = EditorViewportCanvasPlaneFactory.Create(Render3D, PreviewRenderTargetValue);
            PlaneMeshComponent = AssertPlaneMeshComponent(PlaneEntityValue);
            PlaneMaterial = PlaneMeshComponent.Material ?? throw new InvalidOperationException("Canvas plane material must exist after plane creation.");
            SynchronizePlaneTransform();
            IsInitialized = true;
        }

        /// <summary>
        /// Releases the preview render target and editor-only entities when removed from the scene camera entity.
        /// </summary>
        /// <param name="entity">Owning scene camera entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisposePreviewRenderTarget();
            DisposePreviewEntity();
            DisposePlaneEntity();
            IsInitialized = false;
        }

        /// <summary>
        /// Synchronizes preview resources with the current canvas settings.
        /// </summary>
        public override void Update() {
            if (!IsInitialized) {
                return;
            }

            EnsureRenderTargetMatchesSettings();
            PreviewCameraComponent.RenderQueue3D.Clear();
            SynchronizePlaneTransform();
        }

        /// <summary>
        /// Creates the standalone editor entity that owns the offscreen preview camera.
        /// </summary>
        /// <returns>Configured internal preview-camera entity.</returns>
        EditorEntity CreatePreviewCameraEntity() {
            return new EditorEntity {
                Name = "Viewport Canvas Preview Camera",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneObjects
            };
        }

        /// <summary>
        /// Creates the offscreen camera component used to render the preview texture.
        /// </summary>
        /// <returns>Configured preview camera component.</returns>
        CameraComponent CreatePreviewCameraComponent() {
            return new CameraComponent {
                CameraDrawOrder = SceneCamera.CameraDrawOrder,
                LayerMask = EditorLayerMasks.SceneObjects,
                ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0),
                Viewport = new float4(0f, 0f, Settings.CanvasWidth, Settings.CanvasHeight)
            };
        }

        /// <summary>
        /// Rebuilds the preview render target when the simulated canvas size changes.
        /// </summary>
        void EnsureRenderTargetMatchesSettings() {
            int nextCanvasWidth = Math.Max(1, Settings.CanvasWidth);
            int nextCanvasHeight = Math.Max(1, Settings.CanvasHeight);
            int nextPixelsPerWorldUnit = Math.Max(1, Settings.PixelsPerWorldUnit);
            bool sizeChanged = PreviewRenderTargetValue == null ||
                               nextCanvasWidth != CurrentCanvasWidth ||
                               nextCanvasHeight != CurrentCanvasHeight;
            if (sizeChanged) {
                DisposePreviewRenderTarget();
                PreviewRenderTargetValue = Render3D.CreateRenderTarget(nextCanvasWidth, nextCanvasHeight);
                PreviewCameraComponent.RenderTarget = PreviewRenderTargetValue;
                if (PlaneMaterial != null) {
                    PlaneMaterial.Properties.SetTexture("CanvasTexture", PreviewRenderTargetValue);
                }
            }

            PreviewCameraComponent.Viewport = new float4(0f, 0f, nextCanvasWidth, nextCanvasHeight);
            CurrentCanvasWidth = nextCanvasWidth;
            CurrentCanvasHeight = nextCanvasHeight;
            CurrentPixelsPerWorldUnit = nextPixelsPerWorldUnit;
        }

        /// <summary>
        /// Positions and scales the world-space canvas plane so its bottom-left corner remains at the world origin.
        /// </summary>
        void SynchronizePlaneTransform() {
            if (PlaneEntityValue == null) {
                return;
            }

            float planeWidth = CurrentCanvasWidth / (float)CurrentPixelsPerWorldUnit;
            float planeHeight = CurrentCanvasHeight / (float)CurrentPixelsPerWorldUnit;
            PlaneEntityValue.LocalPosition = new float3(planeWidth * 0.5f, planeHeight * 0.5f, 0f);
            PlaneEntityValue.LocalScale = new float3(planeWidth, planeHeight, 1f);
        }

        /// <summary>
        /// Returns the mesh component attached to the supplied plane entity.
        /// </summary>
        /// <param name="planeEntity">Plane entity whose mesh component should be resolved.</param>
        /// <returns>Plane mesh component.</returns>
        MeshComponent AssertPlaneMeshComponent(EditorEntity planeEntity) {
            if (planeEntity == null) {
                throw new ArgumentNullException(nameof(planeEntity));
            }

            for (int componentIndex = 0; componentIndex < planeEntity.Components.Count; componentIndex++) {
                if (planeEntity.Components[componentIndex] is MeshComponent meshComponent) {
                    return meshComponent;
                }
            }

            throw new InvalidOperationException("Canvas plane entity must contain a mesh component.");
        }

        /// <summary>
        /// Disposes the current preview render target when one is owned.
        /// </summary>
        void DisposePreviewRenderTarget() {
            if (PreviewRenderTargetValue is IDisposable disposableRenderTarget) {
                disposableRenderTarget.Dispose();
            }

            if (PreviewCameraComponent != null) {
                PreviewCameraComponent.RenderTarget = null;
            }

            PreviewRenderTargetValue = null;
        }

        /// <summary>
        /// Removes and disposes the standalone preview-camera entity.
        /// </summary>
        void DisposePreviewEntity() {
            if (PreviewCameraComponent != null) {
                Core.Instance.ObjectManager.RemoveCamera(PreviewCameraComponent);
            }
            if (PreviewCameraEntity != null) {
                Core.Instance.ObjectManager.RemoveEntity(PreviewCameraEntity);
                PreviewCameraEntity.Dispose();
            }

            PreviewCameraComponent = null;
            PreviewCameraEntity = null;
        }

        /// <summary>
        /// Removes and disposes the world-space preview-plane entity.
        /// </summary>
        void DisposePlaneEntity() {
            if (PlaneEntityValue != null) {
                Core.Instance.ObjectManager.RemoveEntity(PlaneEntityValue);
                PlaneEntityValue.Dispose();
            }

            PlaneEntityValue = null;
            PlaneMeshComponent = null;
            PlaneMaterial = null;
        }
    }
}
