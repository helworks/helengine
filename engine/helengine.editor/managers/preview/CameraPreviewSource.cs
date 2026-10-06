namespace helengine.editor {
    /// <summary>
    /// Preview source that renders one selected scene camera into its own offscreen render target.
    /// </summary>
    public class CameraPreviewSource : IPreviewSource {
        /// <summary>Renders after editor capture cameras and before UI cameras that sample the preview texture.</summary>
        const byte PreviewCameraDrawOrder = EditorUiCameraDrawOrders.SharedUi - 1;
        /// <summary>
        /// Owning renderer used to allocate preview render targets.
        /// </summary>
        readonly RenderManager3D renderManager3D;
        readonly ObjectManager objectManager;
        /// <summary>
        /// Selected scene entity that provides the live camera transform.
        /// </summary>
        readonly Entity sourceEntity;
        /// <summary>
        /// Selected camera component whose state is mirrored into the preview camera.
        /// </summary>
        readonly CameraComponent sourceCameraComponent;
        /// <summary>
        /// Scene-owned canvas profile used to size preview render targets for authored 2D scenes.
        /// </summary>
        readonly EditorSceneCanvasProfileState sceneCanvasProfileState;
        /// <summary>
        /// Hidden editor entity that owns the offscreen preview camera.
        /// </summary>
        readonly EditorEntity previewEntity;
        /// <summary>
        /// Offscreen camera component used by the preview source.
        /// </summary>
        readonly CameraPreviewComponent previewCameraComponent;
        /// <summary>
        /// Current render target used by the preview camera.
        /// </summary>
        RenderTarget renderTarget;
        /// <summary>
        /// Current preview content size.
        /// </summary>
        int2 contentSize;
        /// <summary>
        /// Tracks whether the source has been disposed.
        /// </summary>
        bool isDisposed;

        /// <summary>
        /// Initializes a new camera preview source for one selected scene camera.
        /// </summary>
        /// <param name="sourceEntity">Selected scene entity that owns the live camera.</param>
        /// <param name="sourceCameraComponent">Selected camera component.</param>
        /// <param name="renderManager3D">Renderer used to allocate the offscreen target.</param>
        /// <param name="sceneCanvasProfileState">Scene-owned canvas profile used to size previews.</param>
        public CameraPreviewSource(Entity sourceEntity, CameraComponent sourceCameraComponent, RenderManager3D renderManager3D, EditorSceneCanvasProfileState sceneCanvasProfileState, EditorSessionRendererResources rendererResources) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }
            if (sourceCameraComponent == null) {
                throw new ArgumentNullException(nameof(sourceCameraComponent));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }
            if (!ReferenceEquals(renderManager3D, rendererResources.RenderManager3D)) {
                throw new InvalidOperationException("Camera preview resources must belong to the supplied 3D renderer.");
            }

            this.renderManager3D = renderManager3D;
            objectManager = rendererResources.ObjectManager ?? throw new InvalidOperationException("Camera preview resources must provide an object manager.");
            this.sourceEntity = sourceEntity;
            this.sourceCameraComponent = sourceCameraComponent;
            this.sceneCanvasProfileState = sceneCanvasProfileState;

            Core ownerCore = objectManager.OwnerCore ?? throw new InvalidOperationException("Camera preview object manager must be bound to an owning core.");
            previewEntity = new EditorEntity(ownerCore, EditorEntity.RequireInteractionServices(ownerCore));
            previewEntity.InternalEntity = true;
            previewEntity.LayerMask = EditorLayerMasks.SceneObjects;
            previewCameraComponent = new CameraPreviewComponent();
            previewEntity.AddComponent(previewCameraComponent);

            contentSize = new int2(1, 1);
            ApplyMirroredState();
            Resize(contentSize);
        }

        /// <summary>
        /// Initializes a new camera preview source for one selected scene camera without a shared scene canvas profile.
        /// </summary>
        /// <param name="sourceEntity">Selected scene entity that owns the live camera.</param>
        /// <param name="sourceCameraComponent">Selected camera component.</param>
        /// <param name="renderManager3D">Renderer used to allocate the offscreen target.</param>
        public CameraPreviewSource(Entity sourceEntity, CameraComponent sourceCameraComponent, RenderManager3D renderManager3D, EditorSessionRendererResources rendererResources) {
            this.renderManager3D = renderManager3D ?? throw new ArgumentNullException(nameof(renderManager3D));
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }
            if (!ReferenceEquals(renderManager3D, rendererResources.RenderManager3D)) {
                throw new InvalidOperationException("Camera preview resources must belong to the supplied 3D renderer.");
            }
            objectManager = rendererResources.ObjectManager ?? throw new InvalidOperationException("Camera preview resources must provide an object manager.");
            this.sourceEntity = sourceEntity ?? throw new ArgumentNullException(nameof(sourceEntity));
            this.sourceCameraComponent = sourceCameraComponent ?? throw new ArgumentNullException(nameof(sourceCameraComponent));
            sceneCanvasProfileState = null;

            Core ownerCore = objectManager.OwnerCore ?? throw new InvalidOperationException("Camera preview object manager must be bound to an owning core.");
            previewEntity = new EditorEntity(ownerCore, EditorEntity.RequireInteractionServices(ownerCore));
            previewEntity.InternalEntity = true;
            previewEntity.LayerMask = EditorLayerMasks.SceneObjects;
            previewCameraComponent = new CameraPreviewComponent();
            previewEntity.AddComponent(previewCameraComponent);

            contentSize = new int2(1, 1);
            ApplyMirroredState();
            Resize(contentSize);
        }

        /// <summary>
        /// Gets the preview camera component used by the offscreen source.
        /// </summary>
        public CameraComponent PreviewCamera => previewCameraComponent;

        /// <summary>
        /// Gets the current render target used by the preview camera.
        /// </summary>
        public RenderTarget RenderTarget => renderTarget;

        /// <summary>
        /// Gets the current preview texture exposed by the source.
        /// </summary>
        public RuntimeTexture Texture => renderTarget;

        /// <summary>
        /// Updates the preview camera transform and mirrored state.
        /// </summary>
        public void Update() {
            if (isDisposed) {
                return;
            }
            if (sourceEntity.IsDisposed) {
                Dispose();
                return;
            }

            Resize(contentSize);
        }

        /// <summary>
        /// Resizes the preview render target to match the available panel content size.
        /// </summary>
        /// <param name="contentSize">Usable panel content size in pixels.</param>
        public void Resize(int2 contentSize) {
            if (isDisposed) {
                return;
            }
            if (sourceEntity.IsDisposed) {
                Dispose();
                return;
            }

            this.contentSize = new int2(Math.Max(1, contentSize.X), Math.Max(1, contentSize.Y));
            int2 previewTargetSize = this.contentSize;
            int targetWidth = previewTargetSize.X;
            int targetHeight = previewTargetSize.Y;
            if (renderTarget != null && renderTarget.Width == targetWidth && renderTarget.Height == targetHeight) {
                ApplyMirroredState();
                return;
            }

            DisposeRenderTarget();
            renderTarget = renderManager3D.CreateRenderTarget(targetWidth, targetHeight);
            previewCameraComponent.RenderTarget = renderTarget;
            previewCameraComponent.Viewport = BuildPreviewViewport();
            ApplyMirroredState();
        }

        /// <summary>
        /// Releases the offscreen camera and its render target.
        /// </summary>
        public void Dispose() {
            if (isDisposed) {
                return;
            }

            isDisposed = true;
            DisposeRenderTarget();
            objectManager.RemoveCamera(previewCameraComponent);
            objectManager.RemoveEntity(previewEntity);
            previewEntity.Dispose();
        }

        /// <summary>
        /// Mirrors the selected camera transform and state during creation, resizing, and each live frame.
        /// </summary>
        void ApplyMirroredState() {
            previewEntity.Position = sourceEntity.Position;
            previewEntity.Orientation = sourceEntity.Orientation;
            previewCameraComponent.CameraDrawOrder = PreviewCameraDrawOrder;
            previewCameraComponent.LayerMask = ResolvePreviewLayerMask(sourceCameraComponent.LayerMask);
            previewCameraComponent.ClearSettings = sourceCameraComponent.ClearSettings;
            previewCameraComponent.RenderSettings = new CameraRenderSettings(sourceCameraComponent.RenderSettings);
            previewCameraComponent.FieldOfView = sourceCameraComponent.FieldOfView;
            previewCameraComponent.NearPlaneDistance = sourceCameraComponent.NearPlaneDistance;
            previewCameraComponent.FarPlaneDistance = sourceCameraComponent.FarPlaneDistance;
            previewCameraComponent.LogicalViewportSize = ResolveLogicalViewportSize();
            previewCameraComponent.Viewport = BuildPreviewViewport();
            EditorViewportDirect2DPresentationService.SynchronizeViewportOwnedSceneQueue(previewCameraComponent, objectManager);
        }

        /// <summary>
        /// Resolves the preview layer mask so editor-loaded scene cameras can still see authored scene objects.
        /// </summary>
        /// <param name="sourceLayerMask">Authored layer mask mirrored from the selected camera.</param>
        /// <returns>Layer mask that should be applied to the preview camera.</returns>
        ushort ResolvePreviewLayerMask(ushort sourceLayerMask) {
            if (sourceEntity is EditorEntity) {
                return EditorLayerMasks.SceneObjects;
            }

            return sourceLayerMask;
        }

        /// <summary>
        /// Disposes the current render target when one is owned.
        /// </summary>
        void DisposeRenderTarget() {
            if (renderTarget is IDisposable disposableTarget) {
                disposableTarget.Dispose();
            }

            previewCameraComponent.RenderTarget = null;
            renderTarget = null;
        }

        /// <summary>
        /// Builds the physical viewport matching the current preview panel resolution.
        /// </summary>
        /// <returns>Viewport applied to the preview camera.</returns>
        float4 BuildPreviewViewport() {
            int2 previewSize = contentSize;
            return new float4(0f, 0f, previewSize.X, previewSize.Y);
        }

        /// <summary>
        /// Resolves the authored logical canvas independently of the preview render-target size.
        /// </summary>
        /// <returns>Logical dimensions used to project the camera's 2D content.</returns>
        int2 ResolveLogicalViewportSize() {
            if (sceneCanvasProfileState != null) {
                return new int2(
                    Math.Max(1, sceneCanvasProfileState.CanvasWidth),
                    Math.Max(1, sceneCanvasProfileState.CanvasHeight));
            }
            if (EditorSceneCameraSuppressionService.GetSuppressionState(sourceCameraComponent) != null) {
                return new int2(
                    Math.Max(1, (int)Math.Round(sourceCameraComponent.Viewport.Z)),
                    Math.Max(1, (int)Math.Round(sourceCameraComponent.Viewport.W)));
            }

            return new int2(Math.Max(1, contentSize.X), Math.Max(1, contentSize.Y));
        }
    }
}
