namespace helengine.editor {
    /// <summary>
    /// Exposes the authored, unscaled layout of every viewport-scaled subtree while editor serialization reads live entities.
    /// In the editor a <see cref="ViewportComponent"/> stretches its subtree to the editor window by writing scaled values into
    /// the entities and components; serializing those values would persist the editor window size as authored data and compound
    /// on every save. Callers run <see cref="Restore"/> before serializing and <see cref="Reapply"/> in a finally block afterwards,
    /// so the scaled preview always returns even when serialization fails.
    /// </summary>
    public sealed class SceneViewportAuthoredLayoutScope {
        /// <summary>
        /// Viewports whose scaled subtrees overlap the serialized entities, in discovery order.
        /// </summary>
        readonly List<ViewportComponent> Viewports;

        /// <summary>
        /// Number of leading viewports whose authored layout is currently restored and must be reapplied.
        /// </summary>
        int RestoredCount;

        /// <summary>
        /// Initializes an empty scope; use the factory methods to collect viewports.
        /// </summary>
        SceneViewportAuthoredLayoutScope() {
            Viewports = new List<ViewportComponent>();
        }

        /// <summary>
        /// Collects every viewport inside the supplied serialized root entities.
        /// </summary>
        /// <param name="roots">Root entities that will be serialized.</param>
        /// <returns>A scope covering every viewport found below the roots.</returns>
        public static SceneViewportAuthoredLayoutScope ForRoots(IReadOnlyList<EditorEntity> roots) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            SceneViewportAuthoredLayoutScope scope = new SceneViewportAuthoredLayoutScope();
            for (int index = 0; index < roots.Count; index++) {
                scope.CollectSubtree(roots[index]);
            }

            return scope;
        }

        /// <summary>
        /// Collects the viewports that can scale one serialized entity: those on its ancestors and those inside its own subtree.
        /// </summary>
        /// <param name="entity">Entity that will be serialized.</param>
        /// <returns>A scope covering every viewport that affects the entity's serialized values.</returns>
        public static SceneViewportAuthoredLayoutScope ForEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            SceneViewportAuthoredLayoutScope scope = new SceneViewportAuthoredLayoutScope();
            Entity ancestor = entity.Parent;
            while (ancestor != null) {
                scope.CollectComponents(ancestor);
                ancestor = ancestor.Parent;
            }

            scope.CollectSubtree(entity);
            return scope;
        }

        /// <summary>
        /// Writes the authored layout back onto every collected viewport subtree, absorbing edits made in the scaled preview.
        /// </summary>
        public void Restore() {
            for (int index = RestoredCount; index < Viewports.Count; index++) {
                Viewports[index].RestoreAuthoredLayout();
                RestoredCount = index + 1;
            }
        }

        /// <summary>
        /// Re-applies the viewport scale to every subtree restored by <see cref="Restore"/> so the editor preview is scaled again.
        /// </summary>
        public void Reapply() {
            int restoredCount = RestoredCount;
            RestoredCount = 0;
            for (int index = 0; index < restoredCount; index++) {
                Viewports[index].ReapplyLayout();
            }
        }

        /// <summary>
        /// Collects the viewports attached to one entity and all of its descendants.
        /// </summary>
        /// <param name="entity">Subtree root to scan.</param>
        void CollectSubtree(Entity entity) {
            CollectComponents(entity);
            if (entity.Children == null) {
                return;
            }

            for (int index = 0; index < entity.Children.Count; index++) {
                CollectSubtree(entity.Children[index]);
            }
        }

        /// <summary>
        /// Collects the viewports attached directly to one entity, ignoring ones already collected.
        /// </summary>
        /// <param name="entity">Entity whose components are scanned.</param>
        void CollectComponents(Entity entity) {
            if (entity.Components == null) {
                return;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is ViewportComponent viewport && !Viewports.Contains(viewport)) {
                    Viewports.Add(viewport);
                }
            }
        }
    }
}
