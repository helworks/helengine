using System;
using System.Text;

namespace helengine.ui.format {
    /// <summary>Strict, bounded Unicode validation before platform UTF-8 conversion.</summary>
    public static class UiUtf8 {
        /// <summary>Checks UTF-16 surrogate pairing and returns the bounded encoded byte length.</summary>
        public static int Length(string text) {
            if (text == null || text.Length > UiFormatLimits.StringBytes) { throw new FormatException("Invalid UI string length."); }
            for (int i = 0; i < text.Length; i++) {
                int c = text[i] - '\0';
                if (c >= 0xd800 && c <= 0xdbff) {
                    if (++i >= text.Length || text[i] - '\0' < 0xdc00 || text[i] - '\0' > 0xdfff) { throw new FormatException("Unpaired UI surrogate."); }
                } else if (c >= 0xdc00 && c <= 0xdfff) { throw new FormatException("Unpaired UI surrogate."); }
            }
            int length = Encoding.UTF8.GetByteCount(text);
            if (length > UiFormatLimits.StringBytes) { throw new FormatException("UI string exceeds byte limit."); }
            return length;
        }
        /// <summary>Rejects malformed, overlong, surrogate and out-of-range UTF-8 sequences.</summary>
        public static void Validate(byte[] bytes) {
            int i = 0;
            while (i < bytes.Length) {
                int first = bytes[i++];
                if (first <= 0x7f) { continue; }
                int count = first >= 0xc2 && first <= 0xdf ? 1 : first >= 0xe0 && first <= 0xef ? 2 : first >= 0xf0 && first <= 0xf4 ? 3 : -1;
                if (count < 0 || bytes.Length - i < count) { throw new FormatException("Invalid UI UTF-8."); }
                int second = bytes[i];
                if ((first == 0xe0 && second < 0xa0) || (first == 0xed && second >= 0xa0) || (first == 0xf0 && second < 0x90) || (first == 0xf4 && second >= 0x90)) { throw new FormatException("Invalid UI Unicode scalar."); }
                for (int j = 0; j < count; j++) { int next = bytes[i++]; if (next < 0x80 || next > 0xbf) { throw new FormatException("Invalid UI UTF-8 continuation."); } }
            }
        }
    }
}
