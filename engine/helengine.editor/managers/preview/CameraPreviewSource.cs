namespace helengine.editor {
    /// <summary>
    /// Preview source that renders one selected scene camera into its own offscreen render target.
    /// </summary>
    public class CameraPreviewSource : IPreviewSource {
        /// <summary>
        /// Owning renderer used to allocate preview render targets.
        /// </summary>
        readonly RenderManager3D renderManager3D;
        /// <summary>
        /// Selected scene entity that provides the live camera transform.
        /// </summary>
        readonly Entity sourceEntity;
        /// <summary>
        /// Selected camera component whose state is mirrored into the preview camera.
        /// </summary>
        readonly CameraComponent sourceCameraComponent;
        /// <summary>
        /// Authored suppression state captured from the selected camera, when present.
        /// </summary>
        readonly EditorSceneCameraSuppressionComponent suppressionState;
        /// <summary>
        /// Hidden editor entity that owns the offscreen preview camera.
        /// </summary>
        readonly EditorEntity previewEntity;
        /// <summary>
        /// Offscreen camera component used by the preview source.
        /// </summary>
        readonly CameraComponent previewCameraComponent;
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
        public CameraPreviewSource(Entity sourceEntity, CameraComponent sourceCameraComponent, RenderManager3D renderManager3D) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }
            if (sourceCameraComponent == null) {
                throw new ArgumentNullException(nameof(sourceCameraComponent));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }

            this.renderManager3D = renderManager3D;
            this.sourceEntity = sourceEntity;
            this.sourceCameraComponent = sourceCameraComponent;
            suppressionState = EditorSceneCameraSuppressionService.GetSuppressionState(sourceCameraComponent);

            previewEntity = new EditorEntity();
            previewEntity.InternalEntity = true;
            previewEntity.LayerMask = EditorLayerMasks.SceneObjects;
            previewCameraComponent = new CameraComponent();
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

            previewEntity.Position = sourceEntity.Position;
            previewEntity.Orientation = sourceEntity.Orientation;
            ApplyMirroredState();
        }

        /// <summary>
        /// Resizes the preview render target to match the available panel content size.
        /// </summary>
        /// <param name="contentSize">Usable panel content size in pixels.</param>
        public void Resize(int2 contentSize) {
            if (isDisposed) {
                return;
            }

            int targetWidth = Math.Max(1, contentSize.X);
            int targetHeight = Math.Max(1, contentSize.Y);
            if (renderTarget != null && renderTarget.Width == targetWidth && renderTarget.Height == targetHeight) {
                this.contentSize = new int2(targetWidth, targetHeight);
                previewCameraComponent.Viewport = new float4(0f, 0f, targetWidth, targetHeight);
                return;
            }

            DisposeRenderTarget();
            renderTarget = renderManager3D.CreateRenderTarget(targetWidth, targetHeight);
            previewCameraComponent.RenderTarget = renderTarget;
            previewCameraComponent.Viewport = new float4(0f, 0f, targetWidth, targetHeight);
            this.contentSize = new int2(targetWidth, targetHeight);
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
            Core.Instance.ObjectManager.RemoveCamera(previewCameraComponent);
            Core.Instance.ObjectManager.RemoveEntity(previewEntity);
            previewEntity.Dispose();
        }

        /// <summary>
        /// Mirrors the selected camera state into the preview camera.
        /// </summary>
        void ApplyMirroredState() {
            byte cameraDrawOrder;
            ushort layerMask;
            CameraClearSettings clearSettings;
            if (suppressionState != null) {
                cameraDrawOrder = suppressionState.CameraDrawOrder;
                layerMask = suppressionState.LayerMask;
                clearSettings = suppressionState.ClearSettings;
            } else {
                cameraDrawOrder = sourceCameraComponent.CameraDrawOrder;
                layerMask = sourceCameraComponent.LayerMask;
                clearSettings = sourceCameraComponent.ClearSettings;
            }

            previewCameraComponent.CameraDrawOrder = cameraDrawOrder;
            previewCameraComponent.LayerMask = layerMask;
            previewCameraComponent.ClearSettings = clearSettings;
            previewCameraComponent.Viewport = new float4(0f, 0f, Math.Max(1, contentSize.X), Math.Max(1, contentSize.Y));
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
    }
}
