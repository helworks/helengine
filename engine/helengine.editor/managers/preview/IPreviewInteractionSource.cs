namespace helengine.editor {
    /// <summary>
    /// Represents a preview source that accepts pointer-driven interaction from the preview panel.
    /// </summary>
    public interface IPreviewInteractionSource {
        /// <summary>
        /// Handles one left-button drag delta.
        /// </summary>
        /// <param name="delta">Mouse delta accumulated since the previous frame.</param>
        void HandleMouseDrag(int2 delta);

        /// <summary>
        /// Handles one middle-button drag delta.
        /// </summary>
        /// <param name="delta">Mouse delta accumulated since the previous frame.</param>
        void HandleMouseMiddleDrag(int2 delta);

        /// <summary>
        /// Handles one mouse-wheel delta.
        /// </summary>
        /// <param name="wheelDelta">Raw mouse-wheel delta from the input backend.</param>
        void HandleMouseWheel(int wheelDelta);
    }
}
