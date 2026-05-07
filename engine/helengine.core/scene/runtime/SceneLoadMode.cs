namespace helengine {
    /// <summary>
    /// Defines the runtime behavior used when one built scene is loaded.
    /// </summary>
    public enum SceneLoadMode {
        /// <summary>
        /// Unloads every currently tracked scene before the requested scene is loaded.
        /// </summary>
        Single,

        /// <summary>
        /// Preserves currently tracked scenes and appends the requested scene.
        /// </summary>
        Additive
    }
}
