namespace helengine {
    /// <summary>
    /// Renders the hidden editor-only camera icon attached to user camera entities.
    /// </summary>
    public class EditorCameraVisualComponent : MeshComponent, IEditorHiddenComponent {
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly helengine.EditorCameraVisualResources VisualResources;

        public EditorCameraVisualComponent(
            helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache,
            helengine.EditorCameraVisualResources visualResources) {
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            VisualResources = visualResources ?? throw new ArgumentNullException(nameof(visualResources));
        }

        /// <summary>
        /// Registers the shared camera-visual mesh and material before the drawable becomes visible.
        /// </summary>
        /// <param name="entity">Camera entity that owns the editor-only visual.</param>
        public override void ComponentAdded(Entity entity) {
            Model = VisualResources.GetRuntimeModel();
            Materials = new[] { helengine.editor.EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial(GeneratedMaterialCache) };
            base.ComponentAdded(entity);
        }
    }
}
