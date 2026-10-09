namespace helengine {
    /// <summary>Describes a verified GX sampler or raw depth-copy view and its optional TLUT encoding.</summary>
    public sealed class GxNativeTextureFormatDefinition : IDisposable {
        /// <summary>Owns the precise alpha policies representable by this selection.</summary>
        [NativeOwnedMember] TextureAssetAlphaPrecision[] AlphaPrecisions;
        /// <summary>Constructs a description only for known sampler codes and compatible TLUT choices.</summary>
        public GxNativeTextureFormatDefinition(string id, int nativeFormat, int tlutFormat) {
            bool indexed = nativeFormat == 8 || nativeFormat == 9 || nativeFormat == 10;
            bool known = nativeFormat >= 0 && nativeFormat <= 6 || indexed || nativeFormat == 14 || nativeFormat == 17 || nativeFormat == 19 || nativeFormat == 22;
            if (id == null || id.Length == 0 || !known || indexed && (tlutFormat < 0 || tlutFormat > 2) || !indexed && tlutFormat != -1) throw new ArgumentException("Unknown GX sampler or incompatible TLUT format.");
            Id = id; NativeFormat = nativeFormat; HardwareFormat = nativeFormat & 15; TlutFormat = tlutFormat;
            IsIndexed = indexed; IsDepthView = nativeFormat == 17 || nativeFormat == 19 || nativeFormat == 22;
            IndexBitDepth = nativeFormat == 8 ? 4 : nativeFormat == 9 ? 8 : nativeFormat == 10 ? 14 : 0;
            PaletteEntryLimit = indexed ? 1 << IndexBitDepth : 0;
            BlockWidth = HardwareFormat == 0 || HardwareFormat == 8 || HardwareFormat == 14 || HardwareFormat == 1 || HardwareFormat == 2 || HardwareFormat == 9 ? 8 : 4;
            BlockHeight = HardwareFormat == 0 || HardwareFormat == 8 || HardwareFormat == 14 ? 8 : 4;
            BytesPerBlock = HardwareFormat == 6 ? 64 : 32;
            int policy = indexed ? tlutFormat == 0 ? 3 : tlutFormat == 1 ? 0 : 5 : nativeFormat == 22 || HardwareFormat == 4 ? 0 : HardwareFormat == 5 ? 5 : HardwareFormat == 14 ? 1 : HardwareFormat == 0 || HardwareFormat == 2 ? 2 : 3;
            DefaultAlphaPrecision = (TextureAssetAlphaPrecision)policy;
            if (HardwareFormat == 0 || HardwareFormat == 1 || nativeFormat == 19) AlphaPrecisions = new TextureAssetAlphaPrecision[] { DefaultAlphaPrecision };
            else if (policy == 5) AlphaPrecisions = new TextureAssetAlphaPrecision[] { TextureAssetAlphaPrecision.Opaque, TextureAssetAlphaPrecision.Binary, TextureAssetAlphaPrecision.A3 };
            else {
                AlphaPrecisions = new TextureAssetAlphaPrecision[policy + 1];
                for (int index = 0; index < AlphaPrecisions.Length; index++) AlphaPrecisions[index] = (TextureAssetAlphaPrecision)index;
            }
        }
        /// <summary>Gets the stable engine selection identifier.</summary>
        public string Id { get; }
        /// <summary>Gets a readable native texture and TLUT label.</summary>
        public string DisplayName { get { return Id.Replace("Gx.", "GX ").Replace(".TLUT.", " / TLUT "); } }
        /// <summary>Gets the full format code, retaining raw depth-copy alias flags.</summary>
        public int NativeFormat { get; }
        /// <summary>Gets the physical four-bit sampler code passed to GX_InitTexObj.</summary>
        public int HardwareFormat { get; }
        /// <summary>Gets the TLUT code, or minus one for direct textures.</summary>
        public int TlutFormat { get; }
        /// <summary>Gets whether texels contain palette indices.</summary>
        public bool IsIndexed { get; }
        /// <summary>Gets whether this selection preserves depth-copy channels without changing depth state.</summary>
        public bool IsDepthView { get; }
        /// <summary>Gets the meaningful index bits, including all fourteen CI14X2 bits.</summary>
        public int IndexBitDepth { get; }
        /// <summary>Gets the maximum native TLUT entry count.</summary>
        public int PaletteEntryLimit { get; }
        /// <summary>Gets the horizontal texels in a native storage tile.</summary>
        public int BlockWidth { get; }
        /// <summary>Gets the vertical texels in a native storage tile.</summary>
        public int BlockHeight { get; }
        /// <summary>Gets the bytes in one complete native tile.</summary>
        public int BytesPerBlock { get; }
        /// <summary>Gets the preferred intrinsic alpha precision.</summary>
        public TextureAssetAlphaPrecision DefaultAlphaPrecision { get; }
        /// <summary>Gets borrowed alpha choices owned by this description.</summary>
        [NativeBorrowedReturn] public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get { return AlphaPrecisions; } }
        /// <summary>Checks explicit policies without admitting unrelated appended enum values.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) {
            if (AlphaPrecisions == null) return false;
            for (int index = 0; index < AlphaPrecisions.Length; index++) if (AlphaPrecisions[index] == alpha) return true;
            return false;
        }
        /// <summary>Calculates native texel bytes including complete edge tiles.</summary>
        public int GetPixelByteLength(int width, int height) {
            if (width < 1 || height < 1 || width > 1024 || height > 1024) throw new ArgumentOutOfRangeException("GX dimensions must be within 1..1024.");
            return checked(((width + BlockWidth - 1) / BlockWidth) * ((height + BlockHeight - 1) / BlockHeight) * BytesPerBlock);
        }
        /// <summary>Estimates storage with the maximum palette capacity when authored count is unknown, excluding the descriptor.</summary>
        public int GetVramByteLength(int width, int height) { return GetVramByteLength(width, height, PaletteEntryLimit); }
        /// <summary>Calculates tiled texel bytes plus the actual sixteen-entry-aligned palette.</summary>
        public int GetVramByteLength(int width, int height, int paletteEntryCount) {
            if (IsIndexed && (paletteEntryCount < 16 || paletteEntryCount > PaletteEntryLimit || paletteEntryCount % 16 != 0) || !IsIndexed && paletteEntryCount != 0) throw new ArgumentException("Invalid GX TLUT entry count.");
            return checked(GetPixelByteLength(width, height) + paletteEntryCount * 2);
        }
        /// <summary>Releases owned policy storage; catalog descriptions remain borrowed for the process lifetime.</summary>
        public void Dispose() { NativeOwnership.Release(ref AlphaPrecisions); }
    }
}
