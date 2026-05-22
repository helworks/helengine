using System.Security.Cryptography;
using System.Text;

namespace helengine {
    /// <summary>
    /// Generates deterministic numeric runtime asset identifiers from canonical string keys.
    /// </summary>
    public static class RuntimeAssetIdGenerator {
        /// <summary>
        /// Generates one deterministic non-zero runtime asset id from the supplied canonical key.
        /// </summary>
        /// <param name="canonicalKey">Canonical string identity for the cooked asset or packaged subresource.</param>
        /// <returns>Deterministic non-zero runtime asset id.</returns>
        public static ulong Generate(string canonicalKey) {
            if (string.IsNullOrWhiteSpace(canonicalKey)) {
                throw new ArgumentException("Canonical asset key must be provided.", nameof(canonicalKey));
            }

            string normalized = NormalizeCanonicalKey(canonicalKey);
            byte[] keyBytes = Encoding.UTF8.GetBytes(normalized);
            byte[] hash = SHA256.HashData(keyBytes);
            ulong runtimeAssetId = ReadUInt64LittleEndian(hash, 0);
            return runtimeAssetId == 0ul ? 1ul : runtimeAssetId;
        }

        /// <summary>
        /// Normalizes one canonical asset key so runtime id generation treats slash direction and character casing consistently.
        /// </summary>
        /// <param name="canonicalKey">Canonical asset key to normalize.</param>
        /// <returns>Lower-cased key that always uses forward slashes.</returns>
        static string NormalizeCanonicalKey(string canonicalKey) {
            if (canonicalKey == null) {
                throw new ArgumentNullException(nameof(canonicalKey));
            }

            char[] characters = canonicalKey.ToCharArray();
            for (int index = 0; index < characters.Length; index++) {
                char currentCharacter = characters[index];
                if (currentCharacter == '\\') {
                    characters[index] = '/';
                } else {
                    characters[index] = char.ToLowerInvariant(currentCharacter);
                }
            }

            return new string(characters);
        }

        /// <summary>
        /// Reads one unsigned 64-bit integer from a byte buffer using little-endian ordering.
        /// </summary>
        /// <param name="buffer">Byte buffer containing the value.</param>
        /// <param name="offset">Start position of the value within the buffer.</param>
        /// <returns>Decoded unsigned 64-bit integer.</returns>
        static ulong ReadUInt64LittleEndian(byte[] buffer, int offset) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            } else if (offset < 0 || offset > buffer.Length - 8) {
                throw new ArgumentOutOfRangeException(nameof(offset), "The runtime asset id requires eight bytes starting at the supplied offset.");
            }

            ulong value0 = buffer[offset];
            ulong value1 = (ulong)buffer[offset + 1] << 8;
            ulong value2 = (ulong)buffer[offset + 2] << 16;
            ulong value3 = (ulong)buffer[offset + 3] << 24;
            ulong value4 = (ulong)buffer[offset + 4] << 32;
            ulong value5 = (ulong)buffer[offset + 5] << 40;
            ulong value6 = (ulong)buffer[offset + 6] << 48;
            ulong value7 = (ulong)buffer[offset + 7] << 56;
            return value0 | value1 | value2 | value3 | value4 | value5 | value6 | value7;
        }
    }
}
