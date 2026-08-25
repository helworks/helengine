using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Serializes and validates the five-field canonical scene asset reference JSON contract.
    /// </summary>
    public sealed class SceneAssetReferenceJsonConverter : JsonConverter<SceneAssetReference> {
        /// <summary>Reads one canonical reference from JSON.</summary>
        public override SceneAssetReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("sourceKind", out JsonElement sourceKind) ||
                !root.TryGetProperty("relativePath", out JsonElement relativePath) ||
                !root.TryGetProperty("providerId", out JsonElement providerId) ||
                !root.TryGetProperty("assetId", out JsonElement assetId) ||
                !root.TryGetProperty("contentHash", out JsonElement contentHash)) {
                throw new JsonException("Canonical asset references require sourceKind, relativePath, providerId, assetId, and contentHash.");
            }
            return global::helengine.SceneAssetReferenceFactory.Rehydrate(
                (SceneAssetReferenceSourceKind)sourceKind.GetInt32(),
                relativePath.GetString() ?? string.Empty,
                providerId.GetString() ?? string.Empty,
                assetId.GetString() ?? string.Empty,
                contentHash.GetString() ?? string.Empty);
        }

        /// <summary>Writes one canonical reference to JSON.</summary>
        public override void Write(Utf8JsonWriter writer, SceneAssetReference value, JsonSerializerOptions options) {
            if (value == null) {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStartObject();
            writer.WriteNumber("sourceKind", (int)value.SourceKind);
            writer.WriteString("relativePath", value.RelativePath);
            writer.WriteString("providerId", value.ProviderId);
            writer.WriteString("assetId", value.AssetId);
            writer.WriteString("contentHash", value.ContentHash);
            writer.WriteEndObject();
        }
    }
}
