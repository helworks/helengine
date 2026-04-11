namespace helengine {
    /// <summary>
    /// Defines the explicit front-to-back render-order bands used by 2D engine and editor UI.
    /// </summary>
    public static class RenderOrder2D {
        /// <summary>
        /// Base order for docked panel content backgrounds.
        /// </summary>
        public const byte PanelBackground = 16;

        /// <summary>
        /// Base order for raised panel surfaces such as title bars and toolbar backgrounds.
        /// </summary>
        public const byte PanelSurface = 32;

        /// <summary>
        /// Base order for panel text, outlines, and foreground visuals.
        /// </summary>
        public const byte PanelForeground = 48;

        /// <summary>
        /// Base order for the highest non-overlay visuals that still belong to panel chrome.
        /// </summary>
        public const byte PanelInteractive = 64;

        /// <summary>
        /// Extra order applied to floating dockables so they rise above docked panels.
        /// </summary>
        public const byte FloatingPanelBias = 32;

        /// <summary>
        /// Base order for non-modal overlays such as context menus and viewport widgets.
        /// </summary>
        public const byte OverlayBackground = 160;

        /// <summary>
        /// Foreground order for non-modal overlays.
        /// </summary>
        public const byte OverlayForeground = 176;

        /// <summary>
        /// Highest non-modal order used by transparent input shields and blockers.
        /// </summary>
        public const byte OverlayInput = 192;

        /// <summary>
        /// Base order for modal panel surfaces.
        /// </summary>
        public const byte ModalBackground = 224;

        /// <summary>
        /// Foreground order for modal labels and buttons.
        /// </summary>
        public const byte ModalForeground = 240;

        /// <summary>
        /// Highest modal order used by modal-specific input surfaces.
        /// </summary>
        public const byte ModalInput = 248;
    }
}
