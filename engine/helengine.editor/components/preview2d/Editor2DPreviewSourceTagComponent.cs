namespace helengine {
    /// <summary>
    /// Stores the authored 2D entity and component mirrored by one editor-only world-space preview proxy.
    /// </summary>
    public sealed class Editor2DPreviewSourceTagComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Initializes one preview-source tag for the supplied authored entity and component.
        /// </summary>
        /// <param name="sourceEntity">Authored entity mirrored by the preview proxy.</param>
        /// <param name="sourceComponent">Authored 2D component mirrored by the preview proxy.</param>
        public Editor2DPreviewSourceTagComponent(Entity sourceEntity, Component sourceComponent) {
            SourceEntity = sourceEntity ?? throw new ArgumentNullException(nameof(sourceEntity));
            SourceComponent = sourceComponent ?? throw new ArgumentNullException(nameof(sourceComponent));
        }

        /// <summary>
        /// Gets the authored entity mirrored by the preview proxy.
        /// </summary>
        public Entity SourceEntity { get; }

        /// <summary>
        /// Gets the authored 2D component mirrored by the preview proxy.
        /// </summary>
        public Component SourceComponent { get; }
    }
}
