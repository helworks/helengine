namespace helengine.editor {
    /// <summary>Imports tex3ds 2D containers in all PICA encodings and Nintendo compression modes into RGBA previews.</summary>
    public sealed class Nintendo3DsT3xTextureImporter : ITextureImporter {
        /// <summary>Caps container buffering independently of its checked GPU allocation.</summary>
        const int MaximumFileBytes = 16 * 1024 * 1024;
        /// <summary>Imports the first subtexture, or the complete base level when no subtextures are declared.</summary>
        public TextureAsset ImportTexture(Stream stream) => ImportTexture(stream, 0);
        /// <summary>Imports a selected atlas rectangle from mip zero, undoing tex3ds packing rotation.</summary>
        public TextureAsset ImportTexture(Stream stream, int subTextureIndex) {
            if (stream == null || !stream.CanRead) throw new ArgumentException("A readable tex3ds stream is required.");
            if (subTextureIndex < 0) throw new ArgumentOutOfRangeException(nameof(subTextureIndex));
            using MemoryStream buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int count;
            while ((count = stream.Read(chunk, 0, chunk.Length)) != 0) {
                if (buffer.Length + count > MaximumFileBytes) throw new InvalidDataException("Tex3ds container exceeds the import limit.");
                buffer.Write(chunk, 0, count);
            }
            byte[] data = buffer.ToArray();
            if (data.Length < 9) throw new InvalidDataException("Truncated tex3ds header.");
            int subTextures = Word(data, 0);
            int parameters = data[2], code = data[3], levels = data[4];
            int width = 8 << (parameters & 7), height = 8 << (parameters >> 3 & 7);
            if ((parameters & 192) != 0 || code > 13 || levels > Math.Min(parameters & 7, parameters >> 3 & 7)
                || (subTextures == 0 ? subTextureIndex != 0 : subTextureIndex >= subTextures) || subTextures > 4096) throw new InvalidDataException("Unsupported tex3ds encoding, cubemap, mip count or subtexture index.");
            int payload = 5 + subTextures * 12;
            if (data.Length - payload < 4) throw new InvalidDataException("Truncated tex3ds subtexture table.");
            Nintendo3DsTextureFormat format = Nintendo3DsTextureFormatCatalog.GetFormat(code);
            int expected = 0;
            for (int level = 0; level <= levels; level++) expected += (width >> level) * (height >> level) * format.BitsPerPixel / 8;
            byte[] texels = Nintendo3DsTextureCompression.Decode(data, payload, expected);
            byte[] rgba = Nintendo3DsTextureCodec.DecodePixels(texels, 0, code, width, height, width, height);
            if (subTextures == 0) return new TextureAsset { Width = (ushort)width, Height = (ushort)height, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = rgba };
            for (int index = 0; index < subTextures; index++) {
                int entry = 5 + index * 12;
                if (Word(data, entry) < 1 || Word(data, entry + 2) < 1 || Word(data, entry) > 1024 || Word(data, entry + 2) > 1024
                    || Word(data, entry + 4) > 1024 || Word(data, entry + 6) > 1024 || Word(data, entry + 8) > 1024 || Word(data, entry + 10) > 1024) throw new InvalidDataException("Invalid tex3ds atlas rectangle.");
            }
            int selected = 5 + subTextureIndex * 12;
            int resultWidth = Word(data, selected), resultHeight = Word(data, selected + 2);
            int left = Word(data, selected + 4), top = Word(data, selected + 6), right = Word(data, selected + 8), bottom = Word(data, selected + 10);
            if (left >= right || top == bottom) throw new InvalidDataException("Empty tex3ds atlas rectangle.");
            bool rotated = top < bottom;
            int x = (rotated ? top : left) * width / 1024, y = (1024 - (rotated ? right : top)) * height / 1024;
            int physicalWidth = (rotated ? bottom - top : right - left) * width / 1024;
            int physicalHeight = (rotated ? right - left : top - bottom) * height / 1024;
            if (physicalWidth != (rotated ? resultHeight : resultWidth) || physicalHeight != (rotated ? resultWidth : resultHeight)
                || x + physicalWidth > width || y + physicalHeight > height) throw new InvalidDataException("Tex3ds rectangle does not match its dimensions.");
            byte[] cropped = new byte[resultWidth * resultHeight * 4];
            for (int row = 0; row < resultHeight; row++) {
                for (int column = 0; column < resultWidth; column++) {
                    int sourceX = x + (rotated ? row : column), sourceY = y + (rotated ? resultWidth - 1 - column : row);
                    Array.Copy(rgba, (sourceY * width + sourceX) * 4, cropped, (row * resultWidth + column) * 4, 4);
                }
            }
            return new TextureAsset { Width = (ushort)resultWidth, Height = (ushort)resultHeight, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = cropped };
        }
        /// <summary>Reads a little-endian atlas field after checking its bounds.</summary>
        static int Word(byte[] data, int offset) {
            if (offset < 0 || data.Length - offset < 2) throw new InvalidDataException("Truncated tex3ds atlas field.");
            return data[offset] | data[offset + 1] << 8;
        }
    }
}
