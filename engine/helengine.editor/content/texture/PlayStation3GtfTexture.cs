using System.Buffers.Binary;

namespace helengine.editor;

/// <summary>Owns one validated two-dimensional GTF texture without altering its native RSX texel bytes.</summary>
public sealed class PlayStation3GtfTexture {
    /// <summary>Reads a selected standard 36-byte texture attribute from a bounded big-endian GTF file.</summary>
    public PlayStation3GtfTexture(byte[] file, int textureIndex = 0) {
        if (file == null || file.Length < 48 || file.Length > 65 * 1024 * 1024) throw new InvalidDataException("GTF file is truncated or exceeds its size limit.");
        uint version = Read32(file, 0);
        if (version != 0x02000101 && version != 0x02000000 && version != 0x02000001 && version != 0x02000100)
            throw new InvalidDataException("Unsupported GTF version.");
        uint count = Read32(file, 8);
        if (count == 0 || count > 4096 || 12L + count * 36L > file.Length || textureIndex < 0 || textureIndex >= count)
            throw new InvalidDataException("GTF texture table or selection is invalid.");
        int attribute = 12 + textureIndex * 36;
        int descriptor = attribute + 12;
        FormatByte = file[descriptor]; MipCount = file[descriptor + 1];
        if ((FormatByte & 0x80) == 0) throw new InvalidDataException("Bordered GTF texels require a separate resource contract; this pipeline accepts borderless GTF textures.");
        if (file[descriptor + 2] != 2 || file[descriptor + 3] != 0 || Read16(file, descriptor + 12) != 1)
            throw new InvalidDataException("Engine texture assets require a non-cube 2D GTF texture; cube and volume assets need a dedicated resource type.");
        if ((FormatByte & 0x40) != 0) throw new InvalidDataException("Unnormalized GTF coordinates cannot be sampled by the engine's normalized texture shaders.");
        Format = PlayStation3TextureFormatCatalog.ResolveCode((byte)(FormatByte & 0x1f));
        Linear = (FormatByte & 0x20) != 0; Remap = Read32(file, descriptor + 4);
        if ((Remap & ~0x1ffffu) != 0) throw new InvalidDataException("GTF remap contains reserved bits.");
        for (int channel = 0; channel < 4; channel++) if (((Remap >> (8 + channel * 2)) & 3) == 3)
            throw new InvalidDataException("GTF remap contains a reserved operation.");
        _ = PlayStation3GtfPixelCodec.GetSamplingRemap(Format.Code, Remap);
        Width = Read16(file, descriptor + 8); Height = Read16(file, descriptor + 10);
        uint pitch = Read32(file, descriptor + 16);
        if (pitch > 0xfffff) throw new InvalidDataException("GTF pitch exceeds the hardware field.");
        Pitch = (int)pitch;
        int required = PlayStation3TextureFormatCatalog.GetPayloadLength(Format, Width, Height, MipCount, Linear, Pitch);
        uint offset = Read32(file, attribute + 4); uint size = Read32(file, attribute + 8);
        if (offset < 12L + count * 36L || size < required || size > 64 * 1024 * 1024 || (long)offset + size > file.Length)
            throw new InvalidDataException("GTF texture offset, byte count or mip chain is invalid.");
        Pixels = file.AsSpan((int)offset, (int)size).ToArray();
    }
    /// <summary>Gets the resolved RSX pixel encoding.</summary>
    public PlayStation3TextureFormat Format { get; }
    /// <summary>Gets all persisted RSX storage flags.</summary>
    public byte FormatByte { get; }
    /// <summary>Gets the texture's base-level width.</summary>
    public int Width { get; }
    /// <summary>Gets the texture's base-level height.</summary>
    public int Height { get; }
    /// <summary>Gets the number of stored mip levels including the base level.</summary>
    public int MipCount { get; }
    /// <summary>Gets whether uncompressed texels use linear rows.</summary>
    public bool Linear { get; }
    /// <summary>Gets the explicit row stride retained across linear mip levels.</summary>
    public int Pitch { get; }
    /// <summary>Gets the hardware component remapping descriptor.</summary>
    public uint Remap { get; }
    /// <summary>Gets an owned copy of the original native texels and mipmaps.</summary>
    public byte[] Pixels { get; }
    /// <summary>Reads a big-endian descriptor or header word from a prevalidated range.</summary>
    public static uint Read32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
    /// <summary>Reads a big-endian texture extent from a prevalidated descriptor.</summary>
    public static ushort Read16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
}
