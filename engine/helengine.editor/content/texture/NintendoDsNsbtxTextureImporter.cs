namespace helengine.editor {
    /// <summary>Imports Nitro NSBTX textures in all seven native DS formats with explicit texture/palette selection.</summary>
    public sealed class NintendoDsNsbtxTextureImporter : ITextureImporter {
        /// <summary>Caps buffered source containers independently of validated GPU allocation limits.</summary>
        const int MaximumBytes = 16 * 1024 * 1024;
        /// <summary>Imports the first texture, using a matching palette name or the only available palette.</summary>
        public TextureAsset ImportTexture(Stream stream) => ImportTexture(stream, 0, -1);
        /// <summary>Imports a selected texture; an explicit palette index resolves containers without matching palette names.</summary>
        public TextureAsset ImportTexture(Stream stream, int textureIndex, int paletteIndex) {
            using TextureAsset native = ImportNativeTexture(stream, textureIndex, paletteIndex);
            return new TextureAsset { Id = native.Id, Width = native.Width, Height = native.Height, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = NintendoDsTextureCodec.Decode(native) };
        }
        /// <summary>Returns independently owned native GPU storage, preserving compressed blocks from NSBTX without recompression.</summary>
        public TextureAsset ImportNativeTexture(Stream stream, int textureIndex = 0, int paletteIndex = -1) {
            if (stream == null || !stream.CanRead) throw new ArgumentException("A readable NSBTX stream is required.");
            if (textureIndex < 0 || paletteIndex < -1) throw new ArgumentOutOfRangeException(nameof(textureIndex));
            using MemoryStream buffer = new MemoryStream();
            byte[] chunk = new byte[8192]; int count;
            while ((count = stream.Read(chunk, 0, chunk.Length)) != 0) {
                if (buffer.Length + count > MaximumBytes) throw new InvalidDataException("NSBTX container exceeds the import limit.");
                buffer.Write(chunk, 0, count);
            }
            byte[] file = buffer.ToArray();
            if (file.Length < 20 || NintendoDsTextureCodec.Read(file, 0) != 0x30585442 || NintendoDsTextureCodec.Word(file, 4) != 0xfeff
                || NintendoDsTextureCodec.Word(file, 6) != 1 || NintendoDsTextureCodec.Read(file, 8) != file.Length
                || NintendoDsTextureCodec.Word(file, 12) != 16 || NintendoDsTextureCodec.Word(file, 14) != 1) throw new InvalidDataException("Invalid NSBTX file header.");
            uint offset = NintendoDsTextureCodec.Read(file, 16);
            if (offset < 20 || offset > file.Length - 60) throw new InvalidDataException("Invalid NSBTX TEX0 offset.");
            int section = (int)offset;
            uint sectionBytes = NintendoDsTextureCodec.Read(file, section + 4);
            if (NintendoDsTextureCodec.Read(file, section) != 0x30584554 || sectionBytes < 60 || sectionBytes > file.Length - section) throw new InvalidDataException("Invalid NSBTX TEX0 section.");
            byte[] tex = new byte[(int)sectionBytes]; Array.Copy(file, section, tex, 0, tex.Length);
            int dictionary = NintendoDsTextureCodec.Word(tex, 14);
            int entry = Entry(tex, dictionary, textureIndex, 8);
            string name = Name(tex, dictionary, textureIndex, 8);
            int address = NintendoDsTextureCodec.Word(tex, entry) * 8, parameters = NintendoDsTextureCodec.Word(tex, entry + 2);
            int code = parameters >> 10 & 7, width = 8 << (parameters >> 4 & 7), height = 8 << (parameters >> 7 & 7);
            if (code == 0) throw new InvalidDataException("NSBTX no-texture records cannot be imported as images.");
            NintendoDsTextureFormat format = NintendoDsTextureFormatCatalog.GetFormat(code);
            NintendoDsTextureLayout layout = new NintendoDsTextureLayout(format, width, height);
            bool transparent = (parameters & 8192) != 0 && code >= 2 && code <= 4;
            byte[] payload = new byte[layout.TexelBytes + layout.DescriptorBytes];
            int storage = code == 5 ? 36 : 20, length = NintendoDsTextureCodec.Word(tex, code == 5 ? 28 : 12) * 8;
            int baseOffset = Offset(tex, storage, length);
            if (address > length || layout.TexelBytes > length - address) throw new InvalidDataException("NSBTX texture exceeds its native data region.");
            Array.Copy(tex, baseOffset + address, payload, 0, layout.TexelBytes);
            if (code == 5) {
                int descriptorBase = Offset(tex, 40, length / 2);
                if (address / 2 + layout.DescriptorBytes > length / 2) throw new InvalidDataException("NSBTX compressed descriptors exceed their region.");
                Array.Copy(tex, descriptorBase + address / 2, payload, layout.TexelBytes, layout.DescriptorBytes);
            }
            byte[] palette = new byte[0];
            if (code != 7) {
                int paletteDictionary = Offset(tex, 52, 4);
                int paletteCount = DictionaryCount(tex, paletteDictionary, 4);
                if (paletteIndex < 0) {
                    for (int index = 0; index < paletteCount; index++) if (Name(tex, paletteDictionary, index, 4) == name) paletteIndex = index;
                    if (paletteIndex < 0 && paletteCount == 1) paletteIndex = 0;
                    if (paletteIndex < 0) throw new InvalidDataException("NSBTX palette association is ambiguous; select a palette index.");
                }
                int paletteEntry = Entry(tex, paletteDictionary, paletteIndex, 4);
                int paletteAddress = NintendoDsTextureCodec.Word(tex, paletteEntry) * 8;
                int paletteLength = NintendoDsTextureCodec.Word(tex, 48) * 8;
                int paletteBase = Offset(tex, 56, paletteLength), end = paletteLength;
                for (int index = 0; index < paletteCount; index++) {
                    int next = NintendoDsTextureCodec.Word(tex, Entry(tex, paletteDictionary, index, 4)) * 8;
                    if (next > paletteLength) throw new InvalidDataException("NSBTX palette offset exceeds its region.");
                    if (next > paletteAddress) end = Math.Min(end, next);
                }
                if (paletteAddress >= end) throw new InvalidDataException("Empty NSBTX palette.");
                int bytes = code == 5 ? Math.Min(65536, end - paletteAddress) : Math.Min(format.PaletteEntries * 2, end - paletteAddress);
                palette = new byte[bytes]; Array.Copy(tex, paletteBase + paletteAddress, palette, 0, bytes);
            }
            TextureAsset owned = NintendoDsTextureCodec.CreateNative(width, height, code, format.Alpha, transparent, payload, palette);
            owned.Id = name; return owned;
        }
        /// <summary>Resolves a bounded TEX0-relative region from its little-endian 32-bit offset.</summary>
        static int Offset(byte[] data, int field, int length) {
            uint offset = NintendoDsTextureCodec.Read(data, field);
            if (offset > data.Length || length < 0 || length > data.Length - offset) throw new InvalidDataException("NSBTX data region exceeds TEX0.");
            return (int)offset;
        }
        /// <summary>Checks the complete dictionary entry/name arrays and returns the record count.</summary>
        static int DictionaryCount(byte[] data, int offset, int stride) {
            if (offset < 60 || data.Length - offset < 16) throw new InvalidDataException("Truncated NSBTX dictionary.");
            int count = data[offset + 1], bytes = NintendoDsTextureCodec.Word(data, offset + 2);
            int entries = NintendoDsTextureCodec.Word(data, offset + 6);
            if (data[offset] != 0 || count < 1 || bytes > data.Length - offset || bytes < 16 || entries < 12 || entries > bytes - 4
                || NintendoDsTextureCodec.Word(data, offset + entries) != stride) throw new InvalidDataException("Invalid NSBTX dictionary layout.");
            int names = NintendoDsTextureCodec.Word(data, offset + entries + 2);
            if (entries + 4 + count * stride > bytes || names < 4 + count * stride || entries + names + count * 16 > bytes) throw new InvalidDataException("Truncated NSBTX entries or names.");
            return count;
        }
        /// <summary>Gets the checked byte address of one Nitro dictionary record.</summary>
        static int Entry(byte[] data, int dictionary, int index, int stride) {
            int count = DictionaryCount(data, dictionary, stride);
            if (index < 0 || index >= count) throw new InvalidDataException("NSBTX selection index exceeds its dictionary.");
            return dictionary + NintendoDsTextureCodec.Word(data, dictionary + 6) + 4 + index * stride;
        }
        /// <summary>Reads the fixed sixteen-byte dictionary name using Nitro's byte-preserving Latin-1 encoding.</summary>
        static string Name(byte[] data, int dictionary, int index, int stride) {
            Entry(data, dictionary, index, stride);
            int entries = dictionary + NintendoDsTextureCodec.Word(data, dictionary + 6);
            int start = entries + NintendoDsTextureCodec.Word(data, entries + 2) + index * 16, length = 0;
            while (length < 16 && data[start + length] != 0) length++;
            return System.Text.Encoding.Latin1.GetString(data, start, length);
        }
    }
}
