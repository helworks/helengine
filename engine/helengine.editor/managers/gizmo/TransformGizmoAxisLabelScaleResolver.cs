namespace helengine.editor {
    /// <summary>
    /// Resolves the world scale used by transform-gizmo axis labels so drag-time billboards match the frozen gizmo size.
    /// </summary>
    public static class TransformGizmoAxisLabelScaleResolver {
        /// <summary>
        /// Chooses the label scale for the current frame from the live computed size and the frozen drag-time gizmo size.
        /// </summary>
        /// <param name="isDragging">True when the viewport camera is actively dragging the transform gizmo.</param>
        /// <param name="computedScale">Distance-based gizmo scale that would be used when not dragging.</param>
        /// <param name="frozenScale">Current gizmo scale held by the translation gizmo while dragging.</param>
        /// <returns>Scale value that keeps labels aligned with the translation gizmo.</returns>
        public static double Resolve(bool isDragging, double computedScale, double frozenScale) {
            if (computedScale <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(computedScale), "Computed gizmo scale must be greater than zero.");
            }

            if (isDragging && frozenScale > 0.0) {
                return frozenScale;
            }

            return computedScale;
        }
    }
}
