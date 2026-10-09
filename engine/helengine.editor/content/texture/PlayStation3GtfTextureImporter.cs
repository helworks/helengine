namespace helengine.editor;

/// <summary>Imports the base level of native PS3 GTF textures as RGBA8 previews without changing the source file.</summary>
public sealed class PlayStation3GtfTextureImporter : ITextureImporter {
    /// <summary>Decodes the first texture attribute for the standard editor import contract.</summary>
    public TextureAsset ImportTexture(Stream stream) => ImportTexture(stream, 0);
    /// <summary>Decodes a selected 2D texture from a multi-texture GTF, bounding reads even for non-seekable streams.</summary>
    public TextureAsset ImportTexture(Stream stream, int textureIndex) {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream snapshot = new();
        byte[] chunk = new byte[8192];
        int count;
        while ((count = stream.Read(chunk, 0, chunk.Length)) != 0) {
            if (snapshot.Length + count > 65 * 1024 * 1024) throw new InvalidDataException("GTF exceeds the 65 MiB input limit.");
            snapshot.Write(chunk, 0, count);
        }
        PlayStation3GtfTexture texture = new(snapshot.ToArray(), textureIndex);
        return new TextureAsset { Width = (ushort)texture.Width, Height = (ushort)texture.Height,
            Colors = PlayStation3GtfPixelCodec.Decode(texture), PaletteColors = Array.Empty<byte>(),
            ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8 };
    }
}
