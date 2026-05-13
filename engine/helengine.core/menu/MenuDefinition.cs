namespace helengine {
    /// <summary>
    /// Captures the complete menu host configuration including panels, theme colors, and fonts.
    /// </summary>
    public class MenuDefinition {
        /// <summary>
        /// Initializes one menu definition.
        /// </summary>
        /// <param name="title">Optional large display title rendered near the top of the menu.</param>
        /// <param name="subtitle">Optional supporting subtitle rendered under the title.</param>
        /// <param name="initialPanelId">Panel id that should become active when the menu host starts.</param>
        /// <param name="titleFontPath">Project-relative path to the title font asset.</param>
        /// <param name="bodyFontPath">Project-relative path to the body font asset.</param>
        /// <param name="backgroundColor">Primary background color used by the menu canvas.</param>
        /// <param name="surfaceColor">Surface color used by panel chrome.</param>
        /// <param name="surfaceBorderColor">Border color used by panel chrome.</param>
        /// <param name="accentColor">Primary accent color used by focused and hovered elements.</param>
        /// <param name="accentSecondaryColor">Secondary accent color used by decorative accents.</param>
        /// <param name="textColor">Primary text color used for headings and labels.</param>
        /// <param name="mutedTextColor">Secondary text color used for subtitles and descriptions.</param>
        /// <param name="panels">Panels available to the menu host.</param>
        /// <param name="overlayImage">Optional decorative overlay image baked into the menu canvas.</param>
        /// <param name="platformInfoOverlay">Optional top-right platform information overlay baked into the menu canvas.</param>
        public MenuDefinition(
            string title,
            string subtitle,
            string initialPanelId,
            string titleFontPath,
            string bodyFontPath,
            byte4 backgroundColor,
            byte4 surfaceColor,
            byte4 surfaceBorderColor,
            byte4 accentColor,
            byte4 accentSecondaryColor,
            byte4 textColor,
            byte4 mutedTextColor,
            MenuPanelDefinition[] panels,
            MenuOverlayImageDefinition overlayImage = null,
            MenuPlatformInfoDefinition platformInfoOverlay = null) {
            if (title == null) {
                throw new ArgumentNullException(nameof(title));
            }
            if (subtitle == null) {
                throw new ArgumentNullException(nameof(subtitle));
            }
            if (string.IsNullOrWhiteSpace(initialPanelId)) {
                throw new ArgumentException("Initial panel id must be provided.", nameof(initialPanelId));
            }
            if (string.IsNullOrWhiteSpace(titleFontPath)) {
                throw new ArgumentException("Title font path must be provided.", nameof(titleFontPath));
            }
            if (string.IsNullOrWhiteSpace(bodyFontPath)) {
                throw new ArgumentException("Body font path must be provided.", nameof(bodyFontPath));
            }
            if (panels == null) {
                throw new ArgumentNullException(nameof(panels));
            }
            if (panels.Length == 0) {
                throw new InvalidOperationException("Menu definitions must contain at least one panel.");
            }

            Title = title;
            Subtitle = subtitle;
            InitialPanelId = initialPanelId;
            TitleFontPath = titleFontPath;
            BodyFontPath = bodyFontPath;
            BackgroundColor = backgroundColor;
            SurfaceColor = surfaceColor;
            SurfaceBorderColor = surfaceBorderColor;
            AccentColor = accentColor;
            AccentSecondaryColor = accentSecondaryColor;
            TextColor = textColor;
            MutedTextColor = mutedTextColor;
            Panels = panels;
            OverlayImage = overlayImage;
            PlatformInfoOverlay = platformInfoOverlay;
        }

        /// <summary>
        /// Gets the optional large display title rendered near the top of the menu.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the optional supporting subtitle rendered under the title.
        /// </summary>
        public string Subtitle { get; }

        /// <summary>
        /// Gets the panel id activated when the menu host starts.
        /// </summary>
        public string InitialPanelId { get; }

        /// <summary>
        /// Gets the project-relative path to the title font asset.
        /// </summary>
        public string TitleFontPath { get; }

        /// <summary>
        /// Gets the project-relative path to the body font asset.
        /// </summary>
        public string BodyFontPath { get; }

        /// <summary>
        /// Gets the primary background color used by the menu canvas.
        /// </summary>
        public byte4 BackgroundColor { get; }

        /// <summary>
        /// Gets the surface color used by panel chrome.
        /// </summary>
        public byte4 SurfaceColor { get; }

        /// <summary>
        /// Gets the border color used by panel chrome.
        /// </summary>
        public byte4 SurfaceBorderColor { get; }

        /// <summary>
        /// Gets the primary accent color used by focused and hovered elements.
        /// </summary>
        public byte4 AccentColor { get; }

        /// <summary>
        /// Gets the secondary accent color used by decorative accents.
        /// </summary>
        public byte4 AccentSecondaryColor { get; }

        /// <summary>
        /// Gets the primary text color used for headings and labels.
        /// </summary>
        public byte4 TextColor { get; }

        /// <summary>
        /// Gets the secondary text color used for supporting copy.
        /// </summary>
        public byte4 MutedTextColor { get; }

        /// <summary>
        /// Gets the panels available to the menu host.
        /// </summary>
        public MenuPanelDefinition[] Panels { get; }

        /// <summary>
        /// Gets the optional decorative overlay image baked into the menu canvas.
        /// </summary>
        public MenuOverlayImageDefinition OverlayImage { get; }

        /// <summary>
        /// Gets the optional top-right platform information overlay baked into the menu canvas.
        /// </summary>
        public MenuPlatformInfoDefinition PlatformInfoOverlay { get; }
    }
}
