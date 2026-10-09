using System.Text.Json;

namespace helengine.timeline {
    /// <summary>
    /// Writes a <see cref="TimelineAsset"/> in the snake_case JSON form, omitting optional properties that hold their
    /// default, so reading the output back yields an identical model.
    /// </summary>
    static class TimelineJsonWriter {
        /// <summary>
        /// Writes one timeline object.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="timeline">Timeline to write.</param>
        /// <param name="includeSchema">True for the document root, which declares the schema id.</param>
        public static void WriteTimeline(Utf8JsonWriter writer, TimelineAsset timeline, bool includeSchema) {
            if (timeline.Slots == null || timeline.Cues == null || timeline.Tracks == null) {
                throw new InvalidOperationException("Timeline '" + timeline.TimelineId + "' has a missing slot, cue or track list.");
            }
            writer.WriteStartObject();
            if (includeSchema) {
                writer.WriteString("schema", TimelineJson.SchemaId);
            }
            writer.WriteString("id", timeline.TimelineId ?? string.Empty);
            writer.WriteNumber("version", timeline.Version);
            WriteOptionalString(writer, "display_name", timeline.DisplayName);
            WriteOptionalString(writer, "description", timeline.Description);
            writer.WriteNumber("duration", timeline.DurationSeconds);
            if (timeline.Slots.Count > 0) {
                writer.WriteStartArray("slots");
                for (int index = 0; index < timeline.Slots.Count; index++) {
                    TimelineSlotAsset slot = timeline.Slots[index];
                    writer.WriteStartObject();
                    writer.WriteString("name", slot.Name ?? string.Empty);
                    writer.WriteString("kind", TimelineJson.SlotKindName(slot.Kind));
                    WriteOptionalString(writer, "description", slot.Description);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            if (timeline.Cues.Count > 0) {
                writer.WriteStartArray("cues");
                for (int index = 0; index < timeline.Cues.Count; index++) {
                    writer.WriteStartObject();
                    writer.WriteString("name", timeline.Cues[index].Name ?? string.Empty);
                    writer.WriteNumber("time", timeline.Cues[index].TimeSeconds);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            if (timeline.Tracks.Count > 0) {
                writer.WriteStartArray("tracks");
                for (int index = 0; index < timeline.Tracks.Count; index++) {
                    WriteTrack(writer, timeline.Tracks[index]);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes one track of any kind.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="track">Track to write.</param>
        static void WriteTrack(Utf8JsonWriter writer, TimelineTrackAsset track) {
            writer.WriteStartObject();
            writer.WriteString("kind", TimelineJson.TrackKindName(track.Kind));
            WriteOptionalString(writer, "name", track.Name);
            WriteOptionalString(writer, "slot", track.Slot);
            if (track is TimelineEventTrackAsset eventTrack) {
                writer.WriteStartArray("markers");
                for (int index = 0; index < eventTrack.Markers.Count; index++) {
                    TimelineEventMarkerAsset marker = eventTrack.Markers[index];
                    writer.WriteStartObject();
                    writer.WritePropertyName("time");
                    WriteAnchor(writer, marker.Cue, marker.TimeSeconds);
                    writer.WriteString("name", marker.Name ?? string.Empty);
                    WriteOptionalString(writer, "value", marker.Value);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
                return;
            }
            if (track is TimelineTransformTrackAsset transformTrack && transformTrack.Mode != TimelineTransformMode.Absolute) {
                writer.WriteString("mode", transformTrack.Mode == TimelineTransformMode.Offset ? "offset" : transformTrack.Mode.ToString());
            }
            if (track is TimelineValueTrackAsset valueTrack) {
                writer.WriteString("channel", valueTrack.Channel ?? string.Empty);
            }
            IReadOnlyList<TimelineClipAsset> clips = track.ClipView;
            if (clips == null) {
                throw new InvalidOperationException("A " + TimelineJson.TrackKindName(track.Kind) + " track has a missing clip list.");
            }
            writer.WriteStartArray("clips");
            for (int index = 0; index < clips.Count; index++) {
                WriteClip(writer, clips[index]);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes one clip: shared timing first, then the kind-specific properties.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="clip">Clip to write.</param>
        static void WriteClip(Utf8JsonWriter writer, TimelineClipAsset clip) {
            writer.WriteStartObject();
            writer.WritePropertyName("start");
            WriteAnchor(writer, clip.Cue, clip.StartSeconds);
            writer.WriteNumber("duration", clip.DurationSeconds);
            WriteOptionalNumber(writer, "clip_in", clip.ClipInSeconds, 0);
            WriteOptionalNumber(writer, "ease_in", clip.EaseInSeconds, 0);
            WriteOptionalNumber(writer, "ease_out", clip.EaseOutSeconds, 0);
            if (clip is TimelineTransformClipAsset transform) {
                WriteVectorKeyframes(writer, "position", transform.Position);
                WriteVectorKeyframes(writer, "rotation", transform.Rotation);
                WriteVectorKeyframes(writer, "scale", transform.Scale);
            } else if (clip is TimelineValueClipAsset value) {
                writer.WriteStartArray("keyframes");
                for (int index = 0; index < value.Keyframes.Count; index++) {
                    TimelineKeyframeAsset keyframe = value.Keyframes[index];
                    writer.WriteStartObject();
                    writer.WriteNumber("time", keyframe.TimeSeconds);
                    writer.WriteNumber("value", keyframe.Value);
                    WriteCurve(writer, keyframe.Curve);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            } else if (clip is TimelineAudioClipAsset audio) {
                WriteReference(writer, "audio", audio.Audio);
                WriteOptionalNumber(writer, "gain", audio.Gain, 1);
            } else if (clip is TimelineAnimationClipAsset animation) {
                WriteReference(writer, "animation", animation.Animation);
                WriteOptionalNumber(writer, "speed", animation.Speed, 1);
            } else if (clip is TimelineNestedClipAsset nested) {
                WriteReference(writer, "timeline", nested.Timeline);
                if (nested.Definition != null) {
                    writer.WritePropertyName("definition");
                    WriteTimeline(writer, nested.Definition, false);
                }
                WriteOptionalNumber(writer, "speed", nested.Speed, 1);
                if (nested.SlotMappings.Count > 0) {
                    writer.WriteStartObject("slots");
                    for (int index = 0; index < nested.SlotMappings.Count; index++) {
                        writer.WriteString(nested.SlotMappings[index].Inner ?? string.Empty, nested.SlotMappings[index].Outer ?? string.Empty);
                    }
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a time as plain seconds, or as <c>{"cue", "offset"}</c> when it is anchored on a cue.
        /// </summary>
        /// <param name="writer">Destination JSON writer, positioned after a property name.</param>
        /// <param name="cue">Cue name; empty for an absolute time.</param>
        /// <param name="seconds">Absolute seconds or offset from the cue.</param>
        static void WriteAnchor(Utf8JsonWriter writer, string cue, double seconds) {
            if (string.IsNullOrEmpty(cue)) {
                writer.WriteNumberValue(seconds);
                return;
            }
            writer.WriteStartObject();
            writer.WriteString("cue", cue);
            WriteOptionalNumber(writer, "offset", seconds, 0);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a non-empty list of vector keyframes.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="name">List property name.</param>
        /// <param name="keyframes">Keyframes to write; nothing is written when empty.</param>
        static void WriteVectorKeyframes(Utf8JsonWriter writer, string name, List<TimelineVectorKeyframeAsset> keyframes) {
            if (keyframes.Count == 0) {
                return;
            }
            writer.WriteStartArray(name);
            for (int index = 0; index < keyframes.Count; index++) {
                TimelineVectorKeyframeAsset keyframe = keyframes[index];
                writer.WriteStartObject();
                writer.WriteNumber("time", keyframe.TimeSeconds);
                writer.WriteStartArray("value");
                writer.WriteNumberValue(keyframe.X);
                writer.WriteNumberValue(keyframe.Y);
                writer.WriteNumberValue(keyframe.Z);
                writer.WriteEndArray();
                WriteCurve(writer, keyframe.Curve);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        /// <summary>
        /// Writes an optional asset reference object; nothing is written when the reference is null.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="name">Property name.</param>
        /// <param name="reference">Reference to write.</param>
        static void WriteReference(Utf8JsonWriter writer, string name, SceneAssetReference reference) {
            if (reference == null) {
                return;
            }
            writer.WriteStartObject(name);
            writer.WriteString("path", reference.RelativePath);
            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                writer.WriteString("source", "generated");
            }
            WriteOptionalString(writer, "provider_id", reference.ProviderId);
            WriteOptionalString(writer, "asset_id", reference.AssetId);
            WriteOptionalString(writer, "content_hash", reference.ContentHash);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a keyframe curve unless it is the default linear curve.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="curve">Curve id.</param>
        static void WriteCurve(Utf8JsonWriter writer, string curve) {
            if (curve != CurveCatalog.Linear) {
                writer.WriteString("curve", curve ?? string.Empty);
            }
        }

        /// <summary>
        /// Writes a string property unless it is empty.
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="name">Property name.</param>
        /// <param name="value">Value to write.</param>
        static void WriteOptionalString(Utf8JsonWriter writer, string name, string value) {
            if (!string.IsNullOrEmpty(value)) {
                writer.WriteString(name, value);
            }
        }

        /// <summary>
        /// Writes a number property unless it equals its default (compared bit for bit, so -0 is kept).
        /// </summary>
        /// <param name="writer">Destination JSON writer.</param>
        /// <param name="name">Property name.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="fallback">Default the reader restores when the property is omitted.</param>
        static void WriteOptionalNumber(Utf8JsonWriter writer, string name, double value, double fallback) {
            if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(fallback)) {
                writer.WriteNumber(name, value);
            }
        }
    }
}
