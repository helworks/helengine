namespace helengine.editor {
    /// <summary>
    /// Owns renderer-backed editor visual and preview geometry for one session.
    /// </summary>
    public sealed class EditorSessionRendererResources : IDisposable {
        public RenderManager3D RenderManager3D { get; }
        public RenderManager2D RenderManager2D { get; }
        public ObjectManager ObjectManager { get; }
        public IEntityFactory EntityFactory { get; }
        public EditorSceneEntityIdAllocator SceneEntityIdAllocator { get; }
        public FontAsset DefaultFontAsset { get; private set; }
        public EditorCameraVisualResources CameraVisuals { get; }
        public EditorDirectionalLightVisualResources DirectionalLightVisuals { get; }
        public EditorPointLightVisualResources PointLightVisuals { get; }
        public EditorSpotLightVisualResources SpotLightVisuals { get; }
        public EditorWorldSpace2DPreviewMeshResources WorldSpace2DPreviewMeshes { get; }
        public EditorViewportBorderGizmoMeshResources ViewportBorderGizmoMeshes { get; }

        bool IsDisposed;

        /// <summary>Creates all renderer-backed editor resources for one renderer owner.</summary>
        public EditorSessionRendererResources(RenderManager3D renderManager3D, RenderManager2D renderManager2D, ObjectManager objectManager, IEntityFactory entityFactory, EditorSceneEntityIdAllocator sceneEntityIdAllocator, FontAsset defaultFontAsset = null) {
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            EntityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
            SceneEntityIdAllocator = sceneEntityIdAllocator ?? throw new ArgumentNullException(nameof(sceneEntityIdAllocator));
            RenderManager3D = renderManager3D;
            RenderManager2D = renderManager2D ?? throw new ArgumentNullException(nameof(renderManager2D));
            DefaultFontAsset = defaultFontAsset;

            CameraVisuals = new EditorCameraVisualResources(renderManager3D);
            DirectionalLightVisuals = new EditorDirectionalLightVisualResources(renderManager3D);
            PointLightVisuals = new EditorPointLightVisualResources(renderManager3D);
            SpotLightVisuals = new EditorSpotLightVisualResources(renderManager3D);
            WorldSpace2DPreviewMeshes = new EditorWorldSpace2DPreviewMeshResources(renderManager3D);
            ViewportBorderGizmoMeshes = new EditorViewportBorderGizmoMeshResources(renderManager3D);
        }

        /// <summary>Releases all models allocated by this renderer resource graph.</summary>
        public void SetDefaultFontAsset(FontAsset defaultFontAsset) {
            if (defaultFontAsset == null) {
                throw new ArgumentNullException(nameof(defaultFontAsset));
            }
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorSessionRendererResources));
            }
            DefaultFontAsset = defaultFontAsset;
        }

        /// <summary>Releases all models allocated by this renderer resource graph.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;

            List<Exception> failures = new List<Exception>();
            DisposeOwner(ViewportBorderGizmoMeshes, failures);
            DisposeOwner(WorldSpace2DPreviewMeshes, failures);
            DisposeOwner(SpotLightVisuals, failures);
            DisposeOwner(PointLightVisuals, failures);
            DisposeOwner(DirectionalLightVisuals, failures);
            DisposeOwner(CameraVisuals, failures);
            if (failures.Count > 0) {
                throw failures.Count == 1
                    ? failures[0]
                    : new AggregateException("Editor renderer resource disposal failed.", failures);
            }

        }

        static void DisposeOwner(IDisposable owner, List<Exception> failures) {
            try {
                owner.Dispose();
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
    }
}
