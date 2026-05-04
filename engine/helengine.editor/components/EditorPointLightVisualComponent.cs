namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only visual attached to authored point light entities.
    /// </summary>
    public class EditorPointLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        /// <summary>
        /// Resolves the shared point-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Point-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorPointLightVisualResources.GetRuntimeModel();
            Material = helengine.editor.EngineGeneratedMaterialCache.GetRuntimeMaterial(helengine.editor.EngineGeneratedMaterialCache.StandardAssetId);
            base.ComponentAdded(entity);
        }
    }
}
