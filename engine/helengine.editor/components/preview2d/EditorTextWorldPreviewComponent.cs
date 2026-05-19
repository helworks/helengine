namespace helengine {
    /// <summary>
    /// Renders one editor-only world-space mesh proxy for an authored text component.
    /// </summary>
    public sealed class EditorTextWorldPreviewComponent : EditorExact2DWorldPreviewComponentBase {
        /// <summary>
        /// Authored text component mirrored by this preview proxy.
        /// </summary>
        readonly TextComponent SourceComponentValue;

        /// <summary>
        /// Initializes one text preview proxy bound to the supplied authored source entity and component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview proxy.</param>
        /// <param name="sourceComponent">Authored text component mirrored by the preview proxy.</param>
        public EditorTextWorldPreviewComponent(Entity sourceEntity, TextComponent sourceComponent)
            : base(sourceEntity) {
            SourceComponentValue = sourceComponent ?? throw new ArgumentNullException(nameof(sourceComponent));
        }

        /// <summary>
        /// Gets the authored text component mirrored by this preview proxy.
        /// </summary>
        public TextComponent SourceComponent => SourceComponentValue;

        /// <summary>
        /// Resolves the authored preview size from the text component.
        /// </summary>
        /// <returns>Text preview size in world units.</returns>
        protected override int2 ResolvePreviewSize() {
            return SourceComponentValue.Size;
        }

        /// <summary>
        /// Captures the current texture-affecting text preview state.
        /// </summary>
        /// <returns>Texture-affecting text preview render state.</returns>
        protected override helengine.editor.EditorExact2DPreviewRenderState CaptureRenderState() {
            return helengine.editor.EditorExact2DPreviewDirtyStateComparer.CaptureTextState(SourceEntity, SourceComponentValue);
        }

        /// <summary>
        /// Synchronizes the hidden exact-preview capture scene with the authored text component.
        /// </summary>
        /// <param name="captureService">Capture service that owns the hidden clone scene.</param>
        /// <param name="previewSize">Preview size that must be rendered.</param>
        protected override void CapturePreview(helengine.editor.EditorExact2DPreviewCaptureService captureService, int2 previewSize) {
            if (captureService == null) {
                throw new ArgumentNullException(nameof(captureService));
            }

            captureService.CaptureTextPreview(SourceEntity, SourceComponentValue, previewSize);
        }
    }
}
