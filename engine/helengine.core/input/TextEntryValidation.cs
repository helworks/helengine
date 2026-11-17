namespace helengine {
    /// <summary>Validates complete bounded native payloads before state changes, preserving UTF-16 scalar boundaries.</summary>
    public static class TextEntryValidation {
        /// <summary>Maximum complete input shared by the portable edit state, native adapter and shaped text service.</summary>
        public const int MaximumLength = 16384;
        /// <summary>Rejects null, oversized or malformed UTF-16 payloads without truncating an active composition.</summary>
        public static void ValidateText(string text) {
            if (text == null) { throw new ArgumentNullException(nameof(text)); }
            if (text.Length > MaximumLength) { throw new ArgumentOutOfRangeException(nameof(text)); }
            for (int index = 0; index < text.Length; index++) {
                int unit = text[index];
                if (unit >= 0xd800 && unit <= 0xdbff) {
                    if (++index >= text.Length || text[index] < 0xdc00 || text[index] > 0xdfff) { throw new ArgumentException("Native text contains an incomplete UTF-16 surrogate pair."); }
                } else if (unit >= 0xdc00 && unit <= 0xdfff) { throw new ArgumentException("Native text contains an unpaired UTF-16 surrogate."); }
            }
        }
        /// <summary>Clamps a selection endpoint into the payload and moves surrogate-interior positions to their scalar's start.</summary>
        public static int ClampBoundary(string text, int index) {
            int result = Math.Max(0, Math.Min(index, text.Length));
            if (result > 0 && result < text.Length && text[result] >= 0xdc00 && text[result] <= 0xdfff && text[result - 1] >= 0xd800 && text[result - 1] <= 0xdbff) { result--; }
            return result;
        }
    }
}
