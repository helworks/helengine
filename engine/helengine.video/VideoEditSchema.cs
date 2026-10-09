using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace helengine.video {
    /// <summary>
    /// Generates the JSON Schema of <c>helengine.video.edit.v1</c> from the C# model, so the schema the AI receives for
    /// structured output can never drift from what <see cref="VideoEditJson"/> accepts. Every object is closed and every
    /// string with a fixed vocabulary (modes, presets, kinds, locks) is an enum.
    /// </summary>
    public static class VideoEditSchema {
        /// <summary>
        /// Fixed vocabularies, keyed by declaring type and C# property name.
        /// </summary>
        static readonly Dictionary<string, string[]> Vocabularies = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            [Key(typeof(VideoSceneDuration), nameof(VideoSceneDuration.Mode))] = ["from_take", "fixed", "estimate"],
            [Key(typeof(VideoMedia), nameof(VideoMedia.Kind))] = ["video", "audio", "image", "font"],
            [Key(typeof(VideoLayer), nameof(VideoLayer.Kind))] = ["take", "media", "text"],
            [Key(typeof(VideoLayer), nameof(VideoLayer.Fit))] = ["contain", "cover"],
            [Key(typeof(VideoLayout), nameof(VideoLayout.Preset))] = ["full_frame", "inset", "side_by_side"],
            [Key(typeof(VideoMotion), nameof(VideoMotion.Preset))] = ["none", "zoom_to_focus"],
            [Key(typeof(VideoFocus), nameof(VideoFocus.Type))] = ["center", "point", "text_region"],
            [Key(typeof(VideoEnvelope), nameof(VideoEnvelope.Type))] = ["linear", "equal_power_in", "equal_power_out"],
            [Key(typeof(VideoMask), nameof(VideoMask.Channel))] = ["alpha", "luma"],
            [Key(typeof(VideoCaptionTrack), nameof(VideoCaptionTrack.Source))] = ["voice"],
            [Key(typeof(VideoGraphic), nameof(VideoGraphic.Layout))] = ["auto", "vertical", "horizontal"],
            [Key(typeof(VideoArrangement), nameof(VideoArrangement.Preset))] = VideoArrangementPresets.Ids,
            [Key(typeof(VideoLayer), nameof(VideoLayer.Region))] = VideoArrangementPresets.RegionNames,
            [Key(typeof(VideoOverlay), nameof(VideoOverlay.Region))] = VideoArrangementPresets.RegionNames,
            [Key(typeof(VideoEdit), nameof(VideoEdit.Schema))] = [VideoEditJson.SchemaId]
        };

        /// <summary>
        /// Values allowed for every lock marker.
        /// </summary>
        static readonly string[] LockValues = ["human", "ai"];

        /// <summary>
        /// Builds the schema document.
        /// </summary>
        /// <returns>JSON Schema (draft 2020-12) of an edit document.</returns>
        public static JsonElement Create() {
            JsonSchemaExporterOptions exporter = new JsonSchemaExporterOptions { TransformSchemaNode = Transform };
            JsonNode schema = JsonSchemaExporter.GetJsonSchemaAsNode(VideoEditJson.Options, typeof(VideoEdit), exporter);
            schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
            schema["$id"] = VideoEditJson.SchemaId;
            return JsonSerializer.SerializeToElement(schema);
        }

        /// <summary>
        /// Closes object schemas and injects vocabularies while the exporter walks the model.
        /// </summary>
        /// <param name="context">Exporter context for the node.</param>
        /// <param name="node">Generated schema node.</param>
        /// <returns>Adjusted schema node.</returns>
        static JsonNode Transform(JsonSchemaExporterContext context, JsonNode node) {
            if (node is not JsonObject schema) {
                return node;
            }
            if (context.TypeInfo.Kind == System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object && schema.ContainsKey("properties")) {
                schema["additionalProperties"] = false;
            }
            if (context.PropertyInfo != null) {
                string[] values = null;
                if (context.PropertyInfo.Name == "by") {
                    values = LockValues;
                } else if (context.PropertyInfo.DeclaringType != null) {
                    Vocabularies.TryGetValue(Key(context.PropertyInfo.DeclaringType, PropertyName(context)), out values);
                }
                if (values != null) {
                    JsonArray allowed = new JsonArray(values.Select(value => (JsonNode)JsonValue.Create(value)).ToArray());
                    if (schema["type"] is JsonArray types && types.Any(type => type?.GetValue<string>() == "null")) {
                        allowed.Add(null);
                    }
                    schema["enum"] = allowed;
                }
            }
            return schema;
        }

        /// <summary>
        /// Recovers the C# member name of the property being exported.
        /// </summary>
        /// <param name="context">Exporter context.</param>
        /// <returns>C# property name.</returns>
        static string PropertyName(JsonSchemaExporterContext context) {
            return (context.PropertyInfo.AttributeProvider as System.Reflection.MemberInfo)?.Name ?? context.PropertyInfo.Name;
        }

        /// <summary>
        /// Builds the vocabulary key of one property.
        /// </summary>
        /// <param name="type">Declaring type.</param>
        /// <param name="property">C# property name.</param>
        /// <returns>Dictionary key.</returns>
        static string Key(Type type, string property) {
            return type.FullName + "." + property;
        }
    }
}
