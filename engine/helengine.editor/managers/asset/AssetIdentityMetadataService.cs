using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads, validates, creates, and atomically saves authored asset identity sidecars.
    /// </summary>
    public sealed class AssetIdentityMetadataService {
        /// <summary>
        /// Current sidecar schema version.
        /// </summary>
        const int CurrentVersion = 1;

        /// <summary>
        /// JSON options for the stable sidecar contract.
        /// </summary>
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Returns the metadata sidecar path for one authored asset.
        /// </summary>
        /// <param name="assetPath">Absolute authored asset path.</param>
        /// <returns>Adjacent metadata sidecar path.</returns>
        public string GetMetadataPath(string assetPath) {
            ValidateAssetPath(assetPath);
            return assetPath + ".hmeta";
        }

        /// <summary>
        /// Loads and strictly validates an existing metadata sidecar.
        /// </summary>
        /// <param name="assetPath">Absolute authored asset path.</param>
        /// <returns>Validated metadata document.</returns>
        public AssetIdentityMetadataDocument Load(string assetPath) {
            ValidateAssetPath(assetPath);
            if (!File.Exists(assetPath)) {
                throw new InvalidOperationException($"Authored asset '{assetPath}' does not exist.");
            }

            string metadataPath = GetMetadataPath(assetPath);
            if (!File.Exists(metadataPath)) {
                throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' does not exist.");
            }

            try {
                string json = File.ReadAllText(metadataPath);
                using JsonDocument shape = JsonDocument.Parse(json);
                ValidateJsonShape(shape, metadataPath);
                AssetIdentityMetadataDocument document = JsonSerializer.Deserialize<AssetIdentityMetadataDocument>(json, JsonOptions);
                if (document == null) {
                    throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' is empty.");
                }
                ValidateDocument(document, metadataPath);
                return document;
            } catch (JsonException exception) {
                throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' contains malformed JSON.", exception);
            } catch (InvalidOperationException) {
                throw;
            } catch (Exception exception) {
                throw new InvalidOperationException($"Failed to read asset identity metadata '{metadataPath}'.", exception);
            }
        }

        /// <summary>
        /// Loads existing metadata or creates a new validated sidecar for an authored asset.
        /// </summary>
        /// <param name="assetPath">Absolute authored asset path.</param>
        /// <param name="requestedAssetId">Optional requested stable UUID.</param>
        /// <returns>Validated existing or newly created metadata document.</returns>
        public AssetIdentityMetadataDocument LoadOrCreate(string assetPath, string requestedAssetId) {
            ValidateAssetPath(assetPath);
            if (!File.Exists(assetPath)) {
                throw new InvalidOperationException($"Authored asset '{assetPath}' does not exist.");
            }

            string metadataPath = GetMetadataPath(assetPath);
            if (File.Exists(metadataPath)) {
                return Load(assetPath);
            }

            if (!string.IsNullOrWhiteSpace(requestedAssetId)) {
                ValidateAssetId(requestedAssetId, metadataPath, "requested asset id");
            }

            AssetIdentityMetadataDocument document = new AssetIdentityMetadataDocument {
                AssetId = string.IsNullOrWhiteSpace(requestedAssetId)
                    ? Guid.NewGuid().ToString("N")
                    : requestedAssetId
            };
            Save(assetPath, document);
            return document;
        }

        /// <summary>
        /// Strictly validates and atomically saves one metadata document beside its source asset.
        /// </summary>
        /// <param name="assetPath">Absolute authored asset path.</param>
        /// <param name="document">Metadata document to save.</param>
        public void Save(string assetPath, AssetIdentityMetadataDocument document) {
            ValidateAssetPath(assetPath);
            if (!File.Exists(assetPath)) {
                throw new InvalidOperationException($"Authored asset '{assetPath}' does not exist.");
            } else if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            string metadataPath = GetMetadataPath(assetPath);
            ValidateDocument(document, metadataPath);
            string temporaryPath = metadataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                string json = JsonSerializer.Serialize(document, JsonOptions);
                File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));
                File.Move(temporaryPath, metadataPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Validates one metadata JSON document's required property shape.
        /// </summary>
        /// <param name="shape">Parsed JSON document.</param>
        /// <param name="metadataPath">Metadata path used in diagnostics.</param>
        static void ValidateJsonShape(JsonDocument shape, string metadataPath) {
            if (shape.RootElement.ValueKind != JsonValueKind.Object ||
                !shape.RootElement.TryGetProperty("version", out JsonElement version) ||
                !shape.RootElement.TryGetProperty("assetId", out JsonElement assetId) ||
                !shape.RootElement.TryGetProperty("formerAssetIds", out JsonElement formerAssetIds) ||
                version.ValueKind != JsonValueKind.Number ||
                assetId.ValueKind != JsonValueKind.String ||
                formerAssetIds.ValueKind != JsonValueKind.Array) {
                throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' is missing required fields.");
            }
        }

        /// <summary>
        /// Validates one deserialized metadata document.
        /// </summary>
        /// <param name="document">Document to validate.</param>
        /// <param name="metadataPath">Metadata path used in diagnostics.</param>
        static void ValidateDocument(AssetIdentityMetadataDocument document, string metadataPath) {
            if (document.Version != CurrentVersion) {
                throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' has unsupported version '{document.Version}'.");
            }
            ValidateAssetId(document.AssetId, metadataPath, "current asset id");
            if (document.FormerAssetIds == null) {
                throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' has no former asset id list.");
            }

            HashSet<string> formerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < document.FormerAssetIds.Count; index++) {
                string formerAssetId = document.FormerAssetIds[index];
                ValidateAssetId(formerAssetId, metadataPath, "former asset id");
                if (string.Equals(formerAssetId, document.AssetId, StringComparison.Ordinal) || !formerIds.Add(formerAssetId)) {
                    throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' contains duplicate asset identities.");
                }
            }
        }

        /// <summary>
        /// Validates one stable UUID string.
        /// </summary>
        /// <param name="assetId">Candidate UUID.</param>
        /// <param name="metadataPath">Metadata path used in diagnostics.</param>
        /// <param name="description">Identity description used in diagnostics.</param>
        static void ValidateAssetId(string assetId, string metadataPath, string description) {
            if (string.IsNullOrWhiteSpace(assetId) || assetId.Length != 32 || !IsLowerHex(assetId)) {
                throw new InvalidOperationException($"Asset identity metadata '{metadataPath}' contains an invalid {description}.");
            }
        }

        /// <summary>
        /// Validates one source asset path.
        /// </summary>
        /// <param name="assetPath">Candidate source path.</param>
        static void ValidateAssetPath(string assetPath) {
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("An authored asset path is required.", nameof(assetPath));
            }
        }

        /// <summary>
        /// Determines whether all characters in a value are lowercase hexadecimal digits.
        /// </summary>
        /// <param name="value">Value to inspect.</param>
        /// <returns>True when all characters are lowercase hexadecimal digits.</returns>
        static bool IsLowerHex(string value) {
            for (int index = 0; index < value.Length; index++) {
                char character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }
    }
}
