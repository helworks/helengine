using helengine;

namespace helshader {
    /// <summary>
    /// Resolves shader profiles from manifest data.
    /// </summary>
    public class ShaderProfileResolver {
        /// <summary>
        /// Resolves the shader profile for the requested target and stage.
        /// </summary>
        /// <param name="manifest">Shader manifest.</param>
        /// <param name="target">Target backend.</param>
        /// <param name="stage">Shader stage.</param>
        /// <returns>Resolved profile string.</returns>
        public string ResolveProfile(ShaderManifest manifest, string target, ShaderStage stage) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            ShaderManifestProfile profile;
            if (!manifest.Profiles.TryGetValue(target, out profile)) {
                throw new InvalidOperationException($"No profile mapping found for target '{target}'.");
            }

            string resolved = ResolveStageProfile(profile, stage);
            if (string.IsNullOrWhiteSpace(resolved)) {
                throw new InvalidOperationException($"Profile for stage '{stage}' is not defined for target '{target}'.");
            }

            return resolved;
        }

        /// <summary>
        /// Resolves the profile string for a given stage.
        /// </summary>
        /// <param name="profile">Profile mapping.</param>
        /// <param name="stage">Shader stage.</param>
        /// <returns>Profile string.</returns>
        string ResolveStageProfile(ShaderManifestProfile profile, ShaderStage stage) {
            if (profile == null) {
                throw new ArgumentNullException(nameof(profile));
            }

            if (stage == ShaderStage.Vertex) {
                return profile.Vertex;
            }

            if (stage == ShaderStage.Pixel) {
                return profile.Pixel;
            }

            if (stage == ShaderStage.Geometry) {
                return profile.Geometry;
            }

            if (stage == ShaderStage.Hull) {
                return profile.Hull;
            }

            if (stage == ShaderStage.Domain) {
                return profile.Domain;
            }

            if (stage == ShaderStage.Compute) {
                return profile.Compute;
            }

            throw new InvalidOperationException($"Unsupported shader stage '{stage}'.");
        }
    }
}
