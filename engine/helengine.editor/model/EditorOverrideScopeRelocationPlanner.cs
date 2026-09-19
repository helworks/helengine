namespace helengine {
    /// <summary>
    /// Plans and applies the relocation of authored override paths when an entity's level order changes.
    /// </summary>
    public static class EditorOverrideScopeRelocationPlanner {
        /// <summary>
        /// Maps each authored path onto the new order: steps whose kind survives keep their id and are re-sequenced by
        /// the new order; a path whose kind was removed, or whose result is not a valid prefix walk, is dropped.
        /// </summary>
        public static EditorOverrideScopeRelocationPlan Plan(IReadOnlyList<SceneOverrideScopeStepKind> newOrder, IEnumerable<EditorOverrideScope> authored) {
            if (authored == null) {
                throw new ArgumentNullException(nameof(authored));
            }

            EditorOverrideLevelOrder.Validate(newOrder);
            EditorOverrideScopeRelocationPlan plan = new EditorOverrideScopeRelocationPlan { NewOrder = newOrder };
            HashSet<EditorOverrideScope> seen = new HashSet<EditorOverrideScope>();
            foreach (EditorOverrideScope scope in authored) {
                if (scope.IsCommon || !seen.Add(scope)) {
                    continue;
                }
                if (!TryRelocate(newOrder, scope, out EditorOverrideScope target)) {
                    plan.Dropped.Add(scope);
                    continue;
                }
                if (target != scope) {
                    plan.Relocated.Add(new EditorOverrideScopeRelocation { Source = scope, Target = target });
                }
            }

            return plan;
        }

        /// <summary>
        /// Computes the equivalent of one path under a new order.
        /// </summary>
        public static bool TryRelocate(IReadOnlyList<SceneOverrideScopeStepKind> newOrder, EditorOverrideScope scope, out EditorOverrideScope target) {
            if (newOrder == null) {
                throw new ArgumentNullException(nameof(newOrder));
            }

            List<EditorOverrideScopeStep> steps = new List<EditorOverrideScopeStep>(scope.Depth);
            for (int level = 0; level < newOrder.Count; level++) {
                for (int index = 0; index < scope.Depth; index++) {
                    if (scope.Steps[index].Kind == newOrder[level]) {
                        steps.Add(scope.Steps[index]);
                    }
                }
            }

            if (steps.Count != scope.Depth) {
                target = EditorOverrideScope.Common;
                return false;
            }

            target = new EditorOverrideScope(steps);
            return EditorOverrideLevelOrder.IsValidPath(newOrder, target);
        }

        /// <summary>
        /// Moves existence, transform and component-existence entries on the entity, deletes dropped ones, and records the new order.
        /// Component property overrides stored per component are moved by the same rule.
        /// </summary>
        public static void Apply(EntitySaveComponent saveComponent, EditorOverrideScopeRelocationPlan plan) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (plan == null) {
                throw new ArgumentNullException(nameof(plan));
            }

            for (int index = 0; index < plan.Dropped.Count; index++) {
                EditorOverrideScope dropped = plan.Dropped[index];
                saveComponent.RemoveExistencePlatformOverride(dropped);
                saveComponent.RemoveTransformPlatformOverride(dropped);
                saveComponent.RemoveComponentPlatformOverride(dropped);
                foreach (EntityComponentSaveState componentState in saveComponent.EnumerateComponentStates()) {
                    componentState.RemoveScopedPlatformOverride(dropped);
                }
            }

            for (int index = 0; index < plan.Relocated.Count; index++) {
                EditorOverrideScope source = plan.Relocated[index].Source;
                EditorOverrideScope target = plan.Relocated[index].Target;
                if (saveComponent.TryGetExistencePlatformOverride(source, out SceneEntityPlatformExistenceOverrideAsset existence)) {
                    saveComponent.RemoveExistencePlatformOverride(source);
                    saveComponent.SetExistencePlatformOverride(target, existence);
                }
                if (saveComponent.TryGetTransformPlatformOverride(source, out SceneEntityPlatformTransformOverrideAsset transform)) {
                    saveComponent.RemoveTransformPlatformOverride(source);
                    saveComponent.SetTransformPlatformOverride(target, transform);
                }
                if (saveComponent.TryGetComponentPlatformOverride(source, out EntityPlatformComponentOverrideState componentOverride)) {
                    saveComponent.RemoveComponentPlatformOverride(source);
                    componentOverride.Scope = target;
                    saveComponent.SetComponentPlatformOverride(target, componentOverride);
                }
                foreach (EntityComponentSaveState componentState in saveComponent.EnumerateComponentStates()) {
                    if (componentState.TryGetScopedPlatformOverride(source, out EntityComponentPlatformOverrideState propertyOverride)) {
                        componentState.RemoveScopedPlatformOverride(source);
                        componentState.SetScopedPlatformOverride(target, propertyOverride);
                    }
                }
            }

            saveComponent.OverrideLevelOrder = plan.NewOrder;
        }
    }
}
