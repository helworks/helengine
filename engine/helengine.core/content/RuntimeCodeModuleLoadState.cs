namespace helengine {
    /// <summary>
    /// Describes when one packaged runtime code module should be considered loaded.
    /// </summary>
    public enum RuntimeCodeModuleLoadState {
        /// <summary>
        /// The module must remain resident for the entire runtime session.
        /// </summary>
        ResidentAtStartup = 0,

        /// <summary>
        /// The module may be loaded while a scene is active and released when that scene is no longer active.
        /// </summary>
        SceneResident = 1,

        /// <summary>
        /// The module is not required to stay resident after it has been used.
        /// </summary>
        Unloadable = 2
    }
}
