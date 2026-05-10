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
        /// <param name="gizmoCamera">Overlay camera that renders gizmos on top of scene geometry.</param>
        /// <param name="pickerCameraEntity">Hidden entity used for picker rendering.</param>
        /// <param name="pickerCamera">Hidden camera used for picker rendering.</param>
        /// <param name="pickerRenderTarget">Render target used by the picker camera when supported.</param>
        /// <param name="canvasPlanePreviewComponent">Component that renders the shared scene canvas into the viewport plane.</param>
        /// <param name="translationGizmoRoot">Root entity for the viewport-local translation gizmo.</param>
        /// <param name="rotationGizmoRoot">Root entity for the viewport-local rotation gizmo.</param>
        /// <param name="scaleGizmoRoot">Root entity for the viewport-local scale gizmo.</param>
        public EditorViewportWorkspaceState(
            EditorViewport viewport,
            EditorEntity sceneCameraEntity,
            CameraComponent sceneCamera,
            CameraComponent gizmoCamera,
            EditorEntity pickerCameraEntity,
            CameraComponent pickerCamera,
            RenderTarget pickerRenderTarget,
            EditorViewportCanvasPlanePreviewComponent canvasPlanePreviewComponent,
            EditorEntity translationGizmoRoot,
            EditorEntity rotationGizmoRoot,
            EditorEntity scaleGizmoRoot) {
            Viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            SceneCameraEntity = sceneCameraEntity ?? throw new ArgumentNullException(nameof(sceneCameraEntity));
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            GizmoCamera = gizmoCamera ?? throw new ArgumentNullException(nameof(gizmoCamera));
            PickerCameraEntity = pickerCameraEntity ?? throw new ArgumentNullException(nameof(pickerCameraEntity));
            PickerCamera = pickerCamera ?? throw new ArgumentNullException(nameof(pickerCamera));
            PickerRenderTarget = pickerRenderTarget;
            CanvasPlanePreviewComponent = canvasPlanePreviewComponent ?? throw new ArgumentNullException(nameof(canvasPlanePreviewComponent));
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
        /// Gets the canvas-plane preview component owned by the viewport stack.
        /// </summary>
        public EditorViewportCanvasPlanePreviewComponent CanvasPlanePreviewComponent { get; }
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
