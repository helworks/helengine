namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only arrow attached to authored directional light entities.
    /// </summary>
    public class EditorDirectionalLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly helengine.EditorDirectionalLightVisualResources VisualResources;

        public EditorDirectionalLightVisualComponent(
            helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache,
            helengine.EditorDirectionalLightVisualResources visualResources) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            VisualResources = visualResources ?? throw new ArgumentNullException(nameof(visualResources));
        }

        /// <summary>
        /// Resolves the shared directional-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Directional-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = VisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
