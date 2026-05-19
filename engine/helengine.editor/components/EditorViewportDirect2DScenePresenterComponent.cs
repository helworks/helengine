namespace helengine.editor {
    /// <summary>
    /// Exposes the direct 2D scene-presentation space used by one editor viewport when scene 2D content renders straight through the scene camera.
    /// </summary>
    public sealed class EditorViewportDirect2DScenePresenterComponent : UpdateComponent {
        /// <summary>
        /// Camera that renders the viewport's scene content.
        /// </summary>
        readonly CameraComponent SceneCamera;

        /// <summary>
        /// Viewport component that resolves the authoritative pixel-space viewport rectangle for the scene camera.
        /// </summary>
        readonly ViewportComponent SceneViewportComponent;

        /// <summary>
        /// Cached world-presented 2D size where one viewport pixel equals one world unit.
        /// </summary>
        int2 PresentedWorldSizeValue;

        /// <summary>
        /// Initializes a new direct 2D scene presenter for one scene viewport.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that renders the viewport.</param>
        /// <param name="sceneViewportComponent">Viewport component that resolves the scene camera viewport rectangle.</param>
        public EditorViewportDirect2DScenePresenterComponent(CameraComponent sceneCamera, ViewportComponent sceneViewportComponent) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            SceneViewportComponent = sceneViewportComponent ?? throw new ArgumentNullException(nameof(sceneViewportComponent));
        }

        /// <summary>
        /// Gets the current world-presented 2D size for the viewport where one pixel maps to one world unit.
        /// </summary>
        public int2 PresentedWorldSize {
            get { return PresentedWorldSizeValue; }
        }

        /// <summary>
        /// Recomputes the current world-presented 2D size from the viewport component each frame.
        /// </summary>
        public override void Update() {
            base.Update();

            CameraComponent boundCameraComponent = SceneViewportComponent.GetBoundCameraComponent();
            if (boundCameraComponent != null && !ReferenceEquals(boundCameraComponent, SceneCamera)) {
                throw new InvalidOperationException("Direct 2D viewport presentation must be bound to the same scene camera that owns the presenter.");
            }

            PresentedWorldSizeValue = EditorViewportDirect2DPresentationService.ResolvePresentedWorldSize(Parent, SceneViewportComponent);
            EditorViewportDirect2DPresentationService.SynchronizeViewportOwnedSceneQueue(SceneCamera);
        }
    }
}
