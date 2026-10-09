namespace helengine.editor {
    /// <summary>
    /// Selects valid generic texture format and alpha pairs when an editor platform
    /// does not publish its own capability combinations. Stored import settings
    /// are only changed after an explicit selection, never while displaying them.
    /// </summary>
    public static class TextureAssetFormatSelectionService {
        /// <summary>
        /// Resolves the alpha precision displayed for a generic color format,
        /// retaining a valid selection or choosing the format's maximum precision.
        /// Platform-owned format identifiers retain their authored alpha value.
        /// </summary>
        /// <param name="colorFormatId">Generic or platform-owned format identifier.</param>
        /// <param name="alphaPrecision">Previously selected alpha precision.</param>
        /// <returns>A supported generic alpha precision or the platform-owned value.</returns>
        public static TextureAssetAlphaPrecision ResolveAlphaPrecision(string colorFormatId, TextureAssetAlphaPrecision alphaPrecision) {
            if (!TextureAssetProcessorSettings.TryResolveGenericColorFormat(colorFormatId, out TextureAssetColorFormat format)
                || TextureAssetPixelCodec.IsAlphaPrecisionSupported(format, alphaPrecision)) {
                return alphaPrecision;
            }

            return TextureAssetPixelCodec.GetMaximumAlphaPrecision(format);
        }

        /// <summary>
        /// Lists alpha precisions representable by one generic pixel layout.
        /// Platform-owned formats remain governed by platform capability metadata.
        /// </summary>
        /// <param name="colorFormatId">Currently displayed color format identifier.</param>
        /// <returns>Alpha choices in the existing enum order.</returns>
        public static TextureAssetAlphaPrecision[] GetSupportedAlphaPrecisions(string colorFormatId) {
            TextureAssetAlphaPrecision[] precisions = Enum.GetValues<TextureAssetAlphaPrecision>();
            if (!TextureAssetProcessorSettings.TryResolveGenericColorFormat(colorFormatId, out TextureAssetColorFormat format)) {
                return precisions;
            }

            List<TextureAssetAlphaPrecision> supported = new List<TextureAssetAlphaPrecision>();
            foreach (TextureAssetAlphaPrecision precision in precisions) {
                if (TextureAssetPixelCodec.IsAlphaPrecisionSupported(format, precision)) {
                    supported.Add(precision);
                }
            }

            return supported.ToArray();
        }

        /// <summary>
        /// Preserves an explicitly chosen alpha precision by selecting RGBA32 when
        /// the current generic format cannot store it. Platform-owned formats are
        /// left to their registered capability rules.
        /// </summary>
        /// <param name="settings">Pending settings changed by the alpha selector.</param>
        public static void PreserveSelectedAlphaPrecision(TextureAssetProcessorSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (TextureAssetProcessorSettings.TryResolveGenericColorFormat(settings.ColorFormatId, out TextureAssetColorFormat format)
                && !TextureAssetPixelCodec.IsAlphaPrecisionSupported(format, settings.AlphaPrecision)) {
                if (!TextureAssetPixelCodec.IsAlphaPrecisionSupported(TextureAssetColorFormat.Rgba32, settings.AlphaPrecision)) {
                    throw new InvalidOperationException($"Unsupported texture alpha precision '{settings.AlphaPrecision}'.");
                }

                settings.ColorFormat = TextureAssetColorFormat.Rgba32;
            }
        }
    }
}
