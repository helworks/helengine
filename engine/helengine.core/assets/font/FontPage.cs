namespace helengine {
    /// <summary>Describes an optional pre-rendered atlas without retaining any pixel payload.</summary>
    public sealed class FontPage {
        /// <summary>Creates one root-relative texture reference and its unscaled atlas dimensions.</summary>
        public FontPage(string texturePath, int width, int height) {
            TexturePath = CanonicalPackagedAssetPath.ValidateCanonical(texturePath);
            if (width < 1 || height < 1 || width > 2048 || height > 2048) { throw new ArgumentOutOfRangeException(nameof(width)); }
            Width = width; Height = height;
        }
        /// <summary>Gets the texture asset path interpreted by the font's content source.</summary>
        public string TexturePath { get; }
        /// <summary>Gets the pixel width used to convert normalized glyph rectangles.</summary>
        public int Width { get; }
        /// <summary>Gets the pixel height used to convert normalized glyph rectangles.</summary>
        public int Height { get; }
        /// <summary>Gets the RGBA upload budget; source pixels are released after upload.</summary>
        public long ResidentBytes => (long)Width * Height * 4;
        /// <summary>Copies descriptor ownership when an exporter clones a font without its base atlas pixels.</summary>
        public static FontPage[] Copy(FontPage[] source) {
            if (source == null) { return null; }
            FontPage[] result = new FontPage[source.Length];
            for (int index = 0; index < source.Length; index++) { result[index] = new FontPage(source[index].TexturePath, source[index].Width, source[index].Height); }
            return result;
        }
    }
}
