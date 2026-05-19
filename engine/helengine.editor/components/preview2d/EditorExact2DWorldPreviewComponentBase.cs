namespace helengine {
    /// <summary>
    /// Provides the shared exact-preview behavior used by editor-only world-space text and rounded-rectangle preview components.
    /// </summary>
    public abstract class EditorExact2DWorldPreviewComponentBase : EditorWorldSpace2DPreviewComponentBase {
        /// <summary>
        /// Editor-only capture service that owns the hidden clone scene and offscreen render target.
        /// </summary>
        helengine.editor.EditorExact2DPreviewCaptureService CaptureServiceValue;

        /// <summary>
        /// Last texture-affecting render-state snapshot captured for this preview.
        /// </summary>
        helengine.editor.EditorExact2DPreviewRenderState CapturedRenderStateValue;

        /// <summary>
        /// Last preview size rendered by the capture service.
        /// </summary>
        int2 CapturedPreviewSizeValue;

        /// <summary>
        /// Initializes one exact world-space preview component bound to the supplied authored source entity.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview proxy.</param>
        protected EditorExact2DWorldPreviewComponentBase(Entity sourceEntity)
            : base(sourceEntity) {
        }

        /// <summary>
        /// Gets the current capture count reported by the owned preview capture service.
        /// </summary>
        public int CaptureCount {
            get {
                if (CaptureServiceValue == null) {
                    return 0;
                }

                return CaptureServiceValue.CaptureCount;
            }
        }

        /// <summary>
        /// Creates the preview capture service before the shared mesh/material initialization runs.
        /// </summary>
        /// <param name="entity">Preview entity that owns this component.</param>
        public override void ComponentAdded(Entity entity) {
            CaptureServiceValue = new helengine.editor.EditorExact2DPreviewCaptureService(Core.Instance.RenderManager3D);
            base.ComponentAdded(entity);
        }

        /// <summary>
        /// Disposes the owned preview capture service after the shared proxy cleanup completes.
        /// </summary>
        /// <param name="entity">Preview entity that owned this component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            if (CaptureServiceValue != null) {
                CaptureServiceValue.Dispose();
            }

            CaptureServiceValue = null;
            CapturedRenderStateValue = null;
            CapturedPreviewSizeValue = default;
        }

        /// <summary>
        /// Creates the runtime material backed by the exact preview capture service.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material that samples the capture-service preview texture.</returns>
        protected sealed override RuntimeMaterial CreatePreviewMaterial(RenderManager3D render3D) {
            if (CaptureServiceValue == null) {
                throw new InvalidOperationException("Exact 2D preview capture service must be created before preview materials are requested.");
            }

            return CaptureServiceValue.PreviewMaterial;
        }

        /// <summary>
        /// Resolves the current exact preview render target after synchronizing capture state when needed.
        /// </summary>
        /// <returns>Current preview render target, or the shared white pixel texture before the first capture completes.</returns>
        protected sealed override RuntimeTexture ResolvePreviewTexture() {
            EnsurePreviewCaptureIsSynchronized();
            if (CaptureServiceValue.PreviewRenderTarget != null) {
                return CaptureServiceValue.PreviewRenderTarget;
            }

            return TextureUtils.PixelTexture;
        }

        /// <summary>
        /// Prevents the shared base from releasing the runtime material directly because the capture service owns that material.
        /// </summary>
        /// <param name="render3D">Renderer that would normally release the runtime material.</param>
        /// <param name="previewMaterial">Runtime material owned by the capture service.</param>
        protected sealed override void ReleasePreviewMaterial(RenderManager3D render3D, RuntimeMaterial previewMaterial) {
        }

        /// <summary>
        /// Captures the current texture-affecting render state for this exact preview.
        /// </summary>
        /// <returns>Texture-affecting render-state snapshot for the current source component.</returns>
        protected abstract helengine.editor.EditorExact2DPreviewRenderState CaptureRenderState();

        /// <summary>
        /// Synchronizes the hidden capture scene with the authored source component when a preview recapture is required.
        /// </summary>
        /// <param name="captureService">Capture service that owns the hidden clone scene.</param>
        /// <param name="previewSize">Preview size that must be rendered.</param>
        protected abstract void CapturePreview(helengine.editor.EditorExact2DPreviewCaptureService captureService, int2 previewSize);

        /// <summary>
        /// Synchronizes the preview capture service when visible state or preview size changed.
        /// </summary>
        void EnsurePreviewCaptureIsSynchronized() {
            if (CaptureServiceValue == null) {
                throw new InvalidOperationException("Exact 2D preview capture service must exist before preview synchronization.");
            }

            int2 previewSize = ResolvePreviewSize();
            helengine.editor.EditorExact2DPreviewRenderState nextState = CaptureRenderState();
            bool sizeChanged = !CapturedPreviewSizeValue.Equals(previewSize);
            bool hasState = CapturedRenderStateValue != null;
            if (!hasState || sizeChanged || helengine.editor.EditorExact2DPreviewDirtyStateComparer.RequiresRecapture(CapturedRenderStateValue, nextState)) {
                CapturePreview(CaptureServiceValue, previewSize);
                CapturedRenderStateValue = nextState;
                CapturedPreviewSizeValue = previewSize;
            }
        }
    }
}
