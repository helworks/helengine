namespace helengine.editor {
    /// <summary>
    /// Rebuilds one viewport gizmo camera queue from only the gizmo entities owned by that viewport.
    /// </summary>
    [RunInEditor]
    public sealed class EditorViewportGizmoRenderQueueComponent : UpdateComponent {
        /// <summary>
        /// Scene camera whose current projection the overlay camera follows.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Gizmo overlay camera whose render queue should contain only viewport-owned gizmo drawables.
        /// </summary>
        readonly CameraComponent GizmoCamera;
        /// <summary>
        /// Collector that resolves the viewport-owned gizmo drawables.
        /// </summary>
        readonly EditorViewportGizmoDrawableCollector DrawableCollector;
        readonly ObjectManager ObjectManager;

        /// <summary>
        /// Initializes one queue rebuilder for a viewport-local gizmo camera.
        /// </summary>
        /// <param name="sceneCamera">Scene camera whose projection should match the overlay.</param>
        /// <param name="gizmoCamera">Gizmo overlay camera that renders viewport-owned gizmos.</param>
        /// <param name="drawableCollector">Collector that resolves viewport-owned gizmo drawables.</param>
        /// <param name="objectManager">Object manager that determines the queue rebuild update order.</param>
        public EditorViewportGizmoRenderQueueComponent(CameraComponent sceneCamera, CameraComponent gizmoCamera, EditorViewportGizmoDrawableCollector drawableCollector, ObjectManager objectManager) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            GizmoCamera = gizmoCamera ?? throw new ArgumentNullException(nameof(gizmoCamera));
            DrawableCollector = drawableCollector ?? throw new ArgumentNullException(nameof(drawableCollector));
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            UpdateOrder = ObjectManager.GetUpdateOrderForLayer(ObjectManager.UpdateOrderLayers - 1);
        }

        /// <summary>
        /// Rebuilds the gizmo render queue each frame so sibling viewports do not leak into this viewport.
        /// </summary>
        public override void Update() {
            RebuildRenderQueue();
        }

        /// <summary>
        /// Initializes the queue immediately when the component is attached.
        /// </summary>
        /// <param name="entity">Entity that owns the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RebuildRenderQueue();
        }

        /// <summary>
        /// Clears and repopulates the gizmo camera queue from viewport-owned drawables only.
        /// </summary>
        void RebuildRenderQueue() {
            GizmoCamera.Viewport = SceneCamera.Viewport;
            EditorViewportCameraProjectionSynchronizer.Synchronize(SceneCamera, GizmoCamera);
            IRenderQueue3D renderQueue = GizmoCamera.RenderQueue3D;
            renderQueue.Clear();
            DrawableCollector.PopulateRenderQueue(renderQueue);
        }
    }
}
