using System.Text.Json;

namespace helengine.patching {
    /// <summary>
    /// Loads and validates patch manifests from disk.
    /// </summary>
    public sealed class EnginePatchManifestLoader {
        /// <summary>
        /// Gets the serializer options used for patch manifests.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Loads a patch manifest from a JSON file.
        /// </summary>
        /// <param name="manifestPath">Path to the manifest JSON file.</param>
        /// <returns>Loaded and normalized manifest.</returns>
        public EnginePatchManifest Load(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
            }

            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException("Patch manifest not found.", manifestPath);
            }

            string json = File.ReadAllText(manifestPath);
            EnginePatchManifest manifest = JsonSerializer.Deserialize<EnginePatchManifest>(json, SerializerOptions)
                ?? throw new InvalidDataException("Patch manifest could not be parsed.");

            manifest.Normalize();
            return manifest;
        }
    }
}
