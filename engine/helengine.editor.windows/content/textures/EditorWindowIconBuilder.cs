namespace helengine.editor {
    /// <summary>
    /// Wraps a PNG in a single-entry ICO container so the native window can show the same image the editor title bar draws.
    /// </summary>
    public static class EditorWindowIconBuilder {
        /// <summary>
        /// Size in bytes of the ICONDIR header plus one ICONDIRENTRY.
        /// </summary>
        const int IcoHeaderLength = 22;
        /// <summary>
        /// Minimum PNG length that still contains the signature and the IHDR dimensions.
        /// </summary>
        const int MinimumPngLength = 24;

        /// <summary>
        /// Eight-byte PNG file signature.
        /// </summary>
        static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// Builds ICO file bytes that embed the provided PNG as the sole image entry.
        /// </summary>
        /// <param name="pngBytes">Complete PNG file contents.</param>
        /// <returns>ICO file contents suitable for constructing a native icon.</returns>
        public static byte[] BuildIconFromPng(byte[] pngBytes) {
            if (pngBytes == null) {
                throw new ArgumentNullException(nameof(pngBytes));
            }
            if (pngBytes.Length < MinimumPngLength || !HasPngSignature(pngBytes)) {
                throw new ArgumentException("Icon source must be a PNG file.", nameof(pngBytes));
            }

            int width = ReadBigEndianInt32(pngBytes, 16);
            int height = ReadBigEndianInt32(pngBytes, 20);
            if (width <= 0 || height <= 0 || width > 256 || height > 256) {
                throw new ArgumentException("Icon source PNG must be between 1 and 256 pixels on each side.", nameof(pngBytes));
            }

            byte[] ico = new byte[IcoHeaderLength + pngBytes.Length];
            WriteUInt16(ico, 0, 0);
            WriteUInt16(ico, 2, 1);
            WriteUInt16(ico, 4, 1);
            ico[6] = (byte)(width == 256 ? 0 : width);
            ico[7] = (byte)(height == 256 ? 0 : height);
            ico[8] = 0;
            ico[9] = 0;
            WriteUInt16(ico, 10, 1);
            WriteUInt16(ico, 12, 32);
            WriteUInt32(ico, 14, (uint)pngBytes.Length);
            WriteUInt32(ico, 18, IcoHeaderLength);
            Array.Copy(pngBytes, 0, ico, IcoHeaderLength, pngBytes.Length);
            return ico;
        }

        /// <summary>
        /// Checks whether the bytes begin with the PNG signature followed by an IHDR chunk.
        /// </summary>
        /// <param name="bytes">Candidate PNG bytes.</param>
        /// <returns>True when the signature and IHDR chunk type are present.</returns>
        static bool HasPngSignature(byte[] bytes) {
            for (int i = 0; i < PngSignature.Length; i++) {
                if (bytes[i] != PngSignature[i]) {
                    return false;
                }
            }

            return bytes[12] == (byte)'I' && bytes[13] == (byte)'H' && bytes[14] == (byte)'D' && bytes[15] == (byte)'R';
        }

        /// <summary>
        /// Reads a big-endian 32-bit integer.
        /// </summary>
        /// <param name="bytes">Source buffer.</param>
        /// <param name="offset">Offset of the first byte.</param>
        /// <returns>Decoded integer.</returns>
        static int ReadBigEndianInt32(byte[] bytes, int offset) {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        /// <summary>
        /// Writes a little-endian 16-bit value.
        /// </summary>
        /// <param name="bytes">Target buffer.</param>
        /// <param name="offset">Offset of the first byte.</param>
        /// <param name="value">Value to write.</param>
        static void WriteUInt16(byte[] bytes, int offset, ushort value) {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>
        /// Writes a little-endian 32-bit value.
        /// </summary>
        /// <param name="bytes">Target buffer.</param>
        /// <param name="offset">Offset of the first byte.</param>
        /// <param name="value">Value to write.</param>
        static void WriteUInt32(byte[] bytes, int offset, uint value) {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
