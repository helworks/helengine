namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only camera icon attached to user camera entities.
    /// </summary>
    public class EditorCameraVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;

        public EditorCameraVisualComponent(helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
        }

        /// <summary>
        /// Registers the shared camera-visual mesh and material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Camera entity that owns the editor-only visual.</param>
        public override void ComponentAdded(Entity entity) {
            Model = EditorCameraVisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
