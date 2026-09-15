using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {

    /// <summary>
    /// Builds deterministic content keys for cooked asset nodes.
    /// </summary>
    public static class EditorAssetCookKeyBuilder {
        /// <summary>
        /// Builds a SHA-256 key from source, target, settings and dependency identity.
        /// </summary>
        /// <param name="node">Source node.</param>
        /// <param name="engineVersion">Exact engine version.</param>
        /// <param name="platformId">Target platform.</param>
        /// <param name="profileId">Target profile.</param>
        /// <param name="settings">Relevant normalized settings.</param>
        /// <param name="dependencyKeys">Resolved dependency keys.</param>
        /// <returns>Deterministic cook key.</returns>
        public static EditorAssetCookNodeKey Build(
            EditorAssetCookNode node,
            string engineVersion,
            string platformId,
            string profileId,
            IReadOnlyDictionary<string, string> settings,
            IEnumerable<EditorAssetCookNodeKey> dependencyKeys) {
            if (node == null) {
                throw new ArgumentNullException(nameof(node));
            }
            if (string.IsNullOrWhiteSpace(engineVersion)) {
                throw new ArgumentException("Engine version must be provided.", nameof(engineVersion));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (string.IsNullOrWhiteSpace(profileId)) {
                throw new ArgumentException("Profile id must be provided.", nameof(profileId));
            }
            if (dependencyKeys == null) {
                throw new ArgumentNullException(nameof(dependencyKeys));
            }

            StringBuilder material = new();
            material.AppendLine("helengine-cook-key-v1");
            material.AppendLine(engineVersion);
            material.AppendLine(platformId);
            material.AppendLine(profileId);
            material.AppendLine(node.ProcessorId);
            material.AppendLine(node.SourceKind.ToString());
            material.AppendLine(node.SourceReference.SourceKind.ToString());
            material.AppendLine(node.SourceReference.RelativePath);
            material.AppendLine(node.SourceReference.ProviderId);
            material.AppendLine(node.SourceReference.AssetId);
            material.AppendLine(node.SourceReference.ContentHash);

            if (settings != null) {
                foreach (KeyValuePair<string, string> setting in settings.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
                    material.Append("setting:").Append(setting.Key).Append('=').AppendLine(setting.Value ?? string.Empty);
                }
            }

            foreach (EditorAssetCookNodeKey dependencyKey in dependencyKeys.OrderBy(key => key.Value, StringComparer.Ordinal)) {
                if (dependencyKey == null) {
                    throw new ArgumentException("Dependency keys cannot contain null values.", nameof(dependencyKeys));
                }
                material.Append("dependency:").AppendLine(dependencyKey.Value);
            }

            using SHA256 sha256 = SHA256.Create();
            return new EditorAssetCookNodeKey("sha256:" + Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(material.ToString()))).ToLowerInvariant());
        }
    }
}
