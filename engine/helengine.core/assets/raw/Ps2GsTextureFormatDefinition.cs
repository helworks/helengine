namespace helengine {
    /// <summary>Describes a native GS transfer format and its CSM1 palette independently of platform-owned asset classes.</summary>
    public sealed class Ps2GsTextureFormatDefinition : IDisposable {
        /// <summary>Owns the representable alpha policies until this description is disposed.</summary>
        [NativeOwnedMember] TextureAssetAlphaPrecision[] AlphaPrecisions;
        /// <summary>Constructs an immutable description for a verified GS texture and CLUT combination.</summary>
        public Ps2GsTextureFormatDefinition(string id, int code, int platformCode, int clutCode) {
            int expectedPlatformCode = code == 0 ? 0 : code == 19 ? 1 : code == 20 ? 2 : code == 1 ? 3 : code == 2 ? 4 : code == 10 ? 5 : code == 27 ? 6 : code == 36 ? 7 : code == 44 ? 8 : code == 48 ? 9 : code == 49 ? 10 : code == 50 ? 11 : code == 58 ? 12 : -1;
            bool indexed = code == 19 || code == 20 || code == 27 || code == 36 || code == 44;
            if (id == null || id.Length == 0 || expectedPlatformCode < 0 || platformCode != expectedPlatformCode ||
                (!indexed && clutCode != 0) || indexed && clutCode != 0 && clutCode != 2 && clutCode != 10) throw new ArgumentException("Unknown or inconsistent GS texture and CLUT description.");
            Id = id; FormatCode = code; PlatformFormatCode = platformCode; ClutFormatCode = clutCode;
            IsIndexed = code == 19 || code == 20 || code == 27 || code == 36 || code == 44;
            IndexBitDepth = IsIndexed ? code == 19 || code == 27 ? 8 : 4 : 0;
            BitsPerPixel = IsIndexed ? IndexBitDepth : code == 1 || code == 49 ? 24 : code == 2 || code == 10 || code == 50 || code == 58 ? 16 : 32;
            VramBitsPerPixel = code == 19 ? 8 : code == 20 ? 4 : code == 2 || code == 10 || code == 50 || code == 58 ? 16 : 32;
            PageWidth = code == 19 || code == 20 ? 128 : 64;
            PageHeight = code == 20 ? 128 : code == 19 || VramBitsPerPixel == 16 ? 64 : 32;
            BlockWidth = code == 19 ? 16 : code == 20 ? 32 : VramBitsPerPixel == 16 ? 16 : 8;
            BlockHeight = code == 19 || code == 20 ? 16 : 8;
            PaletteEntryCount = IsIndexed ? 1 << IndexBitDepth : 0;
            PaletteByteLength = PaletteEntryCount * (clutCode == 2 || clutCode == 10 ? 2 : 4);
            PlatformClutFormatCode = clutCode == 2 ? 4 : clutCode == 10 ? 5 : 0;
            ClutWidth = IsIndexed ? IndexBitDepth == 4 ? 8 : 16 : 0;
            ClutHeight = IsIndexed ? IndexBitDepth == 4 ? 2 : 16 : 0;
            int alphaCode = IsIndexed ? clutCode : code;
            DefaultAlphaPrecision = alphaCode == 1 || alphaCode == 49 ? TextureAssetAlphaPrecision.Opaque :
                alphaCode == 2 || alphaCode == 10 || alphaCode == 50 || alphaCode == 58 ? TextureAssetAlphaPrecision.Binary : TextureAssetAlphaPrecision.A8;
            AlphaPrecisions = new TextureAssetAlphaPrecision[(int)DefaultAlphaPrecision + 1];
            for (int index = 0; index < AlphaPrecisions.Length; index++) AlphaPrecisions[index] = (TextureAssetAlphaPrecision)index;
        }
        /// <summary>Gets the stable PS2 format identifier, including an optional sixteen-bit CLUT selection.</summary>
        public string Id { get; }
        /// <summary>Gets a readable native PSM and palette selection label.</summary>
        public string DisplayName { get { return Id.Replace("PS2.", "PS2 ").Replace(".", " "); } }
        /// <summary>Gets the actual GS texture PSM used for host transfer and texture fetch.</summary>
        public int FormatCode { get; }
        /// <summary>Gets the stable platform-owned serialized format and storage-mode value.</summary>
        public int PlatformFormatCode { get; }
        /// <summary>Gets the actual GS palette PSM, or zero for direct textures.</summary>
        public int ClutFormatCode { get; }
        /// <summary>Gets the stable platform storage-mode value for the palette.</summary>
        public int PlatformClutFormatCode { get; }
        /// <summary>Gets the compact host-transfer bits per texel, independent of VRAM occupancy.</summary>
        public int BitsPerPixel { get; }
        /// <summary>Gets the bits occupied in GS local memory, including high-index and twenty-four-bit storage.</summary>
        public int VramBitsPerPixel { get; }
        /// <summary>Gets the whether texels select entries in a CSM1 palette.</summary>
        public bool IsIndexed { get; }
        /// <summary>Gets the four or eight index bits for paletted textures; zero for direct textures.</summary>
        public int IndexBitDepth { get; }
        /// <summary>Gets the required full CLUT entry count, sixteen or two hundred fifty-six.</summary>
        public int PaletteEntryCount { get; }
        /// <summary>Gets the compact host bytes in the complete CLUT transfer.</summary>
        public int PaletteByteLength { get; }
        /// <summary>Gets the canonical CLUT transfer width, eight or sixteen texels.</summary>
        public int ClutWidth { get; }
        /// <summary>Gets the canonical CLUT transfer height, two or sixteen texels.</summary>
        public int ClutHeight { get; }
        /// <summary>Gets the gS local-memory page width for this PSM.</summary>
        public int PageWidth { get; }
        /// <summary>Gets the gS local-memory page height for this PSM.</summary>
        public int PageHeight { get; }
        /// <summary>Gets the horizontal texels in one 256-byte GS storage block.</summary>
        public int BlockWidth { get; }
        /// <summary>Gets the vertical texels in one 256-byte GS storage block.</summary>
        public int BlockHeight { get; }
        /// <summary>Gets the preferred precision representable by the texture or its chosen CLUT.</summary>
        public TextureAssetAlphaPrecision DefaultAlphaPrecision { get; }
        /// <summary>Gets borrowed representable alpha policies owned by this description.</summary>
        [NativeBorrowedReturn] public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get { return AlphaPrecisions; } }
        /// <summary>Checks alpha policy without admitting unrelated appended precision values.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) {
            if (AlphaPrecisions == null) return false;
            for (int index = 0; index < AlphaPrecisions.Length; index++) if (AlphaPrecisions[index] == alpha) return true;
            return false;
        }
        /// <summary>Computes a continuous compact GS host-transfer stream, including the last odd nibble.</summary>
        public int ComputeCpuPixelByteLength(int width, int height) {
            ValidateDimensions(width, height);
            return checked((int)(((long)width * height * BitsPerPixel + 7) / 8));
        }
        /// <summary>Computes conservative whole-page GS storage, retaining the distinct physical footprint of high-index modes.</summary>
        public int ComputeVramByteLength(int width, int height) {
            ValidateDimensions(width, height);
            return checked(((width + PageWidth - 1) / PageWidth) * ((height + PageHeight - 1) / PageHeight) * 8192);
        }
        /// <summary>Computes the full GS page allocated for each indexed texture's independent CLUT.</summary>
        public int ComputePaletteVramByteLength() { return IsIndexed ? 8192 : 0; }
        /// <summary>Gets compact transfer bytes using the canonical public sizing contract.</summary>
        public int GetPixelByteLength(int width, int height) { return ComputeCpuPixelByteLength(width, height); }
        /// <summary>Gets full-page GS texture allocation bytes, excluding the separate CLUT.</summary>
        public int GetTextureVramByteLength(int width, int height) { return ComputeVramByteLength(width, height); }
        /// <summary>Gets total full-page GS allocation bytes including an indexed texture's CLUT page.</summary>
        public int GetVramByteLength(int width, int height) { return checked(ComputeVramByteLength(width, height) + ComputePaletteVramByteLength()); }
        /// <summary>Restricts native texture dimensions to documented GS texture extents.</summary>
        static void ValidateDimensions(int width, int height) {
            if (width < 1 || height < 1 || width > 1024 || height > 1024) throw new ArgumentOutOfRangeException("GS texture dimensions must be within 1..1024.");
        }
        /// <summary>Releases policy storage; process-lifetime catalog descriptions remain borrowed.</summary>
        public void Dispose() { NativeOwnership.Release(ref AlphaPrecisions); }
    }
}
