namespace helengine.editor {
    /// <summary>
    /// Identifies the pending scene transition that should continue after the unsaved-changes guard resolves.
    /// The scene-lifecycle service records one of these while the guard is open and the editor session
    /// replays it once the user has saved, discarded, or cancelled the pending changes.
    /// </summary>
    public enum SceneTransitionKind {
        /// <summary>
        /// No transition is pending.
        /// </summary>
        None,

        /// <summary>
        /// The session should reset to a new empty scene.
        /// </summary>
        NewMap,

        /// <summary>
        /// The session should open one scene file chosen by the user.
        /// </summary>
        OpenMap,

        /// <summary>
        /// The editor host should close after the unsaved-changes guard resolves.
        /// </summary>
        Exit
    }
}
