namespace helengine.editor;

/// <summary>Enumerates every published RSX texture encoding and validates platform-specific layout identifiers.</summary>
public static class PlayStation3TextureFormatCatalog {
    /// <summary>Immutable catalog of the 27 RSX encodings, including shared-channel, depth and floating-point storage.</summary>
    public static IReadOnlyList<PlayStation3TextureFormat> Formats { get; } = Array.AsReadOnly(new PlayStation3TextureFormat[] {
        new("B8", 0x01, 1, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("A1R5G5B5", 0x02, 2, 1, 1, TextureAssetAlphaPrecision.Binary),
        new("A4R4G4B4", 0x03, 2, 1, 1, TextureAssetAlphaPrecision.A4),
        new("R5G6B5", 0x04, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("A8R8G8B8", 0x05, 4, 1, 1, TextureAssetAlphaPrecision.A8),
        new("DXT1", 0x06, 8, 4, 4, TextureAssetAlphaPrecision.Binary),
        new("DXT3", 0x07, 16, 4, 4, TextureAssetAlphaPrecision.A4),
        new("DXT5", 0x08, 16, 4, 4, TextureAssetAlphaPrecision.A8),
        new("G8B8", 0x0b, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("B8R8_G8R8", 0x0d, 4, 2, 1, TextureAssetAlphaPrecision.Opaque),
        new("R8B8_R8G8", 0x0e, 4, 2, 1, TextureAssetAlphaPrecision.Opaque),
        new("R6G5B5", 0x0f, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("DEPTH24_D8", 0x10, 4, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("DEPTH24_D8_FLOAT", 0x11, 4, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("DEPTH16", 0x12, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("DEPTH16_FLOAT", 0x13, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("X16", 0x14, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("Y16_X16", 0x15, 4, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("R5G5B5A1", 0x17, 2, 1, 1, TextureAssetAlphaPrecision.Binary),
        new("HILO8", 0x18, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("HILO_S8", 0x19, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("W16_Z16_Y16_X16_FLOAT", 0x1a, 8, 1, 1, TextureAssetAlphaPrecision.A8),
        new("W32_Z32_Y32_X32_FLOAT", 0x1b, 16, 1, 1, TextureAssetAlphaPrecision.A8),
        new("X32_FLOAT", 0x1c, 4, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("D1R5G5B5", 0x1d, 2, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("D8R8G8B8", 0x1e, 4, 1, 1, TextureAssetAlphaPrecision.Opaque),
        new("Y16_X16_FLOAT", 0x1f, 4, 1, 1, TextureAssetAlphaPrecision.Opaque)
    });
    /// <summary>Resolves a base hardware code and rejects reserved RSX encodings.</summary>
    public static PlayStation3TextureFormat ResolveCode(byte code) {
        foreach (PlayStation3TextureFormat format in Formats) if (format.Code == code) return format;
        throw new InvalidDataException($"Unsupported RSX texture format 0x{code:X2}.");
    }
    /// <summary>Resolves an exact setting identifier; compressed encodings expose their supported packed layout only.</summary>
    public static PlayStation3TextureFormat Resolve(string id, out bool linear, out bool mipmapped) {
        string[] parts = (id ?? string.Empty).Split('.');
        if (parts.Length < 3 || parts.Length > 4 || parts[0] != "Ps3" || (parts[2] != "Linear" && parts[2] != "Swizzled")
            || (parts.Length == 4 && parts[3] != "Mipmapped")) throw new InvalidDataException($"Invalid PS3 texture format '{id}'.");
        linear = parts[2] == "Linear"; mipmapped = parts.Length == 4;
        foreach (PlayStation3TextureFormat format in Formats) {
            if (format.Name == parts[1] && (!format.IsCompressed || linear)) return format;
        }
        throw new InvalidDataException($"Unsupported PS3 texture layout '{id}'.");
    }
    /// <summary>Gets the complete mip count down to one texel on both axes.</summary>
    public static int GetMipCount(int width, int height) {
        int count = 1;
        while (width > 1 || height > 1) { width = Math.Max(1, width / 2); height = Math.Max(1, height / 2); count++; }
        return count;
    }
    /// <summary>Computes native storage with constant linear pixel or block pitch across mipmaps.</summary>
    public static int GetPayloadLength(PlayStation3TextureFormat format, int width, int height, int mips, bool linear, int pitch) {
        if (width < 1 || height < 1 || width > 4096 || height > 4096 || mips < 1 || mips > GetMipCount(width, height))
            throw new InvalidDataException("PS3 texture dimensions or mip count are invalid.");
        if (!linear && format.BlockWidth == 1 && ((width & (width - 1)) != 0 || (height & (height - 1)) != 0))
            throw new InvalidDataException("Swizzled PS3 textures require power-of-two dimensions.");
        if (linear && (pitch < format.GetRowBytes(width) || pitch > 0xfffff || pitch % format.BytesPerBlock != 0))
            throw new InvalidDataException("PS3 linear row pitch is invalid.");
        long size = 0;
        for (int mip = 0; mip < mips; mip++) {
            int row = linear ? pitch : format.GetRowBytes(width);
            size += (long)row * ((height + format.BlockHeight - 1) / format.BlockHeight);
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        if (size > 64 * 1024 * 1024) throw new InvalidDataException("PS3 texture payload exceeds the 64 MiB per-texture limit.");
        return checked((int)size);
    }
    /// <summary>Estimates the native payload from a setting without including GTF metadata.</summary>
    public static bool TryCalculateMemory(string id, int width, int height, out long bytes) {
        bytes = 0;
        if (id == null || !id.StartsWith("Ps3.", StringComparison.Ordinal) || id == "Ps3.GtfSource") return false;
        try {
            PlayStation3TextureFormat format = Resolve(id, out bool linear, out bool mipmapped);
            bytes = GetPayloadLength(format, width, height, mipmapped ? GetMipCount(width, height) : 1, linear, format.GetRowBytes(width));
            return true;
        } catch (InvalidDataException) { return false; }
    }
}
