namespace helengine {
    /// <summary>
    /// Stores the fixed layout constants and generated entity names used by the baked demo-disc menu.
    /// </summary>
    public static class DemoMenuLayout {
        /// <summary>
        /// Name assigned to the generated menu subtree entity.
        /// </summary>
        public const string GeneratedRootEntityName = "DemoDiscGeneratedMenu";

        /// <summary>
        /// Width of the baked demo-disc menu canvas.
        /// </summary>
        public const int CanvasWidth = 1280;

        /// <summary>
        /// Height of the baked demo-disc menu canvas.
        /// </summary>
        public const int CanvasHeight = 720;

        /// <summary>
        /// Width of the main menu panel surface.
        /// </summary>
        public const int PanelWidth = 560;

        /// <summary>
        /// Height of the main menu panel surface.
        /// </summary>
        public const int PanelHeight = 420;

        /// <summary>
        /// Width of one baked menu item row.
        /// </summary>
        public const int ButtonWidth = 420;

        /// <summary>
        /// Height of one baked menu item row.
        /// </summary>
        public const int ButtonHeight = 48;

        /// <summary>
        /// Vertical spacing inserted between adjacent item rows.
        /// </summary>
        public const int ButtonSpacing = 14;
    }
}
