namespace helengine.editor {
    /// <summary>Orders picker readback, transform edits, and gizmo presentation independently of component attachment order.</summary>
    public static class TransformGizmoUpdateOrder {
        /// <summary>Resolves the previous GPU pass before drag controllers consume the current mouse press.</summary>
        public const byte Picking = 0;

        /// <summary>Applies transform input after the hovered handle has been resolved.</summary>
        public const byte Drag = 1;

        /// <summary>Updates position, scale, and highlighting after the selected transform changes in this frame.</summary>
        public const byte Follow = 2;
    }
}
