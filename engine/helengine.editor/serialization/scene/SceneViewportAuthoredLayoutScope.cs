namespace helengine.editor {
    /// <summary>
    /// Exposes the authored, unscaled layout of every scaled subtree while editor serialization reads live entities.
    /// In the editor, [RunInEditor] layout owners (<see cref="ViewportComponent"/> and <see cref="ReferenceCanvasFitComponent"/>)
    /// scale their subtrees to the editor window by writing scaled values into the entities and components. Serializing
    /// those values would persist the editor window size as authored data and compound on every save, so serialization runs
    /// through <see cref="Serialize{T}"/>, which restores the authored layout first and always re-applies the scale afterwards.
    /// </summary>
    public sealed class SceneViewportAuthoredLayoutScope {
        /// <summary>
        /// Layout owners whose scaled subtrees overlap the serialized entities, in discovery order.
        /// </summary>
        readonly List<IAuthoredLayoutOwner> Owners;

        /// <summary>
        /// Number of leading owners whose authored layout may currently be restored and must be reapplied.
        /// </summary>
        int RestoredCount;

        /// <summary>
        /// Initializes an empty scope; use the factory methods to collect layout owners.
        /// </summary>
        SceneViewportAuthoredLayoutScope() {
            Owners = new List<IAuthoredLayoutOwner>();
        }

        /// <summary>
        /// Collects every layout owner inside the supplied serialized root entities.
        /// </summary>
        /// <param name="roots">Root entities that will be serialized.</param>
        /// <returns>A scope covering every layout owner found below the roots.</returns>
        public static SceneViewportAuthoredLayoutScope ForRoots(IReadOnlyList<EditorEntity> roots) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            SceneViewportAuthoredLayoutScope scope = new SceneViewportAuthoredLayoutScope();
            for (int index = 0; index < roots.Count; index++) {
                scope.CollectSubtree(roots[index]);
            }

            scope.OrderInnermostFirst();
            return scope;
        }

        /// <summary>
        /// Collects the layout owners that can scale one serialized entity: those on its ancestors and those inside its own subtree.
        /// </summary>
        /// <param name="entity">Entity that will be serialized or cloned.</param>
        /// <returns>A scope covering every layout owner that affects the entity's values.</returns>
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
            scope.OrderInnermostFirst();
            return scope;
        }

        /// <summary>
        /// Runs one serialization step against the authored layout and then re-applies the scaled preview.
        /// When the step (or the restore) throws, the preview is still re-applied and the original exception propagates
        /// unchanged. If re-applying also fails, both failures surface together in one <see cref="AggregateException"/>, so
        /// neither the original cause nor the broken preview is hidden. A plain try/finally is not used because an exception
        /// thrown from a finally block would replace the original one.
        /// </summary>
        /// <typeparam name="T">Serialized result type.</typeparam>
        /// <param name="serialize">Serialization step that reads the restored authored values.</param>
        /// <returns>The serialized result.</returns>
        public T Serialize<T>(Func<T> serialize) {
            if (serialize == null) {
                throw new ArgumentNullException(nameof(serialize));
            }

            T result;
            try {
                Restore();
                result = serialize();
            } catch (Exception failure) {
                ReapplyAfterFailure(failure);
                throw;
            }

            Reapply();
            return result;
        }

        /// <summary>
        /// Writes the authored layout back onto every collected subtree, innermost owner first, absorbing edits made in the
        /// scaled preview. A nested owner writes the final values of its subtree, so it must restore them before an enclosing
        /// owner compares its subtree with its own last pass. Each owner is counted before it is restored, so an owner that
        /// throws partway is still re-applied.
        /// </summary>
        void Restore() {
            for (int index = RestoredCount; index < Owners.Count; index++) {
                RestoredCount = index + 1;
                Owners[index].RestoreAuthoredLayout();
            }
        }

        /// <summary>
        /// Re-applies the scale to every subtree restored by <see cref="Restore"/>, outermost owner first, so nested owners
        /// write the final preview values of their subtrees last, exactly as the editor frame update does.
        /// </summary>
        void Reapply() {
            int restoredCount = RestoredCount;
            RestoredCount = 0;
            for (int index = restoredCount - 1; index >= 0; index--) {
                Owners[index].ReapplyLayout();
            }
        }

        /// <summary>
        /// Orders the collected owners by the depth of their entities, deepest first, so restore runs innermost-first and
        /// re-apply (which walks the list backwards) runs outermost-first. Owners at the same depth own disjoint subtrees.
        /// </summary>
        void OrderInnermostFirst() {
            List<IAuthoredLayoutOwner> ordered = new List<IAuthoredLayoutOwner>();
            List<int> depths = new List<int>();
            for (int index = 0; index < Owners.Count; index++) {
                int depth = ResolveDepth(((Component)Owners[index]).Parent);
                int insertAt = 0;
                while (insertAt < depths.Count && depths[insertAt] >= depth) {
                    insertAt++;
                }

                ordered.Insert(insertAt, Owners[index]);
                depths.Insert(insertAt, depth);
            }

            Owners.Clear();
            Owners.AddRange(ordered);
        }

        /// <summary>
        /// Counts the ancestors of one entity.
        /// </summary>
        /// <param name="entity">Entity whose hierarchy depth is resolved.</param>
        /// <returns>Zero for a root entity, otherwise the number of ancestors.</returns>
        static int ResolveDepth(Entity entity) {
            int depth = 0;
            Entity current = entity == null ? null : entity.Parent;
            while (current != null) {
                depth++;
                current = current.Parent;
            }

            return depth;
        }

        /// <summary>
        /// Re-applies the preview after a failed serialization step without losing the original failure.
        /// </summary>
        /// <param name="failure">Exception thrown by the restore or serialization step.</param>
        void ReapplyAfterFailure(Exception failure) {
            try {
                Reapply();
            } catch (Exception reapplyFailure) {
                throw new AggregateException("Scene serialization failed and the scaled layout preview could not be re-applied.", failure, reapplyFailure);
            }
        }

        /// <summary>
        /// Collects the layout owners attached to one entity and all of its descendants.
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
        /// Collects the layout owners attached directly to one entity, ignoring ones already collected.
        /// </summary>
        /// <param name="entity">Entity whose components are scanned.</param>
        void CollectComponents(Entity entity) {
            if (entity.Components == null) {
                return;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is IAuthoredLayoutOwner owner && !Owners.Contains(owner)) {
                    Owners.Add(owner);
                }
            }
        }
    }
}
