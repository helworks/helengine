namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only visual attached to authored point light entities.
    /// </summary>
    public class EditorPointLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly helengine.EditorPointLightVisualResources VisualResources;

        public EditorPointLightVisualComponent(
            helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache,
            helengine.EditorPointLightVisualResources visualResources) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            VisualResources = visualResources ?? throw new ArgumentNullException(nameof(visualResources));
        }

        /// <summary>
        /// Resolves the shared point-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Point-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = VisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
