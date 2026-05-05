namespace helengine.editor {
    /// <summary>
    /// Resolves scaled editor UI metrics from one effective runtime scale multiplier.
    /// </summary>
    public sealed class EditorUiMetrics {
        /// <summary>
        /// Gets the default metrics instance for unscaled editor UI layout.
        /// </summary>
        public static EditorUiMetrics Default { get; } = new EditorUiMetrics(1d);

        /// <summary>
        /// Gets the effective runtime scale multiplier applied to editor UI layout.
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// Initializes one scaled metrics source for editor UI layout.
        /// </summary>
        /// <param name="scale">Effective UI scale multiplier relative to the unscaled editor layout.</param>
        public EditorUiMetrics(double scale) {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(scale), "Editor UI scale must be a finite value greater than zero.");
            }

            Scale = scale;
        }

        /// <summary>
        /// Gets the base UI font size used by the editor host.
        /// </summary>
        public int UiFontPixelSize => ScalePixels(12);

        /// <summary>
        /// Gets the snap-modifier font size used by the transform gizmo overlay.
        /// </summary>
        public int SnapModifierFontPixelSize => ScalePixels(15);

        /// <summary>
        /// Gets the scaled title-bar height used by the editor host chrome.
        /// </summary>
        public int HostTitleBarHeight => ScalePixels(EditorTitleBar.HeightPixels);

        /// <summary>
        /// Gets the scaled icon size used by the editor host title bar.
        /// </summary>
        public int HostTitleBarIconSize => ScalePixels(EditorTitleBar.IconSizePixels);

        /// <summary>
        /// Gets the scaled icon padding used by the editor host title bar.
        /// </summary>
        public int HostTitleBarIconPadding => ScalePixels(EditorTitleBar.IconPaddingPixels);

        /// <summary>
        /// Gets the scaled dock title-bar height used by dockable editor panels.
        /// </summary>
        public int DockTitleBarHeight => ScalePixels(DockableEntity.TitleBarHeight);

        /// <summary>
        /// Scales one base pixel measurement using the current effective UI scale.
        /// </summary>
        /// <param name="basePixels">Unscaled pixel value from the editor UI design.</param>
        /// <returns>Scaled pixel value rounded upward to avoid clipping.</returns>
        public int ScalePixels(int basePixels) {
            if (basePixels <= 0) {
                throw new ArgumentOutOfRangeException(nameof(basePixels), "Base pixel values must be greater than zero.");
            }

            return Math.Max(1, (int)Math.Ceiling(basePixels * Scale));
        }
    }
}
