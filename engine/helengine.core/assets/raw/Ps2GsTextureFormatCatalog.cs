namespace helengine {
    /// <summary>Lists all thirteen native GS texture PSMs and the three verified CLUT choices for each indexed mode.</summary>
    public static class Ps2GsTextureFormatCatalog {
        /// <summary>Retains immutable catalog descriptions for the process lifetime.</summary>
        static readonly Ps2GsTextureFormatDefinition[] NativeFormats = new Ps2GsTextureFormatDefinition[] {
            new Ps2GsTextureFormatDefinition("PS2.PSMCT32", 0, 0, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMCT24", 1, 3, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMCT16", 2, 4, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMCT16S", 10, 5, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMT8", 19, 1, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMT8.CLUT16", 19, 1, 2),
            new Ps2GsTextureFormatDefinition("PS2.PSMT8.CLUT16S", 19, 1, 10),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4", 20, 2, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4.CLUT16", 20, 2, 2),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4.CLUT16S", 20, 2, 10),
            new Ps2GsTextureFormatDefinition("PS2.PSMT8H", 27, 6, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMT8H.CLUT16", 27, 6, 2),
            new Ps2GsTextureFormatDefinition("PS2.PSMT8H.CLUT16S", 27, 6, 10),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4HL", 36, 7, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4HL.CLUT16", 36, 7, 2),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4HL.CLUT16S", 36, 7, 10),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4HH", 44, 8, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4HH.CLUT16", 44, 8, 2),
            new Ps2GsTextureFormatDefinition("PS2.PSMT4HH.CLUT16S", 44, 8, 10),
            new Ps2GsTextureFormatDefinition("PS2.PSMZ32", 48, 9, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMZ24", 49, 10, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMZ16", 50, 11, 0),
            new Ps2GsTextureFormatDefinition("PS2.PSMZ16S", 58, 12, 0)
        };
        /// <summary>Gets borrowed descriptions of all twenty-three native texture and CLUT combinations.</summary>
        [NativeBorrowedReturn] public static Ps2GsTextureFormatDefinition[] Formats { get { return NativeFormats; } }
        /// <summary>Finds a stable platform selection identifier.</summary>
        public static bool TryGetFormat(string id, out Ps2GsTextureFormatDefinition format) {
            for (int index = 0; index < NativeFormats.Length; index++) if (NativeFormats[index].Id == id) { format = NativeFormats[index]; return true; }
            format = null; return false;
        }
        /// <summary>Finds a catalog combination using preserved platform-owned enum values.</summary>
        public static bool TryGetPlatformFormat(int platformCode, int platformClutCode, out Ps2GsTextureFormatDefinition format) {
            for (int index = 0; index < NativeFormats.Length; index++) {
                if (NativeFormats[index].PlatformFormatCode == platformCode && (!NativeFormats[index].IsIndexed || NativeFormats[index].PlatformClutFormatCode == platformClutCode)) {
                    format = NativeFormats[index]; return true;
                }
            }
            format = null; return false;
        }
    }
}
