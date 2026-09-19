namespace helengine {
    /// <summary>
    /// One authored path and the path it moves to under a new level order.
    /// </summary>
    public sealed class EditorOverrideScopeRelocation {
        /// <summary>Gets or sets the path as authored under the previous order.</summary>
        public EditorOverrideScope Source { get; set; }

        /// <summary>Gets or sets the equivalent path under the new order.</summary>
        public EditorOverrideScope Target { get; set; }
    }
}
