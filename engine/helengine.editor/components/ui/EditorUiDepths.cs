namespace helengine.editor {
    /// <summary>
    /// Physical depth bands used by the editor's screen-space interface.
    /// </summary>
    internal static class EditorUiDepths {
        /// <summary>
        /// Depth of ordinary docked panel content.
        /// </summary>
        public const float DockedPanel = 16f;

        /// <summary>
        /// Local depth offset for split separators above docked panel tabs.
        /// </summary>
        public const float DockSplitSeparatorOffset = 0.75f;

        /// <summary>
        /// Depth of editor title-bar chrome above docked panel content.
        /// </summary>
        public const float EditorChrome = 32f;

        /// <summary>
        /// Local depth of the native title-bar resize strip above its editor chrome host.
        /// </summary>
        public const float NativeResizeInputOffset = 176f;

        /// <summary>
        /// Depth used by transient non-modal overlays such as menus.
        /// </summary>
        public const float Overlay = 160f;

        /// <summary>
        /// Depth of a floating dockable panel above docked content.
        /// </summary>
        public const float FloatingPanel = 48f;

        /// <summary>
        /// Depth of the full-screen blocker behind an open modal dialog.
        /// </summary>
        public const float ModalBackdrop = 224f;

        /// <summary>
        /// Depth of modal panels and their content.
        /// </summary>
        public const float ModalPanel = 240f;
    }
}
