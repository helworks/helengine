namespace helengine {
    /// <summary>Describes an exact GXM format word, its channel swizzle and honest image conversion capabilities.</summary>
    public sealed class VitaNativeTextureFormatDefinition : IDisposable {
        /// <summary>Owns the alpha choices representable by this channel arrangement.</summary>
        [NativeOwnedMember] TextureAssetAlphaPrecision[] AlphaPrecisions;
        /// <summary>Constructs an immutable description for a catalogued SDK format word.</summary>
        public VitaNativeTextureFormatDefinition(string id, uint hardwareFormat) {
            if (id == null || id.Length == 0 || (hardwareFormat & 0x00FF8FFF) != 0) throw new ArgumentException("Invalid GXM identifier or reserved format bits.");
            Id = id; HardwareFormat = hardwareFormat; BaseFormat = (int)(hardwareFormat >> 24); Swizzle = (int)((hardwareFormat >> 12) & 7);
            BitsPerPixel = GetBitsPerPixel(BaseFormat); ComponentCount = GetComponentCount(BaseFormat);
            IsIndexed = BaseFormat == 0x94 || BaseFormat == 0x95; IndexBitDepth = IsIndexed ? BitsPerPixel : 0; PaletteEntryCount = IsIndexed ? 1 << IndexBitDepth : 0;
            IsPvrtc = BaseFormat >= 0x80 && BaseFormat <= 0x83; IsBlockCompressed = BaseFormat >= 0x80 && BaseFormat <= 0x8b;
            BlockWidth = IsPvrtc ? BitsPerPixel == 2 ? 8 : 4 : IsBlockCompressed ? 4 : 1; BlockHeight = IsBlockCompressed ? 4 : 1;
            BytesPerBlock = IsBlockCompressed ? BlockWidth * BlockHeight * BitsPerPixel / 8 : 0;
            NumericKind = BaseFormat == 0x17 || BaseFormat == 0x18 || BaseFormat == 0x1f ? VitaNativeTextureNumericKind.Integer : BaseFormat == 0x14 ? VitaNativeTextureNumericKind.Mixed : BaseFormat == 0x15 ? VitaNativeTextureNumericKind.DepthStencil : BaseFormat >= 0x90 && BaseFormat <= 0x92 ? VitaNativeTextureNumericKind.Yuv : IsBlockCompressed ? VitaNativeTextureNumericKind.Compressed : BaseFormat == 0x0b || BaseFormat == 0x11 || BaseFormat == 0x12 || BaseFormat == 0x13 || BaseFormat == 0x19 || BaseFormat == 0x1a || BaseFormat == 0x1b || BaseFormat == 0x1e || BaseFormat == 0x9a ? VitaNativeTextureNumericKind.Float : BaseFormat == 1 || BaseFormat == 6 || BaseFormat == 8 || BaseFormat == 10 || BaseFormat == 13 || BaseFormat == 16 || BaseFormat == 29 || BaseFormat == 0x99 ? VitaNativeTextureNumericKind.SignedNormalized : VitaNativeTextureNumericKind.UnsignedNormalized;
            SupportsCooking = NumericKind != VitaNativeTextureNumericKind.Integer && NumericKind != VitaNativeTextureNumericKind.Mixed && NumericKind != VitaNativeTextureNumericKind.DepthStencil; SupportsPreview = SupportsCooking;
            UnsupportedReason = NumericKind == VitaNativeTextureNumericKind.Integer ? "Shader integer data requires exact raw import; normalized RGBA cooking and preview are undefined." : NumericKind == VitaNativeTextureNumericKind.Mixed ? "Mixed signed/unsigned bump channels require exact raw import and a native shader contract." : NumericKind == VitaNativeTextureNumericKind.DepthStencil ? "Depth/stencil data requires exact raw import and a native shader contract." : "";
            DefaultLayoutType = IsPvrtc ? VitaNativeTextureLayoutType.SwizzledArbitrary : VitaNativeTextureLayoutType.Linear;
            int component = GetSwizzleComponent(3);
            bool coupled = component < 4 && (GetSwizzleComponent(0) == component || GetSwizzleComponent(1) == component || GetSwizzleComponent(2) == component);
            int alphaBits = component >= 4 || !SupportsCooking ? 0 : BaseFormat == 4 || BaseFormat == 0x85 ? 1 : BaseFormat == 2 || BaseFormat == 0x86 ? 4 : BaseFormat == 14 || BaseFormat == 0x9a ? 2 : 8;
            DefaultAlphaPrecision = alphaBits == 0 ? TextureAssetAlphaPrecision.Opaque : alphaBits == 1 ? TextureAssetAlphaPrecision.Binary : alphaBits == 2 ? TextureAssetAlphaPrecision.A2 : alphaBits == 4 ? TextureAssetAlphaPrecision.A4 : TextureAssetAlphaPrecision.A8;
            if (coupled) AlphaPrecisions = new TextureAssetAlphaPrecision[] { DefaultAlphaPrecision };
            else if (alphaBits == 2) AlphaPrecisions = new TextureAssetAlphaPrecision[] { TextureAssetAlphaPrecision.Opaque, TextureAssetAlphaPrecision.Binary, TextureAssetAlphaPrecision.A2 };
            else {
                AlphaPrecisions = new TextureAssetAlphaPrecision[(int)DefaultAlphaPrecision + 1];
                for (int index = 0; index < AlphaPrecisions.Length; index++) AlphaPrecisions[index] = (TextureAssetAlphaPrecision)index;
            }
        }
        /// <summary>Gets the stable identifier preserving exact SDK format spelling.</summary>
        public string Id { get; }
        /// <summary>Gets a readable SDK channel-format label.</summary>
        public string DisplayName { get { return Id.Replace("Vita.", "Vita "); } }
        /// <summary>Gets the complete GXM format word including channel swizzle.</summary>
        public uint HardwareFormat { get; }
        /// <summary>Gets the high-byte physical base-format code.</summary>
        public int BaseFormat { get; }
        /// <summary>Gets the declared three-bit channel-swizzle index.</summary>
        public int Swizzle { get; }
        /// <summary>Gets native numeric interpretation independently of bytes or image capability.</summary>
        public VitaNativeTextureNumericKind NumericKind { get; }
        /// <summary>Gets whether normalized RGBA source pixels have a native image encoder.</summary>
        public bool SupportsCooking { get; }
        /// <summary>Gets whether native pixels have a defined RGBA image preview.</summary>
        public bool SupportsPreview { get; }
        /// <summary>Gets whether native shader-data bytes can be validated and imported unchanged.</summary>
        public bool SupportsRawImport { get { return true; } }
        /// <summary>Explains specialized formats without silently substituting another format.</summary>
        public string UnsupportedReason { get; }
        /// <summary>Gets whether texels contain native palette indices.</summary>
        public bool IsIndexed { get; }
        /// <summary>Gets native index precision, zero for direct formats.</summary>
        public int IndexBitDepth { get; }
        /// <summary>Gets full native palette capacity, sixteen or 256 entries.</summary>
        public int PaletteEntryCount { get; }
        /// <summary>Gets whether neighboring PVRTC words reconstruct color.</summary>
        public bool IsPvrtc { get; }
        /// <summary>Gets whether texels occupy native compression blocks.</summary>
        public bool IsBlockCompressed { get; }
        /// <summary>Gets native storage bits per texel; YUV420 uses twelve bits over its planes.</summary>
        public int BitsPerPixel { get; }
        /// <summary>Gets physical components before channel swizzle.</summary>
        public int ComponentCount { get; }
        /// <summary>Gets horizontal texels represented by a compression block.</summary>
        public int BlockWidth { get; }
        /// <summary>Gets vertical texels represented by a compression block.</summary>
        public int BlockHeight { get; }
        /// <summary>Gets compressed block bytes, zero for component or planar formats.</summary>
        public int BytesPerBlock { get; }
        /// <summary>Gets canonical cooker storage; PVRTC retains Morton word order.</summary>
        public VitaNativeTextureLayoutType DefaultLayoutType { get; }
        /// <summary>Gets preferred intrinsic alpha precision for this exact swizzle.</summary>
        public TextureAssetAlphaPrecision DefaultAlphaPrecision { get; }
        /// <summary>Gets borrowed alpha choices owned by this description.</summary>
        [NativeBorrowedReturn] public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get { return AlphaPrecisions; } }
        /// <summary>Checks exact supported alpha choices, excluding unrelated GX A3 policies.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) {
            if (AlphaPrecisions == null) return false;
            for (int index = 0; index < AlphaPrecisions.Length; index++) if (AlphaPrecisions[index] == alpha) return true;
            return false;
        }
        /// <summary>Limits PVRTC to Morton storage, YUV to scanlines and block formats to scanlines or Morton blocks.</summary>
        public bool SupportsLayout(VitaNativeTextureLayoutType type) {
            if (type != VitaNativeTextureLayoutType.Linear && type != VitaNativeTextureLayoutType.Swizzled && type != VitaNativeTextureLayoutType.SwizzledArbitrary && type != VitaNativeTextureLayoutType.Tiled) return false;
            if (IsPvrtc) return type == VitaNativeTextureLayoutType.Swizzled || type == VitaNativeTextureLayoutType.SwizzledArbitrary;
            if (NumericKind == VitaNativeTextureNumericKind.Yuv) return type == VitaNativeTextureLayoutType.Linear;
            return !IsBlockCompressed || type != VitaNativeTextureLayoutType.Tiled;
        }
        /// <summary>Estimates exact canonical texel and palette bytes, excluding the VGT1 descriptor.</summary>
        public int GetVramByteLength(int width, int height) {
            VitaNativeTextureLayout layout = new VitaNativeTextureLayout(this, width, height, DefaultLayoutType);
            try { return checked(layout.TexelLength + layout.PaletteLength); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Maps sampled RGBA to physical components; four denotes zero and five denotes one.</summary>
        public int GetSwizzleComponent(int channel) {
            if (channel < 0 || channel > 3) throw new ArgumentOutOfRangeException(nameof(channel));
            if (BaseFormat == 3) return channel == 3 ? 3 : 2 - channel;
            if (NumericKind == VitaNativeTextureNumericKind.Yuv || BaseFormat == 0x84) return channel == 3 ? 5 : channel;
            if (ComponentCount == 4) {
                if (Swizzle == 0 || Swizzle == 4) return channel == 3 && Swizzle == 4 ? 5 : channel;
                if (Swizzle == 1 || Swizzle == 5) return channel == 3 ? Swizzle == 5 ? 5 : 3 : 2 - channel;
                if (Swizzle == 2 || Swizzle == 6) return channel == 3 && Swizzle == 6 ? 5 : 3 - channel;
                return channel == 3 && Swizzle == 7 ? 5 : channel == 0 ? 1 : channel == 1 ? 2 : channel == 2 ? 3 : 0;
            }
            if (ComponentCount == 3) return channel == 3 ? 5 : Swizzle == 0 ? channel : 2 - channel;
            if (ComponentCount == 2) {
                if (Swizzle == 0 || Swizzle == 1) return channel < 2 ? channel : channel == 2 || Swizzle == 1 ? 4 : 5;
                if (Swizzle == 2) return channel == 3 ? 1 : 0;
                if (Swizzle == 3) return channel == 3 ? 0 : 1;
                if (Swizzle == 4) return channel % 2;
                return channel == 0 ? 1 : channel == 1 ? 0 : 4;
            }
            if (Swizzle == 0) return channel == 0 ? 0 : channel == 3 ? 5 : 4;
            if (Swizzle == 1) return channel == 0 ? 0 : 4;
            if (Swizzle == 2) return channel == 0 ? 0 : 5;
            if (Swizzle == 3) return 0;
            if (Swizzle == 4 || Swizzle == 5) return channel == 3 ? Swizzle == 4 ? 4 : 5 : 0;
            return channel == 3 ? 0 : Swizzle == 6 ? 4 : 5;
        }
        /// <summary>Returns physical bit rates from the pinned GXM base-format enum.</summary>
        static int GetBitsPerPixel(int code) {
            if (code == 0 || code == 1 || code == 0x95) return 8;
            if (code >= 2 && code <= 11 || code == 0x92) return 16;
            if (code >= 12 && code <= 21 || code >= 23 && code <= 26 || code == 0x9a) return 32;
            if (code >= 27 && code <= 31) return 64;
            if (code == 0x80 || code == 0x82) return 2;
            if (code == 0x81 || code == 0x83 || code == 0x84 || code == 0x85 || code == 0x88 || code == 0x89 || code == 0x94) return 4;
            if (code == 0x86 || code == 0x87 || code == 0x8a || code == 0x8b) return 8;
            if (code == 0x90 || code == 0x91) return 12;
            if (code == 0x98 || code == 0x99) return 24;
            throw new ArgumentException("Unknown GXM base format.");
        }
        /// <summary>Returns component counts used by SDK channel-swizzle families.</summary>
        static int GetComponentCount(int code) {
            if (code == 2 || code == 3 || code == 4 || code == 12 || code == 13 || code == 14 || code == 27 || code == 28 || code == 29 || code >= 0x80 && code <= 0x87 || code == 0x94 || code == 0x95 || code == 0x9a) return 4;
            if (code == 5 || code == 6 || code == 25 || code == 26 || code == 0x98 || code == 0x99) return 3;
            if (code == 7 || code == 8 || code == 15 || code == 16 || code == 17 || code == 21 || code == 30 || code == 31 || code == 0x8a || code == 0x8b) return 2;
            return 1;
        }
        /// <summary>Releases owned policy storage; catalog descriptions remain borrowed for the process lifetime.</summary>
        public void Dispose() { NativeOwnership.Release(ref AlphaPrecisions); }
    }
}
