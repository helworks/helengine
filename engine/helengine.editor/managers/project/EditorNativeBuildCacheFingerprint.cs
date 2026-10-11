namespace helengine.editor {
    /// <summary>
    /// Encodes complete SHA-256 cache identities as compact directory names that remain distinct on case-insensitive filesystems.
    /// </summary>
    internal static class EditorNativeBuildCacheFingerprint {
        /// <summary>Lowercase Base32 alphabet containing only portable filename characters.</summary>
        const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

        /// <summary>Required number of bytes in a complete SHA-256 fingerprint.</summary>
        const int DigestByteCount = 32;

        /// <summary>Characters needed to retain all 256 digest bits without Base32 padding.</summary>
        const int EncodedCharacterCount = 52;

        /// <summary>
        /// Encodes every digest bit without truncation, saving twelve characters compared with hexadecimal cache directories.
        /// Lowercase output prevents case folding from merging distinct cache identities on Windows.
        /// </summary>
        /// <param name="digest">Complete SHA-256 digest identifying the native build inputs.</param>
        /// <returns>One deterministic, lowercase, unpadded Base32 directory segment.</returns>
        internal static string Encode(byte[] digest) {
            if (digest == null) {
                throw new ArgumentNullException(nameof(digest));
            } else if (digest.Length != DigestByteCount) {
                throw new ArgumentException("Native cache fingerprints must contain a complete SHA-256 digest.", nameof(digest));
            }

            char[] characters = new char[EncodedCharacterCount];
            uint buffer = 0;
            int bufferedBits = 0;
            int characterIndex = 0;
            for (int index = 0; index < digest.Length; index++) {
                buffer = (buffer << 8) | digest[index];
                bufferedBits += 8;
                while (bufferedBits >= 5) {
                    bufferedBits -= 5;
                    characters[characterIndex++] = Alphabet[(int)((buffer >> bufferedBits) & 31)];
                }
            }
            if (bufferedBits > 0) {
                characters[characterIndex] = Alphabet[(int)((buffer << (5 - bufferedBits)) & 31)];
            }
            return new string(characters);
        }
    }
}
