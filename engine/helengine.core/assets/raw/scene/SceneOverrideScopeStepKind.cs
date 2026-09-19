namespace helengine {
    /// <summary>
    /// Kind of one step on an override scope path. Every kind appears at most once as a level in an entity's
    /// level order; the Group level is a chain of nested group steps.
    /// </summary>
    public enum SceneOverrideScopeStepKind : byte {
        /// <summary>One platform group from <c>settings/platform-groups.json</c>.</summary>
        Group = 1,
        /// <summary>One project platform id.</summary>
        Platform = 2,
        /// <summary>One project environment id (debug, release, ...).</summary>
        BuildConfig = 3
    }
}
