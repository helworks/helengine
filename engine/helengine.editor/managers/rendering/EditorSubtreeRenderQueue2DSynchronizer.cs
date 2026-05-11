namespace helengine.editor {
    /// <summary>
    /// Rebuilds one camera's 2D render queue from the drawables owned by a specific entity subtree.
    /// </summary>
    public sealed class EditorSubtreeRenderQueue2DSynchronizer {
        /// <summary>
        /// Camera whose 2D render queue should be rebuilt.
        /// </summary>
        readonly CameraComponent TargetCamera;
        /// <summary>
        /// Root entity whose enabled descendants own the drawables that belong in the rebuilt queue.
        /// </summary>
        readonly Entity SubtreeRoot;

        /// <summary>
        /// Initializes one subtree-scoped 2D render-queue synchronizer.
        /// </summary>
        /// <param name="targetCamera">Camera whose render queue should be rebuilt.</param>
        /// <param name="subtreeRoot">Root entity that owns the drawables for the rebuilt queue.</param>
        public EditorSubtreeRenderQueue2DSynchronizer(CameraComponent targetCamera, Entity subtreeRoot) {
            TargetCamera = targetCamera ?? throw new ArgumentNullException(nameof(targetCamera));
            SubtreeRoot = subtreeRoot ?? throw new ArgumentNullException(nameof(subtreeRoot));
        }

        /// <summary>
        /// Clears the target queue and repopulates it from the current enabled subtree drawables.
        /// </summary>
        public void Synchronize() {
            IRenderQueue2D renderQueue = TargetCamera.RenderQueue2D;
            renderQueue.Clear();
            AddDrawables(SubtreeRoot, renderQueue);
        }

        /// <summary>
        /// Adds all enabled 2D drawables found in the supplied subtree to the target queue.
        /// </summary>
        /// <param name="entity">Current subtree entity being traversed.</param>
        /// <param name="renderQueue">Queue receiving the discovered drawables.</param>
        void AddDrawables(Entity entity, IRenderQueue2D renderQueue) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }
            if (!entity.IsHierarchyEnabled) {
                return;
            }

            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is IDrawable2D drawable) {
                        renderQueue.Add(drawable);
                    }
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                Entity child = entity.Children[childIndex];
                if (child == null) {
                    continue;
                }

                AddDrawables(child, renderQueue);
            }
        }
    }
}
