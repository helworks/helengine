namespace helengine.editor {
    /// <summary>
    /// Extends the editor with one per-component scene visualization shown while an entity owning the component is selected.
    /// </summary>
    public interface IComponentSceneSelectionEditor {
        /// <summary>
        /// Returns whether this editor visualizes the supplied component.
        /// </summary>
        /// <param name="component">Component attached to the selected entity.</param>
        /// <returns>True when this editor should create a selection visual for the component.</returns>
        bool Supports(Component component);

        /// <summary>
        /// Creates the internal editor entity that visualizes the supplied component while its owner stays selected.
        /// </summary>
        /// <param name="render3D">Renderer used to build the visual's runtime resources.</param>
        /// <param name="generatedMaterialCache">Session-owned generated material cache used by the visual.</param>
        /// <param name="selectedEntity">Currently selected entity that owns the component.</param>
        /// <param name="component">Component being visualized.</param>
        /// <returns>Owned internal visual entity.</returns>
        EditorEntity CreateSelectionVisual(RenderManager3D render3D, EngineGeneratedMaterialCache generatedMaterialCache, Entity selectedEntity, Component component);

        /// <summary>
        /// Synchronizes one previously created visual with the live component and owner transform.
        /// </summary>
        /// <param name="visualEntity">Visual entity previously returned by <see cref="CreateSelectionVisual"/>.</param>
        /// <param name="selectedEntity">Currently selected entity that owns the component.</param>
        /// <param name="component">Component being visualized.</param>
        void UpdateSelectionVisual(EditorEntity visualEntity, Entity selectedEntity, Component component);
    }
}
