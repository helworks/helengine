namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only arrow attached to authored directional light entities.
    /// </summary>
    public class EditorDirectionalLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;

        public EditorDirectionalLightVisualComponent(helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
        }

        /// <summary>
        /// Resolves the shared directional-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Directional-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorDirectionalLightVisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
