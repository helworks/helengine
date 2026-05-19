namespace helengine {
    /// <summary>
    /// Renders one editor-only world-space border gizmo that mirrors an authored viewport entity.
    /// </summary>
    public sealed class EditorViewportBorderGizmoComponent : MeshComponent, IEditorHiddenComponent {
        /// <summary>
        /// Authored entity that owns the mirrored viewport component.
        /// </summary>
        readonly Entity SourceEntityValue;

        /// <summary>
        /// Authored viewport component mirrored by this border gizmo.
        /// </summary>
        readonly ViewportComponent SourceViewportComponentValue;

        /// <summary>
        /// Runtime material owned by this border gizmo.
        /// </summary>
        RuntimeMaterial BorderMaterialValue;

        /// <summary>
        /// Initializes one authored viewport border gizmo component.
        /// </summary>
        /// <param name="sourceEntity">Authored entity that owns the viewport component.</param>
        /// <param name="sourceViewportComponent">Viewport component mirrored by the gizmo.</param>
        public EditorViewportBorderGizmoComponent(Entity sourceEntity, ViewportComponent sourceViewportComponent) {
            SourceEntityValue = sourceEntity ?? throw new ArgumentNullException(nameof(sourceEntity));
            SourceViewportComponentValue = sourceViewportComponent ?? throw new ArgumentNullException(nameof(sourceViewportComponent));
        }

        /// <summary>
        /// Gets the authored source entity mirrored by this gizmo.
        /// </summary>
        public Entity SourceEntity => SourceEntityValue;

        /// <summary>
        /// Gets the authored viewport component mirrored by this gizmo.
        /// </summary>
        public ViewportComponent SourceViewportComponent => SourceViewportComponentValue;

        /// <summary>
        /// Allocates the mesh and material resources required by this gizmo.
        /// </summary>
        /// <param name="entity">Editor entity that owns the gizmo component.</param>
        public override void ComponentAdded(Entity entity) {
            EditorEntity gizmoEntity = ResolveEditorEntity(entity);
            gizmoEntity.InternalEntity = true;
            gizmoEntity.LayerMask = helengine.editor.EditorLayerMasks.SceneObjects;
            Model = helengine.editor.EditorViewportBorderGizmoMeshResources.GetRuntimeModel();
            BorderMaterialValue = helengine.editor.EditorViewportBorderGizmoMaterialFactory.Create(Core.Instance.RenderManager3D);
            Material = BorderMaterialValue;
            base.ComponentAdded(entity);
            SynchronizeFromSource();
        }

        /// <summary>
        /// Releases the owned runtime material when the gizmo leaves the scene.
        /// </summary>
        /// <param name="entity">Entity that owned the gizmo component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            Core core = Core.Instance;
            if (core != null && core.RenderManager3D != null && BorderMaterialValue != null) {
                core.RenderManager3D.ReleaseMaterial(BorderMaterialValue);
            }

            BorderMaterialValue = null;
            Material = null;
            Model = null;
        }

        /// <summary>
        /// Synchronizes the gizmo transform and border parameters from the authored viewport component.
        /// </summary>
        public void SynchronizeFromSource() {
            EditorEntity gizmoEntity = ResolveEditorEntity(Parent);
            int2 resolvedSize = helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldSize(SourceEntityValue, SourceViewportComponentValue);
            if (!SourceEntityValue.Enabled || resolvedSize.X <= 0 || resolvedSize.Y <= 0) {
                gizmoEntity.Enabled = false;
                return;
            }

            gizmoEntity.InternalEntity = true;
            gizmoEntity.LayerMask = helengine.editor.EditorLayerMasks.SceneObjects;
            gizmoEntity.Enabled = true;
            gizmoEntity.LocalPosition = helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldPosition(SourceEntityValue);
            gizmoEntity.LocalOrientation = SourceEntityValue.Orientation;
            gizmoEntity.LocalScale = helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldScale(SourceEntityValue, resolvedSize);
            helengine.editor.EditorViewportBorderGizmoParameters.Apply(BorderMaterialValue, resolvedSize.X, resolvedSize.Y);
        }

        /// <summary>
        /// Resolves the editor entity that owns this gizmo component.
        /// </summary>
        /// <param name="entity">Candidate entity to resolve.</param>
        /// <returns>Typed editor entity that owns the gizmo component.</returns>
        EditorEntity ResolveEditorEntity(Entity entity) {
            if (entity is not EditorEntity editorEntity) {
                throw new InvalidOperationException("Viewport border gizmos must be attached to editor entities.");
            }

            return editorEntity;
        }
    }
}
