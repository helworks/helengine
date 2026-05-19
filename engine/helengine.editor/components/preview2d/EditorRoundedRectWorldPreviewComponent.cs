namespace helengine {
    /// <summary>
    /// Renders one editor-only world-space mesh proxy for an authored rounded-rectangle component.
    /// </summary>
    public sealed class EditorRoundedRectWorldPreviewComponent : EditorExact2DWorldPreviewComponentBase {
        /// <summary>
        /// Authored rounded-rectangle component mirrored by this preview proxy.
        /// </summary>
        readonly RoundedRectComponent SourceComponentValue;

        /// <summary>
        /// Initializes one rounded-rectangle preview proxy bound to the supplied authored source entity and component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview proxy.</param>
        /// <param name="sourceComponent">Authored rounded-rectangle component mirrored by the preview proxy.</param>
        public EditorRoundedRectWorldPreviewComponent(Entity sourceEntity, RoundedRectComponent sourceComponent)
            : base(sourceEntity) {
            SourceComponentValue = sourceComponent ?? throw new ArgumentNullException(nameof(sourceComponent));
        }

        /// <summary>
        /// Gets the authored rounded-rectangle component mirrored by this preview proxy.
        /// </summary>
        public RoundedRectComponent SourceComponent => SourceComponentValue;

        /// <summary>
        /// Resolves the authored preview size from the rounded-rectangle component.
        /// </summary>
        /// <returns>Rounded-rectangle preview size in world units.</returns>
        protected override int2 ResolvePreviewSize() {
            return SourceComponentValue.Size;
        }

        /// <summary>
        /// Captures the current texture-affecting rounded-rectangle preview state.
        /// </summary>
        /// <returns>Texture-affecting rounded-rectangle preview render state.</returns>
        protected override helengine.editor.EditorExact2DPreviewRenderState CaptureRenderState() {
            return helengine.editor.EditorExact2DPreviewDirtyStateComparer.CaptureRoundedRectState(SourceEntity, SourceComponentValue);
        }

        /// <summary>
        /// Synchronizes the hidden exact-preview capture scene with the authored rounded-rectangle component.
        /// </summary>
        /// <param name="captureService">Capture service that owns the hidden clone scene.</param>
        /// <param name="previewSize">Preview size that must be rendered.</param>
        protected override void CapturePreview(helengine.editor.EditorExact2DPreviewCaptureService captureService, int2 previewSize) {
            if (captureService == null) {
                throw new ArgumentNullException(nameof(captureService));
            }

            captureService.CaptureRoundedRectPreview(SourceEntity, SourceComponentValue, previewSize);
        }
    }
}
