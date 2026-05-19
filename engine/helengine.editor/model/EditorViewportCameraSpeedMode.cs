namespace helengine.editor {
    /// <summary>
    /// Defines how one editor viewport derives its effective camera navigation speeds.
    /// </summary>
    public static class EditorViewportCameraSpeedMode {
        /// <summary>
        /// Derives viewport camera speed from the currently selected entity bounds extent.
        /// </summary>
        public const byte AutoFromSelection = 0;

        /// <summary>
        /// Uses one viewport-local authored manual speed override instead of selection bounds.
        /// </summary>
        public const byte ManualOverride = 1;
    }
}
