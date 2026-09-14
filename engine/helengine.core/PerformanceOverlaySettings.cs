namespace helengine {
    /// <summary>
    /// Defines the runtime presentation policy used by the performance overlay without coupling overlay components to a platform identity.
    /// </summary>
    public sealed class PerformanceOverlaySettings {
        /// <summary>
        /// Gets the generic performance overlay policy used when a host does not provide platform-specific presentation settings.
        /// </summary>
        public static PerformanceOverlaySettings Default { get; } = new PerformanceOverlaySettings(1f, true);

        /// <summary>
        /// Initializes immutable runtime presentation settings for the performance overlay.
        /// </summary>
        /// <param name="fontScaleMultiplier">Multiplier applied to the authored overlay font scale.</param>
        /// <param name="textShadowEnabled">Whether overlay text rows render their shadow glyphs.</param>
        public PerformanceOverlaySettings(float fontScaleMultiplier, bool textShadowEnabled) {
            if (float.IsNaN(fontScaleMultiplier)
                || float.IsInfinity(fontScaleMultiplier)
                || fontScaleMultiplier <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(fontScaleMultiplier), "Font scale multiplier must be finite and greater than zero.");
            }

            FontScaleMultiplier = fontScaleMultiplier;
            TextShadowEnabled = textShadowEnabled;
        }

        /// <summary>
        /// Gets the multiplier applied to the FPS component's authored font scale at runtime.
        /// </summary>
        public float FontScaleMultiplier { get; }

        /// <summary>
        /// Gets whether FPS overlay text rows render a shadow glyph pass.
        /// </summary>
        public bool TextShadowEnabled { get; }
    }
}