namespace helengine.editor {
    /// <summary>
    /// Owns the runtime objects required by one workspace-managed viewport instance.
    /// </summary>
    public sealed class EditorViewportWorkspaceState {
        /// <summary>
        /// Initializes one viewport workspace state bundle.
        /// </summary>
        /// <param name="viewport">Dockable viewport panel shown in the workspace.</param>
        /// <param name="sceneCameraEntity">Entity that owns the scene and gizmo cameras.</param>
        /// <param name="sceneCamera">Primary camera that renders scene geometry for the viewport.</param>
        /// <param name="sceneViewportComponent">Viewport component that resolves the scene camera viewport rectangle.</param>
        /// <param name="direct2DScenePresenterComponent">Component that exposes the viewport's direct 2D scene-presentation space.</param>
        /// <param name="worldSpace2DPreviewSyncComponent">Component that maintains shared world-space 2D preview proxies for supported scene entities.</param>
        /// <param name="viewportBorderGizmoSyncComponent">Component that maintains authored viewport border gizmos for the scene.</param>
        /// <param name="gizmoCamera">Overlay camera that renders gizmos on top of scene geometry.</param>
        /// <param name="pickerCameraEntity">Hidden entity used for picker rendering.</param>
        /// <param name="pickerCamera">Hidden camera used for picker rendering.</param>
        /// <param name="pickerRenderTarget">Render target used by the picker camera when supported.</param>
        /// <param name="cameraController">Viewport-local camera controller that owns orbit state and input-driven navigation.</param>
        /// <param name="translationGizmoRoot">Root entity for the viewport-local translation gizmo.</param>
        /// <param name="rotationGizmoRoot">Root entity for the viewport-local rotation gizmo.</param>
        /// <param name="scaleGizmoRoot">Root entity for the viewport-local scale gizmo.</param>
        public EditorViewportWorkspaceState(
            EditorViewport viewport,
            EditorEntity sceneCameraEntity,
            CameraComponent sceneCamera,
            ViewportComponent sceneViewportComponent,
            EditorViewportDirect2DScenePresenterComponent direct2DScenePresenterComponent,
            EditorWorldSpace2DPreviewSyncComponent worldSpace2DPreviewSyncComponent,
            EditorViewportBorderGizmoSyncComponent viewportBorderGizmoSyncComponent,
            CameraComponent gizmoCamera,
            EditorEntity pickerCameraEntity,
            CameraComponent pickerCamera,
            RenderTarget pickerRenderTarget,
            EditorViewportCameraController cameraController,
            EditorEntity translationGizmoRoot,
            EditorEntity rotationGizmoRoot,
            EditorEntity scaleGizmoRoot) {
            Viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            SceneCameraEntity = sceneCameraEntity ?? throw new ArgumentNullException(nameof(sceneCameraEntity));
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            SceneViewportComponent = sceneViewportComponent ?? throw new ArgumentNullException(nameof(sceneViewportComponent));
            Direct2DScenePresenterComponent = direct2DScenePresenterComponent ?? throw new ArgumentNullException(nameof(direct2DScenePresenterComponent));
            WorldSpace2DPreviewSyncComponent = worldSpace2DPreviewSyncComponent ?? throw new ArgumentNullException(nameof(worldSpace2DPreviewSyncComponent));
            ViewportBorderGizmoSyncComponent = viewportBorderGizmoSyncComponent ?? throw new ArgumentNullException(nameof(viewportBorderGizmoSyncComponent));
            GizmoCamera = gizmoCamera ?? throw new ArgumentNullException(nameof(gizmoCamera));
            PickerCameraEntity = pickerCameraEntity ?? throw new ArgumentNullException(nameof(pickerCameraEntity));
            PickerCamera = pickerCamera ?? throw new ArgumentNullException(nameof(pickerCamera));
            PickerRenderTarget = pickerRenderTarget;
            CameraController = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            TranslationGizmoRoot = translationGizmoRoot ?? throw new ArgumentNullException(nameof(translationGizmoRoot));
            RotationGizmoRoot = rotationGizmoRoot ?? throw new ArgumentNullException(nameof(rotationGizmoRoot));
            ScaleGizmoRoot = scaleGizmoRoot ?? throw new ArgumentNullException(nameof(scaleGizmoRoot));
        }

        /// <summary>
        /// Gets the dockable viewport panel shown in the workspace.
        /// </summary>
        public EditorViewport Viewport { get; }
        /// <summary>
        /// Gets the entity that owns the scene and gizmo cameras.
        /// </summary>
        public EditorEntity SceneCameraEntity { get; }
        /// <summary>
        /// Gets the primary camera that renders scene geometry for the viewport.
        /// </summary>
        public CameraComponent SceneCamera { get; }
        /// <summary>
        /// Gets the viewport component that resolves the scene camera viewport rectangle.
        /// </summary>
        public ViewportComponent SceneViewportComponent { get; }
        /// <summary>
        /// Gets the component that exposes the viewport's direct 2D scene-presentation space.
        /// </summary>
        public EditorViewportDirect2DScenePresenterComponent Direct2DScenePresenterComponent { get; }
        /// <summary>
        /// Gets the component that maintains shared world-space 2D preview proxies for the scene.
        /// </summary>
        public EditorWorldSpace2DPreviewSyncComponent WorldSpace2DPreviewSyncComponent { get; }
        /// <summary>
        /// Gets the component that maintains authored viewport border gizmos for the scene.
        /// </summary>
        public EditorViewportBorderGizmoSyncComponent ViewportBorderGizmoSyncComponent { get; }
        /// <summary>
        /// Gets the overlay camera that renders gizmos for the viewport.
        /// </summary>
        public CameraComponent GizmoCamera { get; }
        /// <summary>
        /// Gets the hidden picker-camera entity.
        /// </summary>
        public EditorEntity PickerCameraEntity { get; }
        /// <summary>
        /// Gets the hidden picker camera.
        /// </summary>
        public CameraComponent PickerCamera { get; }
        /// <summary>
        /// Gets the picker render target when picker rendering is supported by the active renderer.
        /// </summary>
        public RenderTarget PickerRenderTarget { get; }
        /// <summary>
        /// Gets the viewport-local camera controller that owns orbit state and camera navigation.
        /// </summary>
        public EditorViewportCameraController CameraController { get; }
        /// <summary>
        /// Gets the root entity for the viewport-local translation gizmo.
        /// </summary>
        public EditorEntity TranslationGizmoRoot { get; }
        /// <summary>
        /// Gets the root entity for the viewport-local rotation gizmo.
        /// </summary>
        public EditorEntity RotationGizmoRoot { get; }
        /// <summary>
        /// Gets the root entity for the viewport-local scale gizmo.
        /// </summary>
        public EditorEntity ScaleGizmoRoot { get; }
    }
}
