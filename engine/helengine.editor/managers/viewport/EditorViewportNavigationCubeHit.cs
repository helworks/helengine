namespace helengine.editor {
    /// <summary>
    /// Describes one resolved, camera-visible navigation-cube face, edge, or corner hit.
    /// </summary>
    public sealed class EditorViewportNavigationCubeHit {
        /// <summary>
        /// Initializes the selected direction and its depth in the visible cube surface.
        /// </summary>
        /// <param name="target">Signed cube direction resolved by the hit tester.</param>
        /// <param name="visibleFaceDepth">Camera-space depth of the visible surface region.</param>
        public EditorViewportNavigationCubeHit(EditorViewportNavigationTarget target, float visibleFaceDepth) {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            VisibleFaceDepth = visibleFaceDepth;
        }

        /// <summary>
        /// Gets the signed direction selected by this hit.
        /// </summary>
        public EditorViewportNavigationTarget Target { get; }

        /// <summary>
        /// Gets the camera-space depth used when resolving overlapping visible surfaces.
        /// </summary>
        public float VisibleFaceDepth { get; }
    }
}
