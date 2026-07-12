namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only arrow attached to authored directional light entities.
    /// </summary>
    public class EditorDirectionalLightVisualComponent : MeshComponent, IEditorHiddenComponent {
        /// <summary>
        /// Resolves the shared directional-light visual model and editor material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Directional-light visual entity that owns the editor-only mesh.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorDirectionalLightVisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial() };
            base.ComponentAdded(entity);
        }
    }
}
