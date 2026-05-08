namespace helengine.editor {
    /// <summary>
    /// Extends a preview source with direct pointer interaction hooks used by the preview panel.
    /// </summary>
    public interface IPreviewInteractionSource {
        /// <summary>
        /// Handles one mouse-wheel delta delivered while the pointer is over the preview content.
        /// </summary>
        /// <param name="wheelDelta">Raw mouse-wheel delta from the input backend.</param>
        void HandleMouseWheel(int wheelDelta);

        /// <summary>
        /// Handles one left-button drag delta delivered while the pointer is over the preview content.
        /// </summary>
        /// <param name="delta">Mouse delta accumulated since the previous frame.</param>
        void HandleMouseDrag(int2 delta);
    }
}
