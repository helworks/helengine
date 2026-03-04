namespace helengine.editor {
    /// <summary>
    /// Tracks the currently hovered transform gizmo handle entity.
    /// </summary>
    public static class EditorGizmoHoverService {
        /// <summary>
        /// Backing field for hovered handle storage.
        /// </summary>
        static Entity HoveredHandleEntityValue;

        /// <summary>
        /// Gets the currently hovered gizmo handle entity.
        /// </summary>
        public static Entity HoveredHandleEntity => HoveredHandleEntityValue;

        /// <summary>
        /// Gets the currently hovered gizmo axis entity.
        /// </summary>
        public static Entity HoveredAxisEntity => HoveredHandleEntityValue;

        /// <summary>
        /// Sets the currently hovered gizmo handle entity.
        /// </summary>
        /// <param name="handleEntity">Handle entity to set, or null to clear hover.</param>
        public static void SetHoveredHandle(Entity handleEntity) {
            HoveredHandleEntityValue = handleEntity;
        }

        /// <summary>
        /// Sets the currently hovered gizmo axis entity.
        /// </summary>
        /// <param name="axisEntity">Axis entity to set, or null to clear hover.</param>
        public static void SetHoveredAxis(Entity axisEntity) {
            SetHoveredHandle(axisEntity);
        }

        /// <summary>
        /// Clears the current hovered gizmo handle.
        /// </summary>
        public static void ClearHoveredHandle() {
            HoveredHandleEntityValue = null;
        }

        /// <summary>
        /// Clears the current hovered gizmo axis.
        /// </summary>
        public static void ClearHoveredAxis() {
            ClearHoveredHandle();
        }
    }
}
