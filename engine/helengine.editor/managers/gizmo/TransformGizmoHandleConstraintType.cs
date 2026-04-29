namespace helengine.editor {
    /// <summary>
    /// Describes how a transform gizmo handle constrains movement.
    /// </summary>
    public enum TransformGizmoHandleConstraintType {
        /// <summary>
        /// Constrains translation to a single axis direction.
        /// </summary>
        Axis = 0,
        /// <summary>
        /// Constrains translation to a plane defined by two basis directions.
        /// </summary>
        Plane = 1
    }
}
