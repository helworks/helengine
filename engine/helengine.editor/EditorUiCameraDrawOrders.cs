namespace helengine.editor {
    /// <summary>
    /// Defines the shared camera draw-order tiers used by editor UI rendering.
    /// </summary>
    public static class EditorUiCameraDrawOrders {
        /// <summary>
        /// Draw order used by panel-owned secondary UI content cameras.
        /// </summary>
        public const byte PanelContent = 254;

        /// <summary>
        /// Draw order used by the shared editor UI camera that renders modal dialogs.
        /// </summary>
        public const byte ModalUi = 255;
    }
}
