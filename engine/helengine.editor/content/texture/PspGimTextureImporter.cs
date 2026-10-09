namespace helengine.editor {
    /// <summary>Imports little-endian PSP GIM images into RGBA previews, retaining native channel and block semantics.</summary>
    public sealed class PspGimTextureImporter : ITextureImporter {
        /// <summary>Caps a container before buffering its block table and pixel data.</summary>
        const int MaximumFileBytes = 64 * 1024 * 1024;
        /// <summary>Imports the first picture's first frame and base mip level.</summary>
        public TextureAsset ImportTexture(Stream stream) => ImportTexture(stream, 0);
        /// <summary>Imports the selected picture; frame zero and mip zero are used for the static engine texture.</summary>
        public TextureAsset ImportTexture(Stream stream, int pictureIndex) {
            if (stream == null || !stream.CanRead) throw new ArgumentException("A readable GIM stream is required.");
            if (pictureIndex < 0) throw new ArgumentOutOfRangeException(nameof(pictureIndex));
            using MemoryStream buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int count;
            while ((count = stream.Read(chunk, 0, chunk.Length)) != 0) {
                if (buffer.Length + count > MaximumFileBytes) throw new InvalidDataException("GIM exceeds the 64 MiB import limit.");
                buffer.Write(chunk, 0, count);
            }
            byte[] data = buffer.ToArray();
            byte[] signature = { 77, 73, 71, 46, 48, 48, 46, 49, 80, 83, 80, 0, 0, 0, 0, 0 };
            if (data.Length < 32 || !data.Take(16).SequenceEqual(signature)) throw new InvalidDataException("Expected a little-endian MIG.00.1 PSP GIM container.");
            int rootEnd = BlockEnd(data, 16, data.Length, 2);
            if (rootEnd != data.Length) throw new InvalidDataException("GIM root must bound the entire file.");
            int rootChild = checked(16 + (int)PspTextureCodec.Read(data, 24, 4));
            if (rootChild < 32 || rootChild > rootEnd) throw new InvalidDataException("Invalid GIM root child offset.");
            int picture = -1;
            int found = 0;
            for (int block = rootChild; block < rootEnd;) {
                int end = BlockEnd(data, block, rootEnd, -1);
                if (PspTextureCodec.Read(data, block, 2) == 3 && found++ == pictureIndex) picture = block;
                block = end;
            }
            if (picture < 0) throw new InvalidDataException("Requested GIM picture does not exist.");
            int pictureEnd = BlockEnd(data, picture, rootEnd, 3);
            int child = checked(picture + (int)PspTextureCodec.Read(data, picture + 8, 4));
            if (child < picture + 16 || child > pictureEnd) throw new InvalidDataException("Invalid GIM picture child offset.");
            int image = -1, palette = -1;
            for (int block = child; block < pictureEnd;) {
                int end = BlockEnd(data, block, pictureEnd, -1);
                uint type = PspTextureCodec.Read(data, block, 2);
                if (type == 4) {
                    if (image != -1) throw new InvalidDataException("Multiple image blocks in one GIM picture are not supported.");
                    image = block;
                } else if (type == 5) {
                    if (palette != -1) throw new InvalidDataException("Multiple palette blocks in one GIM picture are not supported.");
                    palette = block;
                } else if (type != 6) throw new InvalidDataException("Unsupported GIM picture block.");
                uint next = PspTextureCodec.Read(data, block + 8, 4);
                if (next != end - block) throw new InvalidDataException("GIM leaf next offset must match its block extent.");
                block = end;
            }
            if (image < 0) throw new InvalidDataException("GIM picture has no image block.");
            PspGimImageInfo info = ReadImageInfo(data, image, pictureEnd, false);
            byte[] palettePixels = null;
            int paletteCode = 0;
            if (info.Code >= 4 && info.Code <= 7) {
                if (palette < 0) throw new InvalidDataException("Indexed GIM image requires a palette block.");
                PspGimImageInfo paletteInfo = ReadImageInfo(data, palette, pictureEnd, true);
                paletteCode = paletteInfo.Code;
                byte[] decodedPalette = PspTextureCodec.DecodePixels(data, paletteInfo.PixelOffset, null, paletteCode, 0,
                    paletteInfo.Width, paletteInfo.Height, paletteInfo.Pitch, paletteInfo.StorageHeight, paletteInfo.Swizzled);
                palettePixels = new byte[decodedPalette.Length / 4 * (paletteCode == 3 ? 4 : 2)];
                for (int entry = 0; entry < decodedPalette.Length / 4; entry++) PspTextureCodec.Write(palettePixels, entry * (paletteCode == 3 ? 4 : 2), PspTextureCodec.Pack(decodedPalette, entry * 4, paletteCode, TextureAssetAlphaPrecision.A8), paletteCode == 3 ? 4 : 2);
            }
            return new TextureAsset { Width = (ushort)info.Width, Height = (ushort)info.Height, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = PspTextureCodec.DecodePixels(data, info.PixelOffset, palettePixels,
                    info.Code, paletteCode, info.Width, info.Height, info.Pitch, info.StorageHeight, info.Swizzled) };
        }
        /// <summary>Validates a monotonic block extent within its owning container.</summary>
        static int BlockEnd(byte[] data, int offset, int parentEnd, int expectedType) {
            if (offset < 16 || parentEnd - offset < 16) throw new InvalidDataException("Truncated GIM block header.");
            uint size = PspTextureCodec.Read(data, offset + 4, 4);
            uint start = PspTextureCodec.Read(data, offset + 12, 4);
            if (size < 16 || size > parentEnd - offset || start < 16 || start > size || expectedType >= 0 && PspTextureCodec.Read(data, offset, 2) != expectedType) throw new InvalidDataException("Invalid GIM block type, length or data offset.");
            return offset + (int)size;
        }
        /// <summary>Validates all frame/mip offsets, then resolves the first frame's base level and bounded native storage.</summary>
        static PspGimImageInfo ReadImageInfo(byte[] data, int block, int parentEnd, bool palette) {
            int end = BlockEnd(data, block, parentEnd, palette ? 5 : 4);
            int start = block + checked((int)PspTextureCodec.Read(data, block + 12, 4));
            if (end - start < 48) throw new InvalidDataException("Truncated GIM image metadata.");
            int header = (int)PspTextureCodec.Read(data, start, 2);
            int code = (int)PspTextureCodec.Read(data, start + 4, 2);
            int order = (int)PspTextureCodec.Read(data, start + 6, 2);
            int width = (int)PspTextureCodec.Read(data, start + 8, 2), height = (int)PspTextureCodec.Read(data, start + 10, 2);
            int pitchAlign = (int)PspTextureCodec.Read(data, start + 14, 2), heightAlign = (int)PspTextureCodec.Read(data, start + 16, 2);
            uint table = PspTextureCodec.Read(data, start + 24, 4), pixelsStart = PspTextureCodec.Read(data, start + 28, 4), pixelsEnd = PspTextureCodec.Read(data, start + 32, 4);
            int levels = (int)PspTextureCodec.Read(data, start + 42, 2), frames = (int)PspTextureCodec.Read(data, start + 46, 2);
            if (header < 48 || code > 10 || palette && code > 3 || order > 1 || width < 1 || height < 1 || width > 512 || height > 512
                || palette && width * height > 256 || levels < 1 || levels > 8 || frames < 1 || frames > 512
                || pitchAlign < 1 || pitchAlign > 256 || (pitchAlign & (pitchAlign - 1)) != 0 || heightAlign < 1 || heightAlign > 512 || (heightAlign & (heightAlign - 1)) != 0
                || table < header || pixelsStart < table + levels * frames * 4 || pixelsEnd < pixelsStart || pixelsEnd > end - start) throw new InvalidDataException("Invalid GIM image dimensions, format, alignment or index table.");
            for (int index = 0; index < levels * frames; index++) {
                uint entry = PspTextureCodec.Read(data, start + (int)table + index * 4, 4);
                if (entry < pixelsStart || entry >= pixelsEnd) throw new InvalidDataException("GIM frame index points outside pixel storage.");
            }
            int pixelOffset = start + (int)PspTextureCodec.Read(data, start + (int)table, 4);
            int bits = new PspTextureFormat(string.Empty, code, false, 0).BitsPerPixel;
            int pitch = (((width * bits + 7) / 8 + pitchAlign - 1) / pitchAlign) * pitchAlign;
            int storageHeight = ((height + heightAlign - 1) / heightAlign) * heightAlign;
            if (order == 1) {
                pitch = (pitch + 15) / 16 * 16;
                storageHeight = (storageHeight + 7) / 8 * 8;
            }
            if (code >= 8) {
                if (order != 0) throw new InvalidDataException("GIM DXT images must use normal block order.");
                pitch = Math.Max(pitch * 4, (width + 3) / 4 * (code == 8 ? 8 : 16));
                storageHeight = (storageHeight + 3) / 4 * 4;
            }
            int required = code >= 8 ? pitch * (storageHeight / 4) : pitch * storageHeight;
            if (required > start + (int)pixelsEnd - pixelOffset) throw new InvalidDataException("GIM base level pixels are truncated.");
            return new PspGimImageInfo(code, width, height, pitch, storageHeight, order == 1, pixelOffset);
        }
    }
}
