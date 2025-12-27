using System.Text.Json;

namespace helshader {
    /// <summary>
    /// Loads and validates shader manifest files.
    /// </summary>
    public class ShaderManifestLoader {
        /// <summary>
        /// Loads the manifest from disk and validates its contents.
        /// </summary>
        /// <param name="manifestPath">Absolute path to the manifest file.</param>
        /// <returns>Validated shader manifest.</returns>
        public ShaderManifest Load(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Manifest path must be provided.", nameof(manifestPath));
            }

            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException("Manifest file was not found.", manifestPath);
            }

            string json = File.ReadAllText(manifestPath);
            JsonSerializerOptions options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            ShaderManifest manifest = JsonSerializer.Deserialize<ShaderManifest>(json, options);
            if (manifest == null) {
                throw new InvalidOperationException("Manifest could not be parsed.");
            }

            ShaderManifestValidator validator = new ShaderManifestValidator();
            validator.Validate(manifest);

            return manifest;
        }
    }
}
