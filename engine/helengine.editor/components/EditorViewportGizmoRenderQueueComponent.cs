namespace helengine.editor {
    /// <summary>
    /// Rebuilds one viewport gizmo camera queue from only the gizmo entities owned by that viewport.
    /// </summary>
    public sealed class EditorViewportGizmoRenderQueueComponent : UpdateComponent {
        /// <summary>
        /// Gizmo overlay camera whose render queue should contain only viewport-owned gizmo drawables.
        /// </summary>
        readonly CameraComponent GizmoCamera;
        /// <summary>
        /// Collector that resolves the viewport-owned gizmo drawables.
        /// </summary>
        readonly EditorViewportGizmoDrawableCollector DrawableCollector;

        /// <summary>
        /// Initializes one queue rebuilder for a viewport-local gizmo camera.
        /// </summary>
        /// <param name="gizmoCamera">Gizmo overlay camera that renders viewport-owned gizmos.</param>
        /// <param name="drawableCollector">Collector that resolves viewport-owned gizmo drawables.</param>
        public EditorViewportGizmoRenderQueueComponent(CameraComponent gizmoCamera, EditorViewportGizmoDrawableCollector drawableCollector) {
            GizmoCamera = gizmoCamera ?? throw new ArgumentNullException(nameof(gizmoCamera));
            DrawableCollector = drawableCollector ?? throw new ArgumentNullException(nameof(drawableCollector));
            UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(Core.Instance.ObjectManager.UpdateOrderLayers - 1);
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
            IRenderQueue3D renderQueue = GizmoCamera.RenderQueue3D;
            renderQueue.Clear();
            DrawableCollector.PopulateRenderQueue(renderQueue);
        }
    }
}
