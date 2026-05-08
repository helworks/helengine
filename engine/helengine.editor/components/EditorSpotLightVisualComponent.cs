namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only cone attached to authored spot light entities.
    /// </summary>
    public class EditorSpotLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        /// <summary>
        /// Resolves the shared spot-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Spot-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorSpotLightVisualResources.GetRuntimeModel();
            Material = helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();
            base.ComponentAdded(entity);
        }
    }
}
