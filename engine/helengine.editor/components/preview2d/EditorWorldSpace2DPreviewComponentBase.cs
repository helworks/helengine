namespace helengine {
    /// <summary>
    /// Provides the shared mesh-proxy behavior used by editor-only world-space 2D preview components.
    /// </summary>
    public abstract class EditorWorldSpace2DPreviewComponentBase : MeshComponent, IEditorHiddenComponent {
        /// <summary>
        /// Backing material owned by this preview component.
        /// </summary>
        RuntimeMaterial PreviewMaterialValue;

        /// <summary>
        /// Initializes one world-space preview component bound to the supplied authored source entity.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview proxy.</param>
        protected EditorWorldSpace2DPreviewComponentBase(Entity sourceEntity) {
            SourceEntity = sourceEntity ?? throw new ArgumentNullException(nameof(sourceEntity));
        }

        /// <summary>
        /// Gets the authored source entity mirrored by this preview proxy.
        /// </summary>
        public Entity SourceEntity { get; }

        /// <summary>
        /// Gets the runtime material currently owned by this preview proxy.
        /// </summary>
        protected RuntimeMaterial PreviewMaterial => PreviewMaterialValue;

        /// <summary>
        /// Allocates mesh and material resources when the preview proxy enters the scene.
        /// </summary>
        /// <param name="entity">Preview entity that owns this component.</param>
        public override void ComponentAdded(Entity entity) {
            EditorEntity previewEntity = ResolvePreviewEntity(entity);
            previewEntity.InternalEntity = true;
            previewEntity.LayerMask = helengine.editor.EditorLayerMasks.SceneObjects;
            Model = ResolvePreviewModel();
            PreviewMaterialValue = CreatePreviewMaterial(Core.Instance.RenderManager3D);
            Material = PreviewMaterialValue;
            base.ComponentAdded(entity);
            SynchronizeFromSource();
        }

        /// <summary>
        /// Releases the owned runtime material when the preview proxy leaves the scene.
        /// </summary>
        /// <param name="entity">Preview entity that owned this component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            Core core = Core.Instance;
            if (core != null && core.RenderManager3D != null && PreviewMaterialValue != null) {
                ReleasePreviewMaterial(core.RenderManager3D, PreviewMaterialValue);
            }

            PreviewMaterialValue = null;
            Material = null;
            Model = null;
        }

        /// <summary>
        /// Synchronizes the preview entity transform, enabled state, and render data from the authored 2D source.
        /// </summary>
        public void SynchronizeFromSource() {
            EditorEntity previewEntity = ResolvePreviewEntity(Parent);
            previewEntity.InternalEntity = true;
            previewEntity.LayerMask = helengine.editor.EditorLayerMasks.SceneObjects;
            previewEntity.Enabled = SourceEntity.Enabled;
            previewEntity.LocalPosition = helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldPosition(SourceEntity);
            previewEntity.LocalOrientation = SourceEntity.Orientation;
            previewEntity.LocalScale = ResolvePreviewScale();
            SynchronizePreviewMaterial();
        }

        /// <summary>
        /// Resolves the authored preview size in world units for the current proxy.
        /// </summary>
        /// <returns>World-space preview size for the shared unit quad.</returns>
        protected abstract int2 ResolvePreviewSize();

        /// <summary>
        /// Resolves the runtime texture displayed by the preview material.
        /// </summary>
        /// <returns>Runtime texture shown by the preview proxy.</returns>
        protected abstract RuntimeTexture ResolvePreviewTexture();

        /// <summary>
        /// Resolves the editor preview entity that owns this component.
        /// </summary>
        /// <param name="entity">Owning entity to validate.</param>
        /// <returns>Validated editor preview entity.</returns>
        EditorEntity ResolvePreviewEntity(Entity entity) {
            if (entity is not EditorEntity previewEntity) {
                throw new InvalidOperationException("Editor world-space 2D preview components must be attached to EditorEntity instances.");
            }

            return previewEntity;
        }

        /// <summary>
        /// Creates the runtime material used by the preview mesh.
        /// </summary>
        /// <returns>Configured runtime material for the preview proxy.</returns>
        protected abstract RuntimeMaterial CreatePreviewMaterial(RenderManager3D render3D);

        /// <summary>
        /// Resolves the shared preview mesh model used by this preview proxy.
        /// </summary>
        /// <returns>Runtime model whose local rectangle matches the authored coordinate convention.</returns>
        protected virtual RuntimeModel ResolvePreviewModel() {
            if (helengine.editor.EditorViewportDirect2DPresentationService.TryResolveViewportOwner(SourceEntity, out _, out _)) {
                return EditorWorldSpace2DPreviewMeshResources.GetViewportRuntimeModel();
            }

            return EditorWorldSpace2DPreviewMeshResources.GetRuntimeModel();
        }

        /// <summary>
        /// Releases the runtime material owned by this preview component.
        /// </summary>
        /// <param name="render3D">Renderer that owns the runtime material.</param>
        /// <param name="previewMaterial">Runtime material that should be released.</param>
        protected virtual void ReleasePreviewMaterial(RenderManager3D render3D, RuntimeMaterial previewMaterial) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            } else if (previewMaterial == null) {
                throw new ArgumentNullException(nameof(previewMaterial));
            }

            render3D.ReleaseMaterial(previewMaterial);
        }

        /// <summary>
        /// Updates the preview material texture from the authored source component.
        /// </summary>
        void SynchronizePreviewMaterial() {
            if (PreviewMaterialValue == null) {
                return;
            }

            ApplyPreviewTexture(PreviewMaterialValue, ResolvePreviewTexture());
        }

        /// <summary>
        /// Applies the preview texture binding used by world-space 2D proxy materials.
        /// </summary>
        /// <param name="material">Runtime material that should display the preview texture.</param>
        /// <param name="texture">Runtime texture to bind.</param>
        protected virtual void ApplyPreviewTexture(RuntimeMaterial material, RuntimeTexture texture) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            } else if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            ShaderRuntimeMaterialAccess.Require(material).Properties.SetTexture("PreviewTexture", texture);
        }

        /// <summary>
        /// Converts the authored preview size into a scale for the shared unit quad.
        /// </summary>
        /// <returns>World-space scale for the preview proxy entity.</returns>
        float3 ResolvePreviewScale() {
            int2 previewSize = ResolvePreviewSize();
            int2 clampedPreviewSize = new int2(Math.Max(1, previewSize.X), Math.Max(1, previewSize.Y));
            return helengine.editor.EditorViewportDirect2DPresentationService.ResolvePresentedWorldScale(SourceEntity, clampedPreviewSize);
        }
    }
}
