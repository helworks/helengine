namespace helengine {
    /// <summary>Describes a declared GX2 surface format and canonical sampled-channel mapping independently of PPC container endian order.</summary>
    public sealed class WiiUNativeTextureFormatDefinition : IDisposable {
        /// <summary>Owns intrinsic alpha policies for the selected native sampled channels.</summary>
        [NativeOwnedMember] TextureAssetAlphaPrecision[] AlphaPrecisions;
        /// <summary>Constructs one pinned SDK surface-format description.</summary>
        public WiiUNativeTextureFormatDefinition(string id, uint hardwareFormat) {
            if (id == null || id.Length == 0) throw new ArgumentException("GX2 format requires a stable identifier.");
            Id = id; HardwareFormat = hardwareFormat; BaseFormat = (int)(hardwareFormat & 63); int numeric = (int)(hardwareFormat >> 8);
            IsNv12 = hardwareFormat == 0x81; IsBlockCompressed = BaseFormat >= 0x31 && BaseFormat <= 0x35;
            NumericKind = IsNv12 ? WiiUNativeTextureNumericKind.Planar : hardwareFormat == 0x811 || hardwareFormat == 0x81c ? WiiUNativeTextureNumericKind.DepthStencil : numeric == 1 || numeric == 3 ? WiiUNativeTextureNumericKind.Integer : numeric == 2 ? WiiUNativeTextureNumericKind.SignedNormalized : numeric == 4 ? WiiUNativeTextureNumericKind.Srgb : numeric == 8 ? WiiUNativeTextureNumericKind.Float : WiiUNativeTextureNumericKind.UnsignedNormalized;
            BitsPerPixel = IsNv12 ? 12 : GetBitsPerPixel(BaseFormat); ComponentCount = GetComponentCount(BaseFormat);
            BlockWidth = IsBlockCompressed ? 4 : 1; BlockHeight = IsBlockCompressed ? 4 : 1; BytesPerBlock = IsBlockCompressed ? BitsPerPixel * 2 : 0;
            ComponentMap = BaseFormat == 12 || BaseFormat == 27 ? 0x03020100u : BaseFormat == 0x11 ? 0x01040405u : ComponentCount == 4 ? 0x00010203u : ComponentCount == 3 ? 0x00010205u : ComponentCount == 2 ? 0x00010405u : 0x00040405u;
            SupportsCooking = NumericKind != WiiUNativeTextureNumericKind.Integer && NumericKind != WiiUNativeTextureNumericKind.DepthStencil && NumericKind != WiiUNativeTextureNumericKind.Planar; SupportsPreview = SupportsCooking;
            UnsupportedReason = NumericKind == WiiUNativeTextureNumericKind.Integer ? "Native integer data requires exact raw import and a compatible integer shader." : NumericKind == WiiUNativeTextureNumericKind.DepthStencil ? "Native depth/stencil data requires exact raw import and a compatible shader." : IsNv12 ? "Native NV12 planes require exact raw import and a compatible planar sampling shader." : "";
            int alphaBits = !SupportsCooking || ComponentCount != 4 ? 0 : BaseFormat == 10 || BaseFormat == 12 || BaseFormat == 0x31 ? 1 : BaseFormat == 11 || BaseFormat == 0x32 ? 4 : BaseFormat == 25 || BaseFormat == 27 ? NumericKind == WiiUNativeTextureNumericKind.SignedNormalized ? 1 : 2 : 8;
            DefaultAlphaPrecision = alphaBits == 0 ? TextureAssetAlphaPrecision.Opaque : alphaBits == 1 ? TextureAssetAlphaPrecision.Binary : alphaBits == 2 ? TextureAssetAlphaPrecision.A2 : alphaBits == 4 ? TextureAssetAlphaPrecision.A4 : TextureAssetAlphaPrecision.A8;
            if (alphaBits == 2) AlphaPrecisions = new TextureAssetAlphaPrecision[] { TextureAssetAlphaPrecision.Opaque, TextureAssetAlphaPrecision.Binary, TextureAssetAlphaPrecision.A2 };
            else { AlphaPrecisions = new TextureAssetAlphaPrecision[(int)DefaultAlphaPrecision + 1]; for (int index = 0; index < AlphaPrecisions.Length; index++) AlphaPrecisions[index] = (TextureAssetAlphaPrecision)index; }
        }
        /// <summary>Gets the stable WiiU identifier preserving the pinned SDK format spelling.</summary>
        public string Id { get; }
        /// <summary>Gets a human-readable native format label.</summary>
        public string DisplayName { get { return Id.Replace("WiiU.", "Wii U "); } }
        /// <summary>Gets the exact GX2SurfaceFormat word including numeric interpretation.</summary>
        public uint HardwareFormat { get; }
        /// <summary>Gets the physical Latte format code, excluding numeric flags.</summary>
        public int BaseFormat { get; }
        /// <summary>Gets the canonical GX2 channel selector bytes in sampled RGBA order.</summary>
        public uint ComponentMap { get; }
        /// <summary>Gets the native numeric sampling interpretation.</summary>
        public WiiUNativeTextureNumericKind NumericKind { get; }
        /// <summary>Gets physical bits per texel, including twelve-bit NV12 planes.</summary>
        public int BitsPerPixel { get; }
        /// <summary>Gets native physical components before channel selectors.</summary>
        public int ComponentCount { get; }
        /// <summary>Gets whether standard BC blocks occupy native storage.</summary>
        public bool IsBlockCompressed { get; }
        /// <summary>Gets whether native storage contains Y and interleaved UV planes.</summary>
        public bool IsNv12 { get; }
        /// <summary>Gets whether this format stores palette indices; GX2 has no indexed surface formats.</summary>
        public bool IsIndexed { get { return false; } }
        /// <summary>Gets horizontal texels per block.</summary>
        public int BlockWidth { get; }
        /// <summary>Gets vertical texels per block.</summary>
        public int BlockHeight { get; }
        /// <summary>Gets compressed block bytes, zero for scalar storage.</summary>
        public int BytesPerBlock { get; }
        /// <summary>Gets whether normalized source RGBA has an implemented native image encoder.</summary>
        public bool SupportsCooking { get; }
        /// <summary>Gets whether this format has a defined ordinary RGBA sampled preview.</summary>
        public bool SupportsPreview { get; }
        /// <summary>Gets whether exact native bytes can be imported without image coercion.</summary>
        public bool SupportsRawImport { get { return true; } }
        /// <summary>Explains why specialized shader data is unavailable to ordinary image cooking.</summary>
        public string UnsupportedReason { get; }
        /// <summary>Gets preferred intrinsic alpha metadata for this format.</summary>
        public TextureAssetAlphaPrecision DefaultAlphaPrecision { get; }
        /// <summary>Gets the borrowed alpha policy array owned by this description.</summary>
        [NativeBorrowedReturn] public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get { return AlphaPrecisions; } }
        /// <summary>Checks alpha metadata without admitting GX A3 or DS A5 policies.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) { for (int index = 0; index < AlphaPrecisions.Length; index++) if (AlphaPrecisions[index] == alpha) return true; return false; }
        /// <summary>Counts canonical compact transport bytes without the WGT1 descriptor or native GPU allocation padding.</summary>
        public int GetTransportByteLength(int width, int height) { WiiUNativeTextureLayout layout = new WiiUNativeTextureLayout(this, width, height); try { return layout.TexelLength; } finally { NativeOwnership.Release(ref layout); } }
        /// <summary>Counts texture-only LINEAR_ALIGNED allocation using Latte's 256-byte base and max(64,2048/bpp) element pitch alignment.</summary>
        public int GetVramByteLength(int width, int height) {
            WiiUNativeTextureLayout layout = new WiiUNativeTextureLayout(this, width, height);
            try {
                int elementBits = IsNv12 ? 8 : IsBlockCompressed ? BytesPerBlock * 8 : BitsPerPixel; int elementWidth = layout.StorageWidth / BlockWidth; int elementHeight = layout.StorageHeight / BlockHeight;
                int pitchAlignment = Math.Max(64, 2048 / elementBits); int pitch = (elementWidth + pitchAlignment - 1) / pitchAlignment * pitchAlignment; int bytes = checked(pitch * elementHeight * (elementBits / 8));
                return IsNv12 ? checked((bytes + 255) / 256 * 256 + bytes / 2) : bytes;
            } finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Gets a sampled channel's physical selector; four denotes zero and five denotes one.</summary>
        public int GetComponent(int channel) { if (channel < 0 || channel > 3) throw new ArgumentOutOfRangeException(nameof(channel)); return (int)((ComponentMap >> ((3 - channel) * 8)) & 7); }
        /// <summary>Gets physical bit rates from the pinned GX2 surface enum.</summary>
        static int GetBitsPerPixel(int code) {
            if (code == 1 || code == 2) return 8;
            if (code == 5 || code == 6 || code == 7 || code == 8 || code == 10 || code == 11 || code == 12) return 16;
            if (code == 13 || code == 14 || code == 15 || code == 16 || code == 17 || code == 22 || code == 25 || code == 26 || code == 27) return 32;
            if (code == 28 || code == 29 || code == 30 || code == 31 || code == 32) return 64;
            if (code == 34 || code == 35) return 128;
            if (code == 49 || code == 52) return 4;
            if (code == 50 || code == 51 || code == 53) return 8;
            throw new ArgumentException("Unknown GX2 physical surface format.");
        }
        /// <summary>Gets physical components for canonical sampled-channel defaults.</summary>
        static int GetComponentCount(int code) {
            if (code == 10 || code == 11 || code == 12 || code == 25 || code == 26 || code == 27 || code == 31 || code == 32 || code == 34 || code == 35 || code >= 49 && code <= 51) return 4;
            if (code == 8 || code == 22) return 3;
            if (code == 2 || code == 7 || code == 15 || code == 16 || code == 17 || code == 28 || code == 29 || code == 30 || code == 53) return 2;
            return 1;
        }
        /// <summary>Releases owned policy storage while process-lifetime catalog entries remain borrowed to callers.</summary>
        public void Dispose() { NativeOwnership.Release(ref AlphaPrecisions); }
    }
}
