namespace helengine {
    /// <summary>Lists eleven GX samplers, their indexed TLUT alternatives and three raw depth-copy views.</summary>
    public static class GxNativeTextureFormatCatalog {
        /// <summary>Retains twenty verified selections for the process lifetime.</summary>
        static readonly GxNativeTextureFormatDefinition[] NativeFormats = new GxNativeTextureFormatDefinition[] {
            new GxNativeTextureFormatDefinition("Gx.I4", 0, -1), new GxNativeTextureFormatDefinition("Gx.I8", 1, -1),
            new GxNativeTextureFormatDefinition("Gx.IA4", 2, -1), new GxNativeTextureFormatDefinition("Gx.IA8", 3, -1),
            new GxNativeTextureFormatDefinition("Gx.RGB565", 4, -1), new GxNativeTextureFormatDefinition("Gx.RGB5A3", 5, -1),
            new GxNativeTextureFormatDefinition("Gx.RGBA8", 6, -1),
            new GxNativeTextureFormatDefinition("Gx.CI4.TLUT.IA8", 8, 0), new GxNativeTextureFormatDefinition("Gx.CI4.TLUT.RGB565", 8, 1), new GxNativeTextureFormatDefinition("Gx.CI4.TLUT.RGB5A3", 8, 2),
            new GxNativeTextureFormatDefinition("Gx.CI8.TLUT.IA8", 9, 0), new GxNativeTextureFormatDefinition("Gx.CI8.TLUT.RGB565", 9, 1), new GxNativeTextureFormatDefinition("Gx.CI8.TLUT.RGB5A3", 9, 2),
            new GxNativeTextureFormatDefinition("Gx.CI14X2.TLUT.IA8", 10, 0), new GxNativeTextureFormatDefinition("Gx.CI14X2.TLUT.RGB565", 10, 1), new GxNativeTextureFormatDefinition("Gx.CI14X2.TLUT.RGB5A3", 10, 2),
            new GxNativeTextureFormatDefinition("Gx.CMPR", 14, -1), new GxNativeTextureFormatDefinition("Gx.Z8", 17, -1),
            new GxNativeTextureFormatDefinition("Gx.Z16", 19, -1), new GxNativeTextureFormatDefinition("Gx.Z24X8", 22, -1)
        };
        /// <summary>Gets borrowed selections; EFB copy-only conversion encodings are not advertised as samplers.</summary>
        [NativeBorrowedReturn] public static GxNativeTextureFormatDefinition[] Formats { get { return NativeFormats; } }
        /// <summary>Finds a selection by stable engine identifier.</summary>
        public static bool TryGetFormat(string id, out GxNativeTextureFormatDefinition format) {
            for (int index = 0; index < NativeFormats.Length; index++) if (NativeFormats[index].Id == id) { format = NativeFormats[index]; return true; }
            format = null; return false;
        }
        /// <summary>Finds the exact full native code and TLUT combination, preserving depth aliases.</summary>
        public static bool TryGetHardwareFormat(int nativeFormat, int tlutFormat, out GxNativeTextureFormatDefinition format) {
            for (int index = 0; index < NativeFormats.Length; index++) if (NativeFormats[index].NativeFormat == nativeFormat && NativeFormats[index].TlutFormat == tlutFormat) { format = NativeFormats[index]; return true; }
            format = null; return false;
        }
    }
}
