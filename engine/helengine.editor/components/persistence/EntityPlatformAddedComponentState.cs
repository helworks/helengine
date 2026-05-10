namespace helengine {
    /// <summary>
    /// Stores one detached platform-only component together with the editor metadata required to save and reload it.
    /// </summary>
    public class EntityPlatformAddedComponentState {
        /// <summary>
        /// Gets or sets the stable component key assigned to the detached platform-only component.
        /// </summary>
        public string ComponentKey { get; set; }

        /// <summary>
        /// Gets or sets the detached component instance authored only for one platform.
        /// </summary>
        public Component Component { get; set; }

        /// <summary>
        /// Gets or sets the editor-time save metadata associated with the detached platform-only component.
        /// </summary>
        public EntityComponentSaveState SaveState { get; set; }
    }
}
