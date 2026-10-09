namespace helengine {
    /// <summary>Lists all sixty-four Xenos codes while publishing only formats with implemented storage codecs.</summary>
    public static class Xbox360NativeTextureFormatCatalog {
        /// <summary>Retains immutable descriptions for the process lifetime.</summary>
        static readonly Xbox360NativeTextureFormatDefinition[] NativeFormats = new Xbox360NativeTextureFormatDefinition[] {
            new Xbox360NativeTextureFormatDefinition(0, "1_REVERSE", 1, 1, 0, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(1, "1", 1, 1, 0, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(2, "8", 1, 1, 1, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(3, "1_5_5_5", 1, 1, 2, 0x688, (TextureAssetAlphaPrecision)1, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(4, "5_6_5", 1, 1, 2, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(5, "6_5_5", 1, 1, 2, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(6, "8_8_8_8", 1, 1, 4, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(7, "2_10_10_10", 1, 1, 4, 0x688, TextureAssetAlphaPrecision.A2, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(8, "8_A", 1, 1, 1, 0x16d, TextureAssetAlphaPrecision.A8, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(9, "8_B", 1, 1, 1, 0xa00, (TextureAssetAlphaPrecision)0, true, false, "Known sampling alias lacks a loader in pinned Xenia."),
            new Xbox360NativeTextureFormatDefinition(10, "8_8", 1, 1, 2, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(11, "Cr_Y1_Cb_Y0_REP", 2, 1, 4, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(12, "Y1_Cr_Y0_Cb_REP", 2, 1, 4, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(13, "16_16_EDRAM", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "EDRAM surface format is not usable as a texture."),
            new Xbox360NativeTextureFormatDefinition(14, "8_8_8_8_A", 1, 1, 4, 0x688, (TextureAssetAlphaPrecision)3, true, false, "Known sampling alias lacks a loader in pinned Xenia."),
            new Xbox360NativeTextureFormatDefinition(15, "4_4_4_4", 1, 1, 2, 0x688, (TextureAssetAlphaPrecision)2, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(16, "10_11_11", 1, 1, 4, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(17, "11_11_10", 1, 1, 4, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(18, "DXT1", 4, 4, 8, 0x688, (TextureAssetAlphaPrecision)1, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(19, "DXT2_3", 4, 4, 16, 0x688, (TextureAssetAlphaPrecision)2, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(20, "DXT4_5", 4, 4, 16, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(21, "16_16_16_16_EDRAM", 1, 1, 8, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "EDRAM surface format is not usable as a texture."),
            new Xbox360NativeTextureFormatDefinition(22, "24_8", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(23, "24_8_FLOAT", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(24, "16", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(25, "16_16", 1, 1, 4, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(26, "16_16_16_16", 1, 1, 8, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(27, "16_EXPAND", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(28, "16_16_EXPAND", 1, 1, 4, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(29, "16_16_16_16_EXPAND", 1, 1, 8, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(30, "16_FLOAT", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(31, "16_16_FLOAT", 1, 1, 4, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(32, "16_16_16_16_FLOAT", 1, 1, 8, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(33, "32", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(34, "32_32", 1, 1, 8, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(35, "32_32_32_32", 1, 1, 16, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(36, "32_FLOAT", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(37, "32_32_FLOAT", 1, 1, 8, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(38, "32_32_32_32_FLOAT", 1, 1, 16, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(39, "32_AS_8", 4, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(40, "32_AS_8_8", 2, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(41, "16_MPEG", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(42, "16_16_MPEG", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(43, "8_INTERLACED", 1, 1, 1, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(44, "32_AS_8_INTERLACED", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(45, "32_AS_8_8_INTERLACED", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(46, "16_INTERLACED", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(47, "16_MPEG_INTERLACED", 1, 1, 2, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(48, "16_16_MPEG_INTERLACED", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(49, "DXN", 4, 4, 16, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(50, "8_8_8_8_AS_16_16_16_16", 1, 1, 4, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(51, "DXT1_AS_16_16_16_16", 4, 4, 8, 0x688, (TextureAssetAlphaPrecision)1, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(52, "DXT2_3_AS_16_16_16_16", 4, 4, 16, 0x688, (TextureAssetAlphaPrecision)2, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(53, "DXT4_5_AS_16_16_16_16", 4, 4, 16, 0x688, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(54, "2_10_10_10_AS_16_16_16_16", 1, 1, 4, 0x688, TextureAssetAlphaPrecision.A2, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(55, "10_11_11_AS_16_16_16_16", 1, 1, 4, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(56, "11_11_10_AS_16_16_16_16", 1, 1, 4, 0xa88, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(57, "32_32_32_FLOAT", 1, 1, 12, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "Sampling or storage semantics are not verified by the pinned texture loader."),
            new Xbox360NativeTextureFormatDefinition(58, "DXT3A", 4, 4, 8, 0x16d, (TextureAssetAlphaPrecision)2, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(59, "DXT5A", 4, 4, 8, 0x16d, (TextureAssetAlphaPrecision)3, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(60, "CTX1", 4, 4, 8, 0xb08, (TextureAssetAlphaPrecision)0, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(61, "DXT3A_AS_1_1_1_1", 4, 4, 8, 0x688, (TextureAssetAlphaPrecision)1, true, true, ""),
            new Xbox360NativeTextureFormatDefinition(62, "8_8_8_8_GAMMA_EDRAM", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "EDRAM surface format is not usable as a texture."),
            new Xbox360NativeTextureFormatDefinition(63, "2_10_10_10_FLOAT_EDRAM", 1, 1, 4, 0xa00, (TextureAssetAlphaPrecision)0, false, false, "EDRAM surface format is not usable as a texture.")
        };
        /// <summary>Retains the forty-four implemented descriptions independently of emulator loader availability.</summary>
        static readonly Xbox360NativeTextureFormatDefinition[] CookableFormats = CreateSupportedFormats();
        /// <summary>Gets borrowed descriptions of every known hardware code, including unavailable EDRAM and video formats.</summary>
        [NativeBorrowedReturn] public static Xbox360NativeTextureFormatDefinition[] Formats { get { return NativeFormats; } }
        /// <summary>Gets borrowed descriptions that have actual encoders and decoders.</summary>
        [NativeBorrowedReturn] public static Xbox360NativeTextureFormatDefinition[] SupportedFormats { get { return CookableFormats; } }
        /// <summary>Finds a stable selection identifier without assuming that every hardware code is cookable.</summary>
        public static bool TryGetFormat(string id, out Xbox360NativeTextureFormatDefinition format) {
            for (int index = 0; index < NativeFormats.Length; index++) {
                if (NativeFormats[index].Id == id) { format = NativeFormats[index]; return true; }
            }
            format = null; return false;
        }
        /// <summary>Finds the description of a six-bit hardware code.</summary>
        public static bool TryGetHardwareFormat(int code, out Xbox360NativeTextureFormatDefinition format) {
            if (code < 0 || code >= NativeFormats.Length) { format = null; return false; }
            format = NativeFormats[code]; return true;
        }
        /// <summary>Builds process-owned supported storage descriptions without changing hardware identities.</summary>
        [NativeOwnedReturn] static Xbox360NativeTextureFormatDefinition[] CreateSupportedFormats() {
            int count = 0;
            for (int index = 0; index < NativeFormats.Length; index++) if (NativeFormats[index].SupportsCooking) count++;
            Xbox360NativeTextureFormatDefinition[] result = new Xbox360NativeTextureFormatDefinition[count];
            int target = 0;
            for (int index = 0; index < NativeFormats.Length; index++) if (NativeFormats[index].SupportsCooking) result[target++] = NativeFormats[index];
            return result;
        }
    }
}
