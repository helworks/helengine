namespace helengine {
    /// <summary>
    /// Result of planning a level-order change: paths that move and paths that have no home in the new order.
    /// </summary>
    public sealed class EditorOverrideScopeRelocationPlan {
        /// <summary>Gets or sets the new level order the plan targets.</summary>
        public IReadOnlyList<SceneOverrideScopeStepKind> NewOrder { get; set; }

        /// <summary>Gets the paths that move, excluding paths that stay where they are.</summary>
        public List<EditorOverrideScopeRelocation> Relocated { get; } = new List<EditorOverrideScopeRelocation>();

        /// <summary>Gets the paths that must be deleted because a step kind was removed or the shape is invalid.</summary>
        public List<EditorOverrideScope> Dropped { get; } = new List<EditorOverrideScope>();

        /// <summary>Gets whether applying the plan deletes authored data; the UI must confirm first.</summary>
        public bool HasDrops => Dropped.Count > 0;
    }
}
