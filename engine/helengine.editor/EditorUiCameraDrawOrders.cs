namespace helengine.editor {
    /// <summary>
    /// Defines the shared camera draw-order tiers used by editor UI rendering.
    /// </summary>
    public static class EditorUiCameraDrawOrders {
        /// <summary>
        /// Draw order used by panel-owned secondary UI content cameras.
        /// </summary>
        public const byte PanelContent = 253;

        /// <summary>
        /// Draw order used by the shared editor UI camera that renders modal dialogs.
        /// </summary>
        public const byte ModalUi = 254;

        /// <summary>
        /// Draw order used by clipped modal sub-viewports that must render above the dialog shell.
        /// </summary>
        public const byte ModalContent = 255;
    }
}
