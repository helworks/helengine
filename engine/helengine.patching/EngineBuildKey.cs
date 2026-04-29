using System.Security.Cryptography;
using System.Text;

namespace helengine.patching {
    /// <summary>
    /// Computes stable build identifiers for patch build plans.
    /// </summary>
    public sealed class EngineBuildKey {
        /// <summary>
        /// Computes a build identifier based on inputs and source file metadata.
        /// </summary>
        /// <param name="configuration">Build configuration name.</param>
        /// <param name="patches">Resolved patches.</param>
        /// <param name="sourceFiles">Source files included in the build.</param>
        /// <param name="defines">Compilation defines.</param>
        /// <returns>Lowercase hex build identifier.</returns>
        public string ComputeBuildId(
            string configuration,
            IReadOnlyList<EnginePatchDefinition> patches,
            IReadOnlyList<string> sourceFiles,
            IReadOnlyList<string> defines) {
            string config = configuration ?? string.Empty;
            var sb = new StringBuilder();
            sb.Append(config);

            if (patches != null) {
                for (int i = 0; i < patches.Count; i++) {
                    EnginePatchDefinition patch = patches[i];
                    sb.Append("|patch:");
                    sb.Append(patch.Id);
                    sb.Append("@");
                    sb.Append(patch.Version);
                }
            }

            if (defines != null) {
                for (int i = 0; i < defines.Count; i++) {
                    sb.Append("|define:");
                    sb.Append(defines[i]);
                }
            }

            if (sourceFiles != null) {
                for (int i = 0; i < sourceFiles.Count; i++) {
                    string path = sourceFiles[i];
                    sb.Append("|src:");
                    sb.Append(path);
                    if (File.Exists(path)) {
                        sb.Append("|ts:");
                        sb.Append(File.GetLastWriteTimeUtc(path).Ticks);
                        sb.Append("|len:");
                        sb.Append(new FileInfo(path).Length);
                    }
                }
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
