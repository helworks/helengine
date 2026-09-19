namespace helengine {
    /// <summary>
    /// Rules for an entity's override level order: which kinds may follow Common and in what sequence a path may visit them.
    /// </summary>
    public static class EditorOverrideLevelOrder {
        /// <summary>
        /// Order used when neither the entity nor the project settings state one: Platform then Build Config.
        /// </summary>
        public static readonly IReadOnlyList<SceneOverrideScopeStepKind> Default = new[] {
            SceneOverrideScopeStepKind.Platform,
            SceneOverrideScopeStepKind.BuildConfig
        };

        /// <summary>
        /// Rejects an order that lists one kind twice. The empty order (Common only) is valid.
        /// </summary>
        public static void Validate(IReadOnlyList<SceneOverrideScopeStepKind> order) {
            if (!TryValidate(order, out string error)) {
                throw new InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Runs the same check as <see cref="Validate"/> and reports the duplicated kind instead of throwing.
        /// </summary>
        /// <param name="order">Override level order to check.</param>
        /// <param name="error">Message naming the duplicated kind when the order is invalid; empty otherwise.</param>
        /// <returns>True when the order lists each kind at most once; otherwise false.</returns>
        public static bool TryValidate(IReadOnlyList<SceneOverrideScopeStepKind> order, out string error) {
            if (order == null) {
                throw new ArgumentNullException(nameof(order));
            }

            for (int outer = 0; outer < order.Count; outer++) {
                for (int inner = outer + 1; inner < order.Count; inner++) {
                    if (order[outer] == order[inner]) {
                        error = $"Override level order lists '{SceneOverrideScopePath.FormatKind(order[outer])}' more than once.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns whether a path is a prefix walk of the order. A Group level may hold zero or more consecutive group
        /// steps (an ungrouped platform contributes none); every other level holds exactly one step.
        /// </summary>
        public static bool IsValidPath(IReadOnlyList<SceneOverrideScopeStepKind> order, EditorOverrideScope scope) {
            if (order == null) {
                throw new ArgumentNullException(nameof(order));
            }

            int level = 0;
            for (int index = 0; index < scope.Depth; index++) {
                SceneOverrideScopeStepKind kind = scope.Steps[index].Kind;
                while (level < order.Count && order[level] == SceneOverrideScopeStepKind.Group && kind != SceneOverrideScopeStepKind.Group) {
                    level++;
                }
                if (level >= order.Count || order[level] != kind) {
                    return false;
                }
                if (kind != SceneOverrideScopeStepKind.Group) {
                    level++;
                }
            }

            return true;
        }

        /// <summary>
        /// Formats an order as <c>Common → Platform → Build Config</c>.
        /// </summary>
        public static string Describe(IReadOnlyList<SceneOverrideScopeStepKind> order) {
            System.Text.StringBuilder builder = new System.Text.StringBuilder("Common");
            for (int index = 0; index < (order?.Count ?? 0); index++) {
                builder.Append(" → ");
                builder.Append(order[index] == SceneOverrideScopeStepKind.BuildConfig ? "Build Config" : order[index].ToString());
            }

            return builder.ToString();
        }
    }
}
