namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only cone attached to authored spot light entities.
    /// </summary>
    public class EditorSpotLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;

        public EditorSpotLightVisualComponent(helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
        }

        /// <summary>
        /// Resolves the shared spot-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Spot-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorSpotLightVisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
