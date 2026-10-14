using System.Buffers.Binary;
using System.Drawing;

namespace helengine.editor {
    /// <summary>Reads a standalone TrueType/OpenType source face's style flags instead of selecting an installed family's regular face.</summary>
    internal static class GdiFontSourceStyle {
        /// <summary>Resolves bold and italic from bounded big-endian head and optional OS/2 tables, preserving regular sources.</summary>
        /// <param name="bytes">Complete source font bytes containing a standalone SFNT face.</param>
        /// <returns>The source face's regular, bold, italic or combined style.</returns>
        public static FontStyle Read(byte[] bytes) {
            if (bytes == null || bytes.Length < 12) { throw new InvalidOperationException("Font source has no complete SFNT header."); }
            uint version = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4));
            if (version != 0x00010000u && version != 0x4F54544Fu && version != 0x74727565u) {
                throw new InvalidOperationException("Font source must be a standalone TrueType or OpenType SFNT face.");
            }
            int count = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2));
            if (count < 1 || count > (bytes.Length - 12) / 16) { throw new InvalidOperationException("Font SFNT table directory is incomplete."); }
            int head = -1;
            int selection = -1;
            for (int index = 0; index < count; index++) {
                int record = 12 + index * 16;
                uint tag = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(record, 4));
                uint offset = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(record + 8, 4));
                uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(record + 12, 4));
                if ((ulong)offset + length > (ulong)bytes.Length) { throw new InvalidOperationException("Font SFNT table is outside its source bytes."); }
                if (tag == 0x68656164u) {
                    if (head >= 0 || length < 46u) { throw new InvalidOperationException("Font head style table is missing bytes or duplicated."); }
                    head = checked((int)offset + 44);
                } else if (tag == 0x4F532F32u) {
                    if (selection >= 0 || length < 64u) { throw new InvalidOperationException("Font OS/2 style table is missing bytes or duplicated."); }
                    selection = checked((int)offset + 62);
                }
            }
            if (head < 0) { throw new InvalidOperationException("Font source has no head style table."); }
            int macStyle = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(head, 2));
            int fsSelection = selection < 0 ? 0 : BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(selection, 2));
            FontStyle style = FontStyle.Regular;
            if ((macStyle & 1) != 0 || (fsSelection & 32) != 0) { style |= FontStyle.Bold; }
            if ((macStyle & 2) != 0 || (fsSelection & 1) != 0 || (fsSelection & 512) != 0) { style |= FontStyle.Italic; }
            return style;
        }
    }
}
