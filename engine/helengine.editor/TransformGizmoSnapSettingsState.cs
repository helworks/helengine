namespace helengine.editor {
    /// <summary>
    /// Stores one camera-scoped set of transform-gizmo snap values for each tool mode and slot.
    /// </summary>
    public sealed class TransformGizmoSnapSettingsState {
        /// <summary>
        /// Initializes one snap-settings state bundle from explicit per-tool defaults.
        /// </summary>
        /// <param name="translateSnap1">Translation snap value used by the first slot.</param>
        /// <param name="translateSnap2">Translation snap value used by the second slot.</param>
        /// <param name="rotateSnap1">Rotation snap value used by the first slot.</param>
        /// <param name="rotateSnap2">Rotation snap value used by the second slot.</param>
        /// <param name="scaleSnap1">Scale snap value used by the first slot.</param>
        /// <param name="scaleSnap2">Scale snap value used by the second slot.</param>
        public TransformGizmoSnapSettingsState(
            double translateSnap1,
            double translateSnap2,
            double rotateSnap1,
            double rotateSnap2,
            double scaleSnap1,
            double scaleSnap2) {
            Snap1ValuesByToolMode = new Dictionary<EditorViewportToolMode, double> {
                [EditorViewportToolMode.Translate] = translateSnap1,
                [EditorViewportToolMode.Rotate] = rotateSnap1,
                [EditorViewportToolMode.Scale] = scaleSnap1
            };
            Snap2ValuesByToolMode = new Dictionary<EditorViewportToolMode, double> {
                [EditorViewportToolMode.Translate] = translateSnap2,
                [EditorViewportToolMode.Rotate] = rotateSnap2,
                [EditorViewportToolMode.Scale] = scaleSnap2
            };
        }

        /// <summary>
        /// Gets the first snap-slot value stored per tool mode.
        /// </summary>
        public Dictionary<EditorViewportToolMode, double> Snap1ValuesByToolMode { get; }
        /// <summary>
        /// Gets the second snap-slot value stored per tool mode.
        /// </summary>
        public Dictionary<EditorViewportToolMode, double> Snap2ValuesByToolMode { get; }
    }
}
