namespace helengine {
    /// <summary>Describes one exact deko3d image format and its selected Tegra storage layout.</summary>
    public sealed class SwitchTextureFormat {
        /// <summary>Creates an immutable native format description with explicit block geometry and alpha capability.</summary>
        public SwitchTextureFormat(string name, int code, bool blockLinear, int blockWidth, int blockHeight, int bytesPerBlock, TextureAssetAlphaPrecision alpha) {
            Name = name; Code = code; BlockLinear = blockLinear;
            BlockWidth = blockWidth; BlockHeight = blockHeight; BytesPerBlock = bytesPerBlock; Alpha = alpha;
            Id = "Switch." + (blockLinear ? "BlockLinear." : "Linear.") + name;
        }
        /// <summary>Gets the stable platform cook identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the exact deko3d enum name suffix.</summary>
        public string Name { get; }
        /// <summary>Gets the pinned deko3d image-format enum value.</summary>
        public int Code { get; }
        /// <summary>Gets whether storage uses Tegra GOB addressing.</summary>
        public bool BlockLinear { get; }
        /// <summary>Gets the number of pixels across a stored element.</summary>
        public int BlockWidth { get; }
        /// <summary>Gets the number of pixels down a stored element.</summary>
        public int BlockHeight { get; }
        /// <summary>Gets the byte width of a texel or compression block.</summary>
        public int BytesPerBlock { get; }
        /// <summary>Gets the intrinsic alpha precision used by the image cook path.</summary>
        public TextureAssetAlphaPrecision Alpha { get; }
        /// <summary>Accepts intrinsic alpha or explicitly discarded source alpha.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) => alpha == TextureAssetAlphaPrecision.Opaque || alpha == Alpha;
    }
}
