namespace helengine.editor {
    /// <summary>
    /// Collects the 3D gizmo drawables owned by one editor viewport instance.
    /// </summary>
    public sealed class EditorViewportGizmoDrawableCollector {
        /// <summary>
        /// Resolves additional world-space gizmo entities owned by the viewport, such as axis-label billboards.
        /// </summary>
        readonly Func<IReadOnlyList<EditorEntity>> ResolveAdditionalOwnedEntities;
        /// <summary>
        /// Translation gizmo root owned by the viewport.
        /// </summary>
        readonly EditorEntity TranslationGizmoRoot;
        /// <summary>
        /// Rotation gizmo root owned by the viewport.
        /// </summary>
        readonly EditorEntity RotationGizmoRoot;
        /// <summary>
        /// Scale gizmo root owned by the viewport.
        /// </summary>
        readonly EditorEntity ScaleGizmoRoot;

        /// <summary>
        /// Initializes one collector for a single viewport-owned gizmo stack.
        /// </summary>
        /// <param name="resolveAdditionalOwnedEntities">Callback that resolves additional owned gizmo entities such as axis-label billboards.</param>
        /// <param name="translationGizmoRoot">Translation gizmo root owned by the viewport.</param>
        /// <param name="rotationGizmoRoot">Rotation gizmo root owned by the viewport.</param>
        /// <param name="scaleGizmoRoot">Scale gizmo root owned by the viewport.</param>
        public EditorViewportGizmoDrawableCollector(
            Func<IReadOnlyList<EditorEntity>> resolveAdditionalOwnedEntities,
            EditorEntity translationGizmoRoot,
            EditorEntity rotationGizmoRoot,
            EditorEntity scaleGizmoRoot) {
            ResolveAdditionalOwnedEntities = resolveAdditionalOwnedEntities ?? throw new ArgumentNullException(nameof(resolveAdditionalOwnedEntities));
            TranslationGizmoRoot = translationGizmoRoot ?? throw new ArgumentNullException(nameof(translationGizmoRoot));
            RotationGizmoRoot = rotationGizmoRoot ?? throw new ArgumentNullException(nameof(rotationGizmoRoot));
            ScaleGizmoRoot = scaleGizmoRoot ?? throw new ArgumentNullException(nameof(scaleGizmoRoot));
        }

        /// <summary>
        /// Populates the supplied render queue with only the drawables owned by this viewport's gizmo stack.
        /// </summary>
        /// <param name="renderQueue">Render queue to populate.</param>
        public void PopulateRenderQueue(IRenderQueue3D renderQueue) {
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }

            VisitOwnedDrawables(renderQueue.Add);
        }

        /// <summary>
        /// Captures the owned gizmo drawables into a stable ordered snapshot.
        /// </summary>
        /// <returns>Owned gizmo drawables for the viewport.</returns>
        public IReadOnlyList<IDrawable3D> CaptureOwnedDrawables() {
            List<IDrawable3D> drawables = new List<IDrawable3D>();
            VisitOwnedDrawables(drawables.Add);
            return drawables;
        }

        /// <summary>
        /// Visits every owned gizmo drawable in deterministic order.
        /// </summary>
        /// <param name="visitor">Callback that receives each owned drawable.</param>
        void VisitOwnedDrawables(Action<IDrawable3D> visitor) {
            if (visitor == null) {
                throw new ArgumentNullException(nameof(visitor));
            }

            AppendEntitySubtreeDrawables(TranslationGizmoRoot, visitor);
            AppendEntitySubtreeDrawables(RotationGizmoRoot, visitor);
            AppendEntitySubtreeDrawables(ScaleGizmoRoot, visitor);

            IReadOnlyList<EditorEntity> additionalEntities = ResolveAdditionalOwnedEntities();
            for (int index = 0; index < additionalEntities.Count; index++) {
                AppendEntitySubtreeDrawables(additionalEntities[index], visitor);
            }
        }

        /// <summary>
        /// Visits every drawable component in one entity subtree when the subtree is currently enabled.
        /// </summary>
        /// <param name="entity">Subtree root to inspect.</param>
        /// <param name="visitor">Callback that receives each drawable.</param>
        void AppendEntitySubtreeDrawables(Entity entity, Action<IDrawable3D> visitor) {
            if (entity == null || !entity.IsHierarchyEnabled) {
                return;
            }

            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is IDrawable3D drawable) {
                        visitor(drawable);
                    }
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                AppendEntitySubtreeDrawables(entity.Children[childIndex], visitor);
            }
        }
    }
}
