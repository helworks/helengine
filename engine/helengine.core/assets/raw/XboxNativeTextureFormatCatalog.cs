namespace helengine {
    /// <summary>Publishes the 42 color codes in the pinned nxdk NV2A register definitions, including DXT1, DXT3 and DXT5.</summary>
    public static class XboxNativeTextureFormatCatalog {
        /// <summary>Permanent catalog storage borrowed by editor and runtime callers.</summary>
        static readonly XboxNativeTextureFormatDefinition[] NativeFormats = new XboxNativeTextureFormatDefinition[] {
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.Y8", 0x00, 1, false, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.AY8", 0x01, 1, false, TextureAssetAlphaPrecision.A8, true),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.A1R5G5B5", 0x02, 2, false, TextureAssetAlphaPrecision.Binary, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.X1R5G5B5", 0x03, 2, false, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.A4R4G4B4", 0x04, 2, false, TextureAssetAlphaPrecision.A4, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.R5G6B5", 0x05, 2, false, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.A8R8G8B8", 0x06, 4, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.X8R8G8B8", 0x07, 4, false, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.I8_A8R8G8B8", 0x0b, 1, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.DXT1", 0x0c, 0, false, TextureAssetAlphaPrecision.Binary, false),
            new XboxNativeTextureFormatDefinition("Xbox.DXT3", 0x0e, 0, false, TextureAssetAlphaPrecision.A4, false),
            new XboxNativeTextureFormatDefinition("Xbox.DXT5", 0x0f, 0, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.A1R5G5B5", 0x10, 2, true, TextureAssetAlphaPrecision.Binary, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.R5G6B5", 0x11, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.A8R8G8B8", 0x12, 4, true, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.Y8", 0x13, 1, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.R8B8", 0x16, 2, true, TextureAssetAlphaPrecision.A8, true),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.G8B8", 0x17, 2, true, TextureAssetAlphaPrecision.A8, true),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.A8", 0x19, 1, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.A8Y8", 0x1a, 2, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.AY8", 0x1b, 1, true, TextureAssetAlphaPrecision.A8, true),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.X1R5G5B5", 0x1c, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.A4R4G4B4", 0x1d, 2, true, TextureAssetAlphaPrecision.A4, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.X8R8G8B8", 0x1e, 4, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.A8", 0x1f, 1, true, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.A8Y8", 0x20, 2, true, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.CR8YB8CB8YA8", 0x24, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.YB8CR8YA8CB8", 0x25, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.R6G5B5", 0x27, 2, false, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.G8B8", 0x28, 2, false, TextureAssetAlphaPrecision.A8, true),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.R8B8", 0x29, 2, false, TextureAssetAlphaPrecision.A8, true),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.DEPTH_Y16_FIXED", 0x2c, 2, false, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.DEPTH_X8_Y24_FIXED", 0x2e, 4, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.DEPTH_Y16_FIXED", 0x30, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.DEPTH_Y16_FLOAT", 0x31, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.Y16", 0x35, 2, true, TextureAssetAlphaPrecision.Opaque, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.A8B8G8R8", 0x3a, 4, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.B8G8R8A8", 0x3b, 4, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Swizzled.R8G8B8A8", 0x3c, 4, false, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.A8B8G8R8", 0x3f, 4, true, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.B8G8R8A8", 0x40, 4, true, TextureAssetAlphaPrecision.A8, false),
            new XboxNativeTextureFormatDefinition("Xbox.Linear.R8G8B8A8", 0x41, 4, true, TextureAssetAlphaPrecision.A8, false)
        };

        /// <summary>Gets the complete catalog; its descriptions and policy arrays are retained by the process.</summary>
        [NativeBorrowedReturn]
        public static XboxNativeTextureFormatDefinition[] Formats { get { return NativeFormats; } }

        /// <summary>Finds an exact stable platform format identifier without accepting unknown aliases.</summary>
        public static bool TryGetFormat(string id, out XboxNativeTextureFormatDefinition definition) {
            for (int index = 0; index < NativeFormats.Length; index++) {
                if (NativeFormats[index].Id == id) {
                    definition = NativeFormats[index];
                    return true;
                }
            }
            definition = null;
            return false;
        }

        /// <summary>Finds the native description for a serialized NV097 color code.</summary>
        public static bool TryGetHardwareFormat(int code, out XboxNativeTextureFormatDefinition definition) {
            for (int index = 0; index < NativeFormats.Length; index++) {
                if (NativeFormats[index].HardwareFormat == code) {
                    definition = NativeFormats[index];
                    return true;
                }
            }
            definition = null;
            return false;
        }
    }
}
