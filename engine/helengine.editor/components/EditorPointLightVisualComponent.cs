namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only visual attached to authored point light entities.
    /// </summary>
    public class EditorPointLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;

        public EditorPointLightVisualComponent(helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
        }

        /// <summary>
        /// Resolves the shared point-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Point-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorPointLightVisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
