using System.Text;
using System.Text.Json;

namespace helengine.timeline {
    /// <summary>
    /// Reads and writes timelines as <c>helengine.timeline.v1</c> JSON: snake_case names, unknown and repeated properties
    /// rejected, omitted or null optional properties restored to their defaults, documents capped at
    /// <see cref="MaxDocumentBytes"/>. This is the form people and planner models author; parsing validates the result and
    /// reports every problem with its JSON path. Writing then reading yields an identical model.
    /// </summary>
    public static class TimelineJson {
        /// <summary>
        /// Schema id the root object declares (optional on input, always written).
        /// </summary>
        public const string SchemaId = "helengine.timeline.v1";

        /// <summary>
        /// Largest accepted document, in UTF-8 bytes.
        /// </summary>
        public const int MaxDocumentBytes = 64 * 1024;

        /// <summary>
        /// Deepest accepted JSON nesting; enough for eight levels of inline timelines.
        /// </summary>
        const int MaxJsonDepth = 128;

        /// <summary>
        /// Parses and validates timeline JSON text.
        /// </summary>
        /// <param name="json">Document text.</param>
        /// <param name="resolver">Loads timelines referenced by nested clips; null leaves references unfollowed.</param>
        /// <returns>The valid timeline.</returns>
        /// <exception cref="TimelineFormatException">The text is too large, is not JSON, has the wrong shape or fails validation.</exception>
        public static TimelineAsset Parse(string json, ITimelineAssetResolver resolver) {
            if (json == null) {
                throw new ArgumentNullException(nameof(json));
            }
            EnsureSize(Encoding.UTF8.GetByteCount(json));
            JsonDocument document;
            try {
                document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            } catch (JsonException exception) {
                throw TimelineJsonFields.Fail(string.Empty, "The text is not valid JSON: " + exception.Message);
            }
            using (document) {
                return Parse(document.RootElement, resolver);
            }
        }

        /// <summary>
        /// Reads and validates a timeline JSON element.
        /// </summary>
        /// <param name="element">Root timeline object.</param>
        /// <param name="resolver">Loads timelines referenced by nested clips; null leaves references unfollowed.</param>
        /// <returns>The valid timeline.</returns>
        /// <exception cref="TimelineFormatException">The element is too large, has the wrong shape or fails validation.</exception>
        public static TimelineAsset Parse(JsonElement element, ITimelineAssetResolver resolver) {
            TimelineAsset timeline = ReadUnvalidated(element);
            TimelineValidator.EnsureValid(timeline, resolver);
            return timeline;
        }

        /// <summary>
        /// Reads a timeline JSON element checking only its structure, for hosts that collect validation diagnostics
        /// themselves (for example to merge them with their own).
        /// </summary>
        /// <param name="element">Root timeline object.</param>
        /// <returns>The timeline, possibly invalid.</returns>
        /// <exception cref="TimelineFormatException">The element is too large or has the wrong shape.</exception>
        public static TimelineAsset ReadUnvalidated(JsonElement element) {
            EnsureSize(Encoding.UTF8.GetByteCount(element.GetRawText()));
            return TimelineJsonReader.ReadTimeline(element, string.Empty);
        }

        /// <summary>
        /// Writes a timeline as a JSON element. The timeline is not validated, so drafts can be written too.
        /// </summary>
        /// <param name="timeline">Timeline to write.</param>
        /// <returns>A detached root element.</returns>
        public static JsonElement Write(TimelineAsset timeline) {
            using JsonDocument document = JsonDocument.Parse(WriteBytes(timeline, false), new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            return document.RootElement.Clone();
        }

        /// <summary>
        /// Writes a timeline as indented JSON text.
        /// </summary>
        /// <param name="timeline">Timeline to write.</param>
        /// <returns>Indented UTF-8 JSON text.</returns>
        public static string Serialize(TimelineAsset timeline) {
            return Encoding.UTF8.GetString(WriteBytes(timeline, true));
        }

        /// <summary>
        /// Returns the JSON name of a slot kind.
        /// </summary>
        /// <param name="kind">Slot kind.</param>
        /// <returns>entity, text, media or rect.</returns>
        public static string SlotKindName(TimelineSlotKind kind) {
            if (kind == TimelineSlotKind.Entity) {
                return "entity";
            } else if (kind == TimelineSlotKind.Text) {
                return "text";
            } else if (kind == TimelineSlotKind.Media) {
                return "media";
            } else if (kind == TimelineSlotKind.Rect) {
                return "rect";
            }
            throw new ArgumentOutOfRangeException(nameof(kind), "Unknown slot kind " + (int)kind + ".");
        }

        /// <summary>
        /// Returns the JSON name of a track kind.
        /// </summary>
        /// <param name="kind">Track kind.</param>
        /// <returns>transform, value, activation, audio, animation, event or timeline.</returns>
        public static string TrackKindName(TimelineTrackKind kind) {
            if (kind == TimelineTrackKind.Transform) {
                return "transform";
            } else if (kind == TimelineTrackKind.Value) {
                return "value";
            } else if (kind == TimelineTrackKind.Activation) {
                return "activation";
            } else if (kind == TimelineTrackKind.Audio) {
                return "audio";
            } else if (kind == TimelineTrackKind.Animation) {
                return "animation";
            } else if (kind == TimelineTrackKind.Event) {
                return "event";
            } else if (kind == TimelineTrackKind.Timeline) {
                return "timeline";
            }
            throw new ArgumentOutOfRangeException(nameof(kind), "Unknown track kind " + (int)kind + ".");
        }

        /// <summary>
        /// Writes a timeline to UTF-8 JSON bytes.
        /// </summary>
        /// <param name="timeline">Timeline to write.</param>
        /// <param name="indented">Whether to indent the output.</param>
        /// <returns>The JSON bytes.</returns>
        static byte[] WriteBytes(TimelineAsset timeline, bool indented) {
            if (timeline == null) {
                throw new ArgumentNullException(nameof(timeline));
            }
            using MemoryStream stream = new MemoryStream();
            JsonWriterOptions options = new JsonWriterOptions { Indented = indented, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = MaxJsonDepth };
            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, options)) {
                TimelineJsonWriter.WriteTimeline(writer, timeline, true);
            }
            return stream.ToArray();
        }

        /// <summary>
        /// Rejects documents above the size cap.
        /// </summary>
        /// <param name="bytes">Document size in UTF-8 bytes.</param>
        static void EnsureSize(int bytes) {
            if (bytes > MaxDocumentBytes) {
                throw TimelineJsonFields.Fail(string.Empty, "The timeline JSON is " + bytes + " bytes; the limit is " + MaxDocumentBytes + " bytes.");
            }
        }
    }
}
