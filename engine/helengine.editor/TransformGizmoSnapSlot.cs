namespace helengine.editor {
    /// <summary>
    /// Identifies one of the configurable transform-gizmo snap slots.
    /// </summary>
    public enum TransformGizmoSnapSlot {
        /// <summary>
        /// No snap slot is active.
        /// </summary>
        None = 0,

        /// <summary>
        /// First snap slot, activated by the control modifier.
        /// </summary>
        Snap1 = 1,

        /// <summary>
        /// Second snap slot, activated by the shift modifier.
        /// </summary>
        Snap2 = 2
    }
}
