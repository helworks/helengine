namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only cone attached to authored spot light entities.
    /// </summary>
    public class EditorSpotLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly helengine.EditorSpotLightVisualResources VisualResources;

        public EditorSpotLightVisualComponent(
            helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache,
            helengine.EditorSpotLightVisualResources visualResources) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            VisualResources = visualResources ?? throw new ArgumentNullException(nameof(visualResources));
        }

        /// <summary>
        /// Resolves the shared spot-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Spot-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = VisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
