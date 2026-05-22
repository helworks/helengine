namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific texture processor configuration record for a source asset.
    /// </summary>
    public class TextureAssetProcessorSettings {
        /// <summary>
        /// Gets or sets the maximum allowed width or height in pixels for the processed texture, or zero when uncapped.
        /// </summary>
        public int MaxResolution { get; set; }

        /// <summary>
        /// Gets or sets the platform-published texture color-format identifier produced for this platform.
        /// </summary>
        public string ColorFormatId { get; set; } = TextureAssetColorFormat.Rgba32.ToString();

        /// <summary>
        /// Gets or sets the generic shared-engine texture color format when the active platform uses one of the built-in generic formats.
        /// </summary>
        public TextureAssetColorFormat ColorFormat {
            get {
                if (!TryResolveGenericColorFormat(ColorFormatId, out TextureAssetColorFormat colorFormat)) {
                    throw new InvalidOperationException($"Texture color format id '{ColorFormatId}' is platform-owned and cannot be processed by the shared generic texture pipeline.");
                }

                return colorFormat;
            }
            set {
                ColorFormatId = value.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the alpha precision stored by the processed texture payload.
        /// </summary>
        public TextureAssetAlphaPrecision AlphaPrecision { get; set; }

        /// <summary>
        /// Returns whether this settings record maps to one generic shared-engine texture format.
        /// </summary>
        /// <returns><c>true</c> when the color-format id is generic and can be handled by the shared texture processor.</returns>
        public bool UsesGenericColorFormat() {
            return TryResolveGenericColorFormat(ColorFormatId, out _);
        }

        /// <summary>
        /// Resolves one platform-published texture color-format identifier into a generic shared-engine texture format.
        /// </summary>
        /// <param name="colorFormatId">Platform-published texture color-format identifier.</param>
        /// <param name="colorFormat">Resolved generic texture color format when the identifier is shared-engine-owned.</param>
        /// <returns><c>true</c> when the identifier resolves to one generic shared-engine format; otherwise <c>false</c>.</returns>
        public static bool TryResolveGenericColorFormat(string colorFormatId, out TextureAssetColorFormat colorFormat) {
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Rgba32.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Rgba32;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Rgba4444.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Rgba4444;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Indexed4.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Indexed4;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Indexed8.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Indexed8;
                return true;
            }

            colorFormat = default;
            return false;
        }
    }
}
