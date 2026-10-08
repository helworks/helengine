using System.Text;
using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Reads and writes <see cref="VideoEdit"/> documents as <c>helengine.video.edit.v1</c> JSON: snake_case names,
    /// unknown properties and duplicate keys rejected, default values omitted. This is the interchange format the AI and
    /// the product exchange; the C# model stays the source of truth.
    /// </summary>
    public static class VideoEditJson {
        /// <summary>
        /// Schema id every edit document declares.
        /// </summary>
        public const string SchemaId = "helengine.video.edit.v1";

        /// <summary>
        /// Gets the serializer options shared by parsing, writing and schema generation.
        /// </summary>
        public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true
        };

        /// <summary>
        /// Parses one edit document.
        /// </summary>
        /// <param name="json">Document text.</param>
        /// <returns>Parsed edit.</returns>
        /// <exception cref="InvalidDataException">The text repeats a key, declares another schema or is not an edit.</exception>
        public static VideoEdit Parse(string json) {
            if (json == null) {
                throw new ArgumentNullException(nameof(json));
            }
            RejectDuplicateKeys(json);
            VideoEdit edit = JsonSerializer.Deserialize<VideoEdit>(json, Options);
            if (edit == null) {
                throw new InvalidDataException("The document is empty.");
            }
            if (edit.Schema != SchemaId) {
                throw new InvalidDataException($"Expected schema '{SchemaId}', found '{edit.Schema}'.");
            }
            return edit;
        }

        /// <summary>
        /// Writes one edit document.
        /// </summary>
        /// <param name="edit">Edit to write.</param>
        /// <returns>Indented JSON text.</returns>
        public static string Serialize(VideoEdit edit) {
            if (edit == null) {
                throw new ArgumentNullException(nameof(edit));
            }
            return JsonSerializer.Serialize(edit, Options);
        }

        /// <summary>
        /// Walks the token stream and rejects an object that names the same property twice, which the serializer would
        /// otherwise resolve silently by keeping the last value.
        /// </summary>
        /// <param name="json">Document text.</param>
        static void RejectDuplicateKeys(string json) {
            Utf8JsonReader reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
            Stack<HashSet<string>> objects = new Stack<HashSet<string>>();
            while (reader.Read()) {
                if (reader.TokenType == JsonTokenType.StartObject) {
                    objects.Push(new HashSet<string>(StringComparer.Ordinal));
                } else if (reader.TokenType == JsonTokenType.EndObject) {
                    objects.Pop();
                } else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString())) {
                    throw new InvalidDataException($"Property '{reader.GetString()}' appears twice in one object.");
                }
            }
        }
    }
}
