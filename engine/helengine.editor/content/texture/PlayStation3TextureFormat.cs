namespace helengine.editor;

/// <summary>Describes one RSX pixel encoding independently of its GTF container and storage layout.</summary>
public sealed class PlayStation3TextureFormat {
    /// <summary>Creates a native encoding with its hardware code, block dimensions and alpha capacity.</summary>
    public PlayStation3TextureFormat(string name, byte code, int bytes, int blockWidth, int blockHeight, TextureAssetAlphaPrecision alpha) {
        Name = name; Code = code; BytesPerBlock = bytes; BlockWidth = blockWidth; BlockHeight = blockHeight; AlphaPrecision = alpha;
    }
    /// <summary>Gets the stable RSX encoding name used in platform setting identifiers.</summary>
    public string Name { get; }
    /// <summary>Gets the base hardware code without linear, unnormalized or border flags.</summary>
    public byte Code { get; }
    /// <summary>Gets the byte count of one pixel or compressed block.</summary>
    public int BytesPerBlock { get; }
    /// <summary>Gets the number of texels represented by one block across a row.</summary>
    public int BlockWidth { get; }
    /// <summary>Gets the number of texel rows represented by one block.</summary>
    public int BlockHeight { get; }
    /// <summary>Gets the maximum alpha precision retained by ordinary RGBA conversion.</summary>
    public TextureAssetAlphaPrecision AlphaPrecision { get; }
    /// <summary>Gets whether storage consists of compressed blocks rather than individual texels.</summary>
    public bool IsCompressed => BlockWidth > 1 || Code == 0x18 || Code == 0x19;
    /// <summary>Computes a tightly packed row of blocks, including partial edge blocks.</summary>
    public int GetRowBytes(int width) => checked(((width + BlockWidth - 1) / BlockWidth) * BytesPerBlock);
}
