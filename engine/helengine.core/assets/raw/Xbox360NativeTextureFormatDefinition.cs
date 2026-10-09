namespace helengine {
    /// <summary>Describes a Xenos code separately from its verified cooking and emulator support.</summary>
    public sealed class Xbox360NativeTextureFormatDefinition : IDisposable {
        /// <summary>Owns the representable alpha policies.</summary>
        [NativeOwnedMember] TextureAssetAlphaPrecision[] AlphaPrecisions;
        /// <summary>Creates a permanent catalog description with canonical unsigned fetch controls.</summary>
        public Xbox360NativeTextureFormatDefinition(int code, string name, int blockWidth, int blockHeight, int bytesPerBlock,
            int swizzle, TextureAssetAlphaPrecision alpha, bool cookable, bool xeniaSupported, string reason) {
            HardwareFormat = code; Id = "Xbox360.Linear." + name; DisplayName = "Xbox 360 " + name;
            BlockWidth = blockWidth; BlockHeight = blockHeight; BytesPerBlock = bytesPerBlock; Swizzle = swizzle;
            DefaultAlphaPrecision = alpha; SupportsCooking = cookable; IsSupportedByXenia = xeniaSupported; UnsupportedReason = reason;
            if (cookable && alpha == TextureAssetAlphaPrecision.A2) {
                AlphaPrecisions = new TextureAssetAlphaPrecision[] { TextureAssetAlphaPrecision.Opaque, TextureAssetAlphaPrecision.Binary, TextureAssetAlphaPrecision.A2 };
            } else {
                AlphaPrecisions = new TextureAssetAlphaPrecision[cookable ? (int)alpha + 1 : 0];
                for (int index = 0; index < AlphaPrecisions.Length; index++) AlphaPrecisions[index] = (TextureAssetAlphaPrecision)index;
            }
        }
        /// <summary>Gets the stable linear format selection identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the readable hardware format name.</summary>
        public string DisplayName { get; }
        /// <summary>Gets the six-bit Xenos texture format.</summary>
        public int HardwareFormat { get; }
        /// <summary>Gets the horizontal texels represented by one storage block.</summary>
        public int BlockWidth { get; }
        /// <summary>Gets the vertical texels represented by one storage block.</summary>
        public int BlockHeight { get; }
        /// <summary>Gets the storage bytes in each complete block.</summary>
        public int BytesPerBlock { get; }
        /// <summary>Gets the canonical twelve-bit fetch swizzle.</summary>
        public int Swizzle { get; }
        /// <summary>Gets unsigned component interpretation for the canonical payload.</summary>
        public int Signs { get { return 0; } }
        /// <summary>Gets fractional normalized rather than integer interpretation.</summary>
        public int NumberFormat { get { return 0; } }
        /// <summary>Gets the zero exponent adjustment used for normalized authored channels.</summary>
        public int ExponentAdjust { get { return 0; } }
        /// <summary>Gets whether an exact storage encoder and decoder exist.</summary>
        public bool SupportsCooking { get; }
        /// <summary>Gets whether the pinned Xenia backend supplies a texture loader for this exact code.</summary>
        public bool IsSupportedByXenia { get; }
        /// <summary>Gets the reason a known code lacks a verified cooking implementation.</summary>
        public string UnsupportedReason { get; }
        /// <summary>Gets the preferred alpha precision supported by the storage and swizzle.</summary>
        public TextureAssetAlphaPrecision DefaultAlphaPrecision { get; }
        /// <summary>Gets borrowed policies owned by this description.</summary>
        [NativeBorrowedReturn] public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get { return AlphaPrecisions; } }
        /// <summary>Tests whether a requested alpha policy has a representable encoding.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) {
            if (AlphaPrecisions == null) return false;
            for (int index = 0; index < AlphaPrecisions.Length; index++) if (AlphaPrecisions[index] == alpha) return true;
            return false;
        }
        /// <summary>Releases owned policy storage; catalog descriptions remain borrowed for the process lifetime.</summary>
        public void Dispose() { NativeOwnership.Release(ref AlphaPrecisions); }
    }
}
