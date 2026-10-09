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
        /// Gets or sets the generic indexed-texture method identifier used when the selected color format is palette-backed.
        /// </summary>
        public string IndexingMethodId { get; set; } = string.Empty;

        /// <summary>
        /// Returns whether this settings record maps to one generic shared-engine texture format.
        /// </summary>
        /// <returns><c>true</c> when the color-format id is generic and can be handled by the shared texture processor.</returns>
        public bool UsesGenericColorFormat() {
            return TryResolveGenericColorFormat(ColorFormatId, out _);
        }

        /// <summary>
        /// Returns whether the selected generic, native Xbox or PS2 GS format stores palette indices.
        /// </summary>
        /// <returns><c>true</c> for generic Indexed4/Indexed8, Xbox P8 or a native PS2 indexed mode.</returns>
        public bool UsesIndexedColorFormat() {
            if (VitaNativeTextureFormatCatalog.TryGetFormat(ColorFormatId, out VitaNativeTextureFormatDefinition vitaFormat)) return vitaFormat.IsIndexed;
            if (GxNativeTextureFormatCatalog.TryGetFormat(ColorFormatId, out GxNativeTextureFormatDefinition gxFormat)) {
                return gxFormat.IsIndexed;
            }
            if (PspTextureFormatCatalog.TryGetFormat(ColorFormatId, out PspTextureFormat pspFormat)) {
                return pspFormat.IsIndexed;
            }
            if (Ps2GsTextureFormatCatalog.TryGetFormat(ColorFormatId, out Ps2GsTextureFormatDefinition ps2Format)) {
                return ps2Format.IsIndexed;
            }
            if (XboxNativeTextureFormatCatalog.TryGetFormat(ColorFormatId, out XboxNativeTextureFormatDefinition nativeFormat)) {
                return nativeFormat.IsPaletted;
            }
            if (!TryResolveGenericColorFormat(ColorFormatId, out TextureAssetColorFormat colorFormat)) {
                return false;
            }

            return colorFormat == TextureAssetColorFormat.Indexed4
                || colorFormat == TextureAssetColorFormat.Indexed8;
        }

        /// <summary>
        /// Resolves the effective indexing method for the current texture settings.
        /// </summary>
        /// <returns>The configured indexing method, or the indexed default when none was authored yet.</returns>
        public TextureAssetIndexingMethod ResolveIndexingMethod() {
            if (!UsesIndexedColorFormat()) {
                throw new InvalidOperationException("Indexing methods are only valid for indexed texture formats.");
            } else if (string.IsNullOrWhiteSpace(IndexingMethodId)
                || string.Equals(IndexingMethodId, TextureAssetIndexingMethod.QuantizedIndexed.ToString(), StringComparison.Ordinal)) {
                return TextureAssetIndexingMethod.QuantizedIndexed;
            }

            throw new InvalidOperationException($"Unsupported texture indexing method id '{IndexingMethodId}'.");
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
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Rgba5551.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Rgba5551;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Ia4.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Ia4;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Ia8.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Ia8;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Ia16.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Ia16;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.I4.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.I4;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.I8.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.I8;
                return true;
            }
            if (string.Equals(colorFormatId, TextureAssetColorFormat.Yuv16.ToString(), StringComparison.Ordinal)) {
                colorFormat = TextureAssetColorFormat.Yuv16;
                return true;
            }

            colorFormat = default;
            return false;
        }
    }
}
