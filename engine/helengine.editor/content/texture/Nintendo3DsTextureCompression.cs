namespace helengine.editor {
    /// <summary>Decodes bounded Nintendo compression streams used by tex3ds, including its default automatic compression.</summary>
    internal static class Nintendo3DsTextureCompression {
        /// <summary>Decodes exactly the declared texture/mipmap budget and rejects truncated or overlong references.</summary>
        public static byte[] Decode(byte[] data, int start, int expectedBytes) {
            int position = start;
            int type = ReadByte(data, ref position);
            int size;
            if ((type & 128) == 0) size = ReadByte(data, ref position) | ReadByte(data, ref position) << 8 | ReadByte(data, ref position) << 16;
            else {
                if (data.Length - position < 7) throw new InvalidDataException("Truncated extended Nintendo compression header.");
                uint wide = Nintendo3DsTextureCodec.Read(data, position); position += 4;
                if (wide > int.MaxValue || ReadByte(data, ref position) != 0 || ReadByte(data, ref position) != 0 || ReadByte(data, ref position) != 0) throw new InvalidDataException("Invalid extended Nintendo compression header.");
                size = (int)wide; type &= 127;
            }
            if (size != expectedBytes || size < 1 || size > 6 * 1024 * 1024) throw new InvalidDataException("Nintendo compression size does not match the texture allocation.");
            byte[] output = new byte[size];
            if (type == 0) {
                for (int index = 0; index < size; index++) output[index] = (byte)ReadByte(data, ref position);
            } else if (type == 0x10 || type == 0x11) DecodeLz(data, ref position, output, type);
            else if (type == 0x30) DecodeRle(data, ref position, output);
            else if (type == 0x28 || type == 0x24) DecodeHuffman(data, ref position, output, type & 15);
            else throw new InvalidDataException("Unsupported Nintendo texture compression type.");
            if (data.Length - position > 3) throw new InvalidDataException("Unexpected trailing Nintendo compression data.");
            while (position < data.Length) {
                if (ReadByte(data, ref position) != 0) throw new InvalidDataException("Nonzero Nintendo stream padding.");
            }
            return output;
        }
        /// <summary>Decodes overlapping LZ10/LZ11 references after checking their history and output bounds.</summary>
        static void DecodeLz(byte[] data, ref int position, byte[] output, int type) {
            int target = 0;
            while (target < output.Length) {
                int flags = ReadByte(data, ref position);
                for (int bit = 7; bit >= 0 && target < output.Length; bit--) {
                    if ((flags & (1 << bit)) == 0) { output[target++] = (byte)ReadByte(data, ref position); continue; }
                    int first = ReadByte(data, ref position), second = ReadByte(data, ref position);
                    int length = (first >> 4) + (type == 0x10 ? 3 : 1);
                    int distance = ((first & 15) << 8 | second) + 1;
                    if (type == 0x11 && first >> 4 == 0) {
                        int third = ReadByte(data, ref position);
                        length = ((first & 15) << 4 | second >> 4) + 0x11;
                        distance = ((second & 15) << 8 | third) + 1;
                    } else if (type == 0x11 && first >> 4 == 1) {
                        int third = ReadByte(data, ref position), fourth = ReadByte(data, ref position);
                        length = ((first & 15) << 12 | second << 4 | third >> 4) + 0x111;
                        distance = ((third & 15) << 8 | fourth) + 1;
                    }
                    if (distance > target || length > output.Length - target) throw new InvalidDataException("Invalid Nintendo LZ history or run length.");
                    for (int index = 0; index < length; index++) { output[target] = output[target - distance]; target++; }
                }
            }
        }
        /// <summary>Decodes literal and repeated RLE runs with exact output accounting.</summary>
        static void DecodeRle(byte[] data, ref int position, byte[] output) {
            int target = 0;
            while (target < output.Length) {
                int control = ReadByte(data, ref position);
                bool repeated = (control & 128) != 0;
                int count = (control & 127) + (repeated ? 3 : 1);
                if (count > output.Length - target) throw new InvalidDataException("Nintendo RLE run exceeds texture bounds.");
                int value = repeated ? ReadByte(data, ref position) : 0;
                for (int index = 0; index < count; index++) output[target++] = (byte)(repeated ? value : ReadByte(data, ref position));
            }
        }
        /// <summary>Traverses the compact Nintendo Huffman tree using MSB-first bits in little-endian 32-bit words.</summary>
        static void DecodeHuffman(byte[] data, ref int position, byte[] output, int bits) {
            int tree = position;
            int treeBytes = (ReadByte(data, ref position) + 1) * 2;
            if (data.Length - tree < treeBytes) throw new InvalidDataException("Truncated Nintendo Huffman tree.");
            position = tree + treeBytes;
            int node = 1, target = 0, symbols = 0, remaining = 0;
            uint word = 0;
            while (target < output.Length) {
                if (remaining == 0) { word = Nintendo3DsTextureCodec.Read(data, position); position += 4; remaining = 32; }
                remaining--;
                int branch = (int)(word >> remaining & 1);
                if (node < 1 || node >= treeBytes) throw new InvalidDataException("Invalid Nintendo Huffman node.");
                int descriptor = data[tree + node];
                int child = (node & ~1) + ((descriptor & 63) + 1) * 2 + branch;
                if (child >= treeBytes) throw new InvalidDataException("Nintendo Huffman child exceeds its tree.");
                if ((descriptor & (branch == 0 ? 128 : 64)) == 0) { node = child; continue; }
                int value = data[tree + child];
                if (bits == 4) {
                    if (value > 15) throw new InvalidDataException("Invalid four-bit Huffman symbol.");
                    output[target] |= (byte)(value << (symbols % 2 * 4)); symbols++;
                    if (symbols % 2 == 0) target++;
                } else output[target++] = (byte)value;
                node = 1;
            }
        }
        /// <summary>Reads one bounded byte while updating the input position.</summary>
        static int ReadByte(byte[] data, ref int position) {
            if (position < 0 || position >= data.Length) throw new InvalidDataException("Truncated Nintendo compression stream.");
            return data[position++];
        }
    }
}
