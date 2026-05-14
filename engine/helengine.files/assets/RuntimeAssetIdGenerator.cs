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

            string normalized = canonicalKey.Replace('\\', '/').ToLowerInvariant();
            byte[] keyBytes = Encoding.UTF8.GetBytes(normalized);
            byte[] hash = SHA256.HashData(keyBytes);
            ulong runtimeAssetId = BitConverter.ToUInt64(hash, 0);
            return runtimeAssetId == 0ul ? 1ul : runtimeAssetId;
        }
    }
}
