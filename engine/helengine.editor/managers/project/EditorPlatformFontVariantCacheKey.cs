namespace helengine.editor {
    /// <summary>
    /// Identifies one cached editor-side font variant generated for a specific target platform and import-settings state.
    /// </summary>
    public sealed class EditorPlatformFontVariantCacheKey {
        /// <summary>
        /// Initializes one cache key for the supplied target platform and checksum inputs.
        /// </summary>
        /// <param name="targetPlatformId">Target platform identifier whose texture settings drive the cached variant.</param>
        /// <param name="sourceChecksum">Checksum of the authored source font bytes.</param>
        /// <param name="settingsChecksum">Checksum of the serialized authored import settings.</param>
        public EditorPlatformFontVariantCacheKey(string targetPlatformId, string sourceChecksum, string settingsChecksum) {
            if (string.IsNullOrWhiteSpace(targetPlatformId)) {
                throw new ArgumentException("Target platform id must be provided.", nameof(targetPlatformId));
            } else if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            } else if (string.IsNullOrWhiteSpace(settingsChecksum)) {
                throw new ArgumentException("Settings checksum must be provided.", nameof(settingsChecksum));
            }

            TargetPlatformId = targetPlatformId;
            SourceChecksum = sourceChecksum;
            SettingsChecksum = settingsChecksum;
            VariantId = BuildVariantId(targetPlatformId, sourceChecksum, settingsChecksum);
        }

        /// <summary>
        /// Gets the target platform identifier whose texture settings drive the cached variant.
        /// </summary>
        public string TargetPlatformId { get; }

        /// <summary>
        /// Gets the checksum of the authored source font bytes.
        /// </summary>
        public string SourceChecksum { get; }

        /// <summary>
        /// Gets the checksum of the serialized authored import settings.
        /// </summary>
        public string SettingsChecksum { get; }

        /// <summary>
        /// Gets the deterministic cache identifier for this source/settings/platform combination.
        /// </summary>
        public string VariantId { get; }

        /// <summary>
        /// Builds one deterministic cache identifier from the source checksum, settings checksum, and target platform.
        /// </summary>
        /// <param name="targetPlatformId">Target platform identifier whose texture settings drive the cached variant.</param>
        /// <param name="sourceChecksum">Checksum of the authored source font bytes.</param>
        /// <param name="settingsChecksum">Checksum of the serialized authored import settings.</param>
        /// <returns>Stable cache identifier for the supplied variant inputs.</returns>
        static string BuildVariantId(string targetPlatformId, string sourceChecksum, string settingsChecksum) {
            string identity = string.Concat(
                "platform-font", "\n",
                targetPlatformId, "\n",
                sourceChecksum, "\n",
                settingsChecksum);
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
