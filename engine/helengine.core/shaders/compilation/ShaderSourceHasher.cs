namespace helengine {
    /// <summary>
    /// Computes stable hashes for shader source text used in cache keys.
    /// </summary>
    public class ShaderSourceHasher {
        /// <summary>
        /// Computes a SHA-256 hash for the provided shader source text.
        /// </summary>
        /// <param name="source">Shader source text.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        public string ComputeHash(string source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            byte[] data = System.Text.Encoding.UTF8.GetBytes(source);
            byte[] hash = System.Security.Cryptography.SHA256.HashData(data);
            return ConvertToHex(hash);
        }

        /// <summary>
        /// Converts a byte array into a lowercase hex string.
        /// </summary>
        /// <param name="data">Hash bytes.</param>
        /// <returns>Hex-encoded string.</returns>
        string ConvertToHex(byte[] data) {
            char[] buffer = new char[data.Length * 2];
            int outputIndex = 0;
            for (int i = 0; i < data.Length; i++) {
                byte value = data[i];
                int high = value >> 4;
                int low = value & 0xF;
                buffer[outputIndex++] = ToHexChar(high);
                buffer[outputIndex++] = ToHexChar(low);
            }

            return new string(buffer);
        }

        /// <summary>
        /// Converts a 0-15 value into a lowercase hex character.
        /// </summary>
        /// <param name="value">Nibble value to convert.</param>
        /// <returns>Hex character.</returns>
        char ToHexChar(int value) {
            if (value < 10) {
                return (char)('0' + value);
            }

            return (char)('a' + (value - 10));
        }
    }
}
