using System.Text.Json;

namespace helengine.timeline {
    /// <summary>
    /// Reads the snake_case JSON form into a <see cref="TimelineAsset"/>. It only checks structure (object shapes, known
    /// properties, value types, enum names, required fields); rules about values are left to <see cref="TimelineValidator"/>.
    /// Omitted or null optional properties take the model defaults.
    /// </summary>
    static class TimelineJsonReader {
        /// <summary>
        /// Properties of a timeline object.
        /// </summary>
        static readonly string[] TimelineFields = new string[] { "schema", "id", "version", "display_name", "description", "duration", "slots", "cues", "tracks" };

        /// <summary>
        /// Properties of a slot object.
        /// </summary>
        static readonly string[] SlotFields = new string[] { "name", "kind", "description" };

        /// <summary>
        /// Properties of a cue object.
        /// </summary>
        static readonly string[] CueFields = new string[] { "name", "time" };

        /// <summary>
        /// Properties any track object may use; each kind accepts a subset.
        /// </summary>
        static readonly string[] TrackFields = new string[] { "kind", "name", "slot", "mode", "channel", "clips", "markers" };

        /// <summary>
        /// Properties any clip object may use; each kind accepts a subset.
        /// </summary>
        static readonly string[] ClipFields = new string[] {
            "start", "duration", "clip_in", "ease_in", "ease_out", "position", "rotation", "scale", "keyframes",
            "audio", "gain", "animation", "speed", "timeline", "definition", "slots"
        };

        /// <summary>
        /// Properties of a cue-anchored start or time object.
        /// </summary>
        static readonly string[] AnchorFields = new string[] { "cue", "offset" };

        /// <summary>
        /// Properties of a scalar keyframe object.
        /// </summary>
        static readonly string[] KeyframeFields = new string[] { "time", "value", "curve" };

        /// <summary>
        /// Properties of an event marker object.
        /// </summary>
        static readonly string[] MarkerFields = new string[] { "time", "name", "value" };

        /// <summary>
        /// Properties of an asset reference object.
        /// </summary>
        static readonly string[] ReferenceFields = new string[] { "path", "source", "provider_id", "asset_id", "content_hash" };

        /// <summary>
        /// Reads one timeline object (the root, or an inline nested definition).
        /// </summary>
        /// <param name="element">Timeline object.</param>
        /// <param name="path">JSON path of the object; empty for the root.</param>
        /// <returns>The timeline.</returns>
        public static TimelineAsset ReadTimeline(JsonElement element, string path) {
            TimelineJsonFields fields = new TimelineJsonFields(element, path, TimelineFields, "a timeline");
            if (fields.Has("schema") && fields.RequiredString("schema") != TimelineJson.SchemaId) {
                throw TimelineJsonFields.Fail(TimelinePath.Member(path, "schema"), "Expected schema '" + TimelineJson.SchemaId + "', found '" + fields.RequiredString("schema") + "'.");
            }
            TimelineAsset timeline = new TimelineAsset {
                TimelineId = fields.RequiredString("id"),
                Version = fields.OptionalInteger("version", 1),
                DisplayName = fields.OptionalString("display_name"),
                Description = fields.OptionalString("description"),
                DurationSeconds = fields.RequiredNumber("duration")
            };
            List<JsonElement> slots = fields.OptionalArray("slots");
            for (int index = 0; index < slots.Count; index++) {
                timeline.Slots.Add(ReadSlot(slots[index], TimelinePath.Index(TimelinePath.Member(path, "slots"), index)));
            }
            List<JsonElement> cues = fields.OptionalArray("cues");
            for (int index = 0; index < cues.Count; index++) {
                TimelineJsonFields cue = new TimelineJsonFields(cues[index], TimelinePath.Index(TimelinePath.Member(path, "cues"), index), CueFields, "a cue");
                timeline.Cues.Add(new TimelineCueAsset { Name = cue.RequiredString("name"), TimeSeconds = cue.RequiredNumber("time") });
            }
            List<JsonElement> tracks = fields.OptionalArray("tracks");
            for (int index = 0; index < tracks.Count; index++) {
                timeline.Tracks.Add(ReadTrack(tracks[index], TimelinePath.Index(TimelinePath.Member(path, "tracks"), index)));
            }
            return timeline;
        }

        /// <summary>
        /// Reads one slot declaration.
        /// </summary>
        /// <param name="element">Slot object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The slot.</returns>
        static TimelineSlotAsset ReadSlot(JsonElement element, string path) {
            TimelineJsonFields fields = new TimelineJsonFields(element, path, SlotFields, "a slot");
            return new TimelineSlotAsset {
                Name = fields.RequiredString("name"),
                Kind = ParseSlotKind(fields.RequiredString("kind"), TimelinePath.Member(path, "kind")),
                Description = fields.OptionalString("description")
            };
        }

        /// <summary>
        /// Reads one track of any kind.
        /// </summary>
        /// <param name="element">Track object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The track.</returns>
        static TimelineTrackAsset ReadTrack(JsonElement element, string path) {
            TimelineJsonFields fields = new TimelineJsonFields(element, path, TrackFields, "a track");
            string kindPath = TimelinePath.Member(path, "kind");
            TimelineTrackKind kind = ParseTrackKind(fields.RequiredString("kind"), kindPath);
            string kindName = TimelineJson.TrackKindName(kind);
            foreach (string name in fields.Names()) {
                if (!TrackAccepts(kind, name)) {
                    throw TimelineJsonFields.Fail(TimelinePath.Member(path, name), "The property '" + name + "' does not apply to " + kindName + " tracks.");
                }
            }
            List<JsonElement> clips = fields.OptionalArray("clips");
            string clipsPath = TimelinePath.Member(path, "clips");
            TimelineTrackAsset track;
            if (kind == TimelineTrackKind.Transform) {
                TimelineTransformTrackAsset transform = new TimelineTransformTrackAsset { Mode = ParseMode(fields, path) };
                for (int index = 0; index < clips.Count; index++) {
                    transform.Clips.Add(ReadTransformClip(clips[index], TimelinePath.Index(clipsPath, index)));
                }
                track = transform;
            } else if (kind == TimelineTrackKind.Value) {
                TimelineValueTrackAsset value = new TimelineValueTrackAsset { Channel = fields.RequiredString("channel") };
                for (int index = 0; index < clips.Count; index++) {
                    value.Clips.Add(ReadValueClip(clips[index], TimelinePath.Index(clipsPath, index)));
                }
                track = value;
            } else if (kind == TimelineTrackKind.Activation) {
                TimelineActivationTrackAsset activation = new TimelineActivationTrackAsset();
                for (int index = 0; index < clips.Count; index++) {
                    TimelineClipAsset clip = new TimelineClipAsset();
                    ReadClipTiming(Clip(clips[index], TimelinePath.Index(clipsPath, index), kind), clip);
                    activation.Clips.Add(clip);
                }
                track = activation;
            } else if (kind == TimelineTrackKind.Audio) {
                TimelineAudioTrackAsset audio = new TimelineAudioTrackAsset();
                for (int index = 0; index < clips.Count; index++) {
                    audio.Clips.Add(ReadAudioClip(clips[index], TimelinePath.Index(clipsPath, index)));
                }
                track = audio;
            } else if (kind == TimelineTrackKind.Animation) {
                TimelineAnimationTrackAsset animation = new TimelineAnimationTrackAsset();
                for (int index = 0; index < clips.Count; index++) {
                    animation.Clips.Add(ReadAnimationClip(clips[index], TimelinePath.Index(clipsPath, index)));
                }
                track = animation;
            } else if (kind == TimelineTrackKind.Event) {
                TimelineEventTrackAsset events = new TimelineEventTrackAsset();
                List<JsonElement> markers = fields.OptionalArray("markers");
                for (int index = 0; index < markers.Count; index++) {
                    events.Markers.Add(ReadMarker(markers[index], TimelinePath.Index(TimelinePath.Member(path, "markers"), index)));
                }
                track = events;
            } else {
                TimelineNestedTrackAsset nested = new TimelineNestedTrackAsset();
                for (int index = 0; index < clips.Count; index++) {
                    nested.Clips.Add(ReadNestedClip(clips[index], TimelinePath.Index(clipsPath, index)));
                }
                track = nested;
            }
            track.Name = fields.OptionalString("name");
            track.Slot = fields.OptionalString("slot");
            return track;
        }

        /// <summary>
        /// Reads a transform clip.
        /// </summary>
        /// <param name="element">Clip object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The clip.</returns>
        static TimelineTransformClipAsset ReadTransformClip(JsonElement element, string path) {
            TimelineJsonFields fields = Clip(element, path, TimelineTrackKind.Transform);
            TimelineTransformClipAsset clip = new TimelineTransformClipAsset();
            ReadClipTiming(fields, clip);
            ReadVectorKeyframes(fields, "position", clip.Position);
            ReadVectorKeyframes(fields, "rotation", clip.Rotation);
            ReadVectorKeyframes(fields, "scale", clip.Scale);
            return clip;
        }

        /// <summary>
        /// Reads a value clip.
        /// </summary>
        /// <param name="element">Clip object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The clip.</returns>
        static TimelineValueClipAsset ReadValueClip(JsonElement element, string path) {
            TimelineJsonFields fields = Clip(element, path, TimelineTrackKind.Value);
            TimelineValueClipAsset clip = new TimelineValueClipAsset();
            ReadClipTiming(fields, clip);
            List<JsonElement> keyframes = fields.OptionalArray("keyframes");
            for (int index = 0; index < keyframes.Count; index++) {
                string keyframePath = TimelinePath.Index(TimelinePath.Member(path, "keyframes"), index);
                TimelineJsonFields keyframe = new TimelineJsonFields(keyframes[index], keyframePath, KeyframeFields, "a keyframe");
                clip.Keyframes.Add(new TimelineKeyframeAsset {
                    TimeSeconds = keyframe.RequiredNumber("time"),
                    Value = keyframe.RequiredNumber("value"),
                    Curve = keyframe.Has("curve") ? keyframe.RequiredString("curve") : CurveCatalog.Linear
                });
            }
            return clip;
        }

        /// <summary>
        /// Reads an audio clip.
        /// </summary>
        /// <param name="element">Clip object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The clip.</returns>
        static TimelineAudioClipAsset ReadAudioClip(JsonElement element, string path) {
            TimelineJsonFields fields = Clip(element, path, TimelineTrackKind.Audio);
            TimelineAudioClipAsset clip = new TimelineAudioClipAsset();
            ReadClipTiming(fields, clip);
            clip.Audio = ReadReference(fields.Required("audio"), TimelinePath.Member(path, "audio"));
            clip.Gain = fields.OptionalNumber("gain", 1);
            return clip;
        }

        /// <summary>
        /// Reads an animation clip placement.
        /// </summary>
        /// <param name="element">Clip object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The clip.</returns>
        static TimelineAnimationClipAsset ReadAnimationClip(JsonElement element, string path) {
            TimelineJsonFields fields = Clip(element, path, TimelineTrackKind.Animation);
            TimelineAnimationClipAsset clip = new TimelineAnimationClipAsset();
            ReadClipTiming(fields, clip);
            clip.Animation = ReadReference(fields.Required("animation"), TimelinePath.Member(path, "animation"));
            clip.Speed = fields.OptionalNumber("speed", 1);
            return clip;
        }

        /// <summary>
        /// Reads a nested timeline clip with its reference or inline definition and its slot mapping.
        /// </summary>
        /// <param name="element">Clip object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The clip.</returns>
        static TimelineNestedClipAsset ReadNestedClip(JsonElement element, string path) {
            TimelineJsonFields fields = Clip(element, path, TimelineTrackKind.Timeline);
            TimelineNestedClipAsset clip = new TimelineNestedClipAsset();
            ReadClipTiming(fields, clip);
            if (fields.Has("timeline")) {
                clip.Timeline = ReadReference(fields.Required("timeline"), TimelinePath.Member(path, "timeline"));
            }
            if (fields.Has("definition")) {
                clip.Definition = ReadTimeline(fields.Required("definition"), TimelinePath.Member(path, "definition"));
            }
            clip.Speed = fields.OptionalNumber("speed", 1);
            if (fields.Has("slots")) {
                string slotsPath = TimelinePath.Member(path, "slots");
                JsonElement slots = fields.Required("slots");
                if (slots.ValueKind != JsonValueKind.Object) {
                    throw TimelineJsonFields.Fail(slotsPath, "Expected an object mapping nested slot names to slots of this timeline, found " + TimelineJsonFields.Describe(slots) + ".");
                }
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty mapping in slots.EnumerateObject()) {
                    string mappingPath = TimelinePath.Member(slotsPath, mapping.Name);
                    if (!seen.Add(mapping.Name)) {
                        throw TimelineJsonFields.Fail(mappingPath, "The nested slot '" + mapping.Name + "' is mapped twice.");
                    }
                    clip.SlotMappings.Add(new TimelineSlotMappingAsset { Inner = mapping.Name, Outer = TimelineJsonFields.ToText(mapping.Value, mappingPath) });
                }
            }
            return clip;
        }

        /// <summary>
        /// Reads one event marker.
        /// </summary>
        /// <param name="element">Marker object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The marker.</returns>
        static TimelineEventMarkerAsset ReadMarker(JsonElement element, string path) {
            TimelineJsonFields fields = new TimelineJsonFields(element, path, MarkerFields, "an event marker");
            TimelineEventMarkerAsset marker = new TimelineEventMarkerAsset {
                Name = fields.RequiredString("name"),
                Value = fields.OptionalString("value")
            };
            string cue;
            double time;
            ReadAnchor(fields.Required("time"), TimelinePath.Member(path, "time"), out cue, out time);
            marker.Cue = cue;
            marker.TimeSeconds = time;
            return marker;
        }

        /// <summary>
        /// Reads a clip object's properties and rejects those that do not apply to its track kind.
        /// </summary>
        /// <param name="element">Clip object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <param name="kind">Kind of the track holding the clip.</param>
        /// <returns>The clip's properties.</returns>
        static TimelineJsonFields Clip(JsonElement element, string path, TimelineTrackKind kind) {
            TimelineJsonFields fields = new TimelineJsonFields(element, path, ClipFields, "a clip");
            foreach (string name in fields.Names()) {
                if (!ClipAccepts(kind, name)) {
                    throw TimelineJsonFields.Fail(TimelinePath.Member(path, name), "The property '" + name + "' does not apply to " + TimelineJson.TrackKindName(kind) + " clips.");
                }
            }
            return fields;
        }

        /// <summary>
        /// Reads the timing properties every clip shares.
        /// </summary>
        /// <param name="fields">Clip properties.</param>
        /// <param name="clip">Clip receiving the values.</param>
        static void ReadClipTiming(TimelineJsonFields fields, TimelineClipAsset clip) {
            string cue;
            double start;
            ReadAnchor(fields.Required("start"), TimelinePath.Member(fields.Path, "start"), out cue, out start);
            clip.Cue = cue;
            clip.StartSeconds = start;
            clip.DurationSeconds = fields.RequiredNumber("duration");
            clip.ClipInSeconds = fields.OptionalNumber("clip_in", 0);
            clip.EaseInSeconds = fields.OptionalNumber("ease_in", 0);
            clip.EaseOutSeconds = fields.OptionalNumber("ease_out", 0);
        }

        /// <summary>
        /// Reads a time that is either absolute seconds or <c>{"cue": name, "offset": seconds}</c>.
        /// </summary>
        /// <param name="element">Number or anchor object.</param>
        /// <param name="path">JSON path of the value.</param>
        /// <param name="cue">Cue name; empty for an absolute time.</param>
        /// <param name="seconds">Absolute seconds or the offset from the cue.</param>
        static void ReadAnchor(JsonElement element, string path, out string cue, out double seconds) {
            if (element.ValueKind == JsonValueKind.Number) {
                cue = string.Empty;
                seconds = element.GetDouble();
                return;
            }
            if (element.ValueKind != JsonValueKind.Object) {
                throw TimelineJsonFields.Fail(path, "Expected seconds or {\"cue\": name, \"offset\": seconds}, found " + TimelineJsonFields.Describe(element) + ".");
            }
            TimelineJsonFields fields = new TimelineJsonFields(element, path, AnchorFields, "a cue anchor");
            cue = fields.RequiredString("cue");
            if (cue.Length == 0) {
                throw TimelineJsonFields.Fail(TimelinePath.Member(path, "cue"), "The cue name cannot be empty; use a plain number for an absolute time.");
            }
            seconds = fields.OptionalNumber("offset", 0);
        }

        /// <summary>
        /// Reads an optional list of vector keyframes.
        /// </summary>
        /// <param name="fields">Clip properties.</param>
        /// <param name="name">List property name: position, rotation or scale.</param>
        /// <param name="keyframes">List receiving the keyframes.</param>
        static void ReadVectorKeyframes(TimelineJsonFields fields, string name, List<TimelineVectorKeyframeAsset> keyframes) {
            List<JsonElement> items = fields.OptionalArray(name);
            for (int index = 0; index < items.Count; index++) {
                string keyframePath = TimelinePath.Index(TimelinePath.Member(fields.Path, name), index);
                TimelineJsonFields keyframe = new TimelineJsonFields(items[index], keyframePath, KeyframeFields, "a keyframe");
                JsonElement value = keyframe.Required("value");
                string valuePath = TimelinePath.Member(keyframePath, "value");
                if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3) {
                    throw TimelineJsonFields.Fail(valuePath, "Expected a list of three numbers [x, y, z], found " + TimelineJsonFields.Describe(value) + ".");
                }
                keyframes.Add(new TimelineVectorKeyframeAsset {
                    TimeSeconds = keyframe.RequiredNumber("time"),
                    X = TimelineJsonFields.ToNumber(value[0], TimelinePath.Index(valuePath, 0)),
                    Y = TimelineJsonFields.ToNumber(value[1], TimelinePath.Index(valuePath, 1)),
                    Z = TimelineJsonFields.ToNumber(value[2], TimelinePath.Index(valuePath, 2)),
                    Curve = keyframe.Has("curve") ? keyframe.RequiredString("curve") : CurveCatalog.Linear
                });
            }
        }

        /// <summary>
        /// Reads an asset reference object into a validated <see cref="SceneAssetReference"/>.
        /// </summary>
        /// <param name="element">Reference object.</param>
        /// <param name="path">JSON path of the object.</param>
        /// <returns>The reference.</returns>
        static SceneAssetReference ReadReference(JsonElement element, string path) {
            TimelineJsonFields fields = new TimelineJsonFields(element, path, ReferenceFields, "an asset reference");
            SceneAssetReferenceSourceKind source = SceneAssetReferenceSourceKind.FileSystem;
            if (fields.Has("source")) {
                string sourceName = fields.RequiredString("source");
                if (sourceName == "generated") {
                    source = SceneAssetReferenceSourceKind.Generated;
                } else if (sourceName != "file_system") {
                    throw TimelineJsonFields.Fail(TimelinePath.Member(path, "source"), "The source must be file_system or generated (found '" + sourceName + "').");
                }
            }
            try {
                return TimelineAssetReferences.Create(source, fields.RequiredString("path"), fields.OptionalString("provider_id"), fields.OptionalString("asset_id"), fields.OptionalString("content_hash"));
            } catch (ArgumentException exception) {
                throw TimelineJsonFields.Fail(path, "Invalid asset reference: " + exception.Message);
            }
        }

        /// <summary>
        /// Reads the optional transform mode of a track.
        /// </summary>
        /// <param name="fields">Track properties.</param>
        /// <param name="path">JSON path of the track.</param>
        /// <returns>The mode; absolute when omitted.</returns>
        static TimelineTransformMode ParseMode(TimelineJsonFields fields, string path) {
            if (!fields.Has("mode")) {
                return TimelineTransformMode.Absolute;
            }
            string mode = fields.RequiredString("mode");
            if (mode == "absolute") {
                return TimelineTransformMode.Absolute;
            } else if (mode == "offset") {
                return TimelineTransformMode.Offset;
            }
            throw TimelineJsonFields.Fail(TimelinePath.Member(path, "mode"), "The transform mode must be absolute or offset (found '" + mode + "').");
        }

        /// <summary>
        /// Parses a slot kind name.
        /// </summary>
        /// <param name="name">entity, text, media or rect.</param>
        /// <param name="path">JSON path of the value.</param>
        /// <returns>The slot kind.</returns>
        static TimelineSlotKind ParseSlotKind(string name, string path) {
            for (int kind = 0; kind <= (int)TimelineSlotKind.Rect; kind++) {
                if (TimelineJson.SlotKindName((TimelineSlotKind)kind) == name) {
                    return (TimelineSlotKind)kind;
                }
            }
            throw TimelineJsonFields.Fail(path, "The slot kind must be entity, text, media or rect (found '" + name + "').");
        }

        /// <summary>
        /// Parses a track kind name.
        /// </summary>
        /// <param name="name">transform, value, activation, audio, animation, event or timeline.</param>
        /// <param name="path">JSON path of the value.</param>
        /// <returns>The track kind.</returns>
        static TimelineTrackKind ParseTrackKind(string name, string path) {
            for (int kind = 0; kind <= (int)TimelineTrackKind.Timeline; kind++) {
                if (TimelineJson.TrackKindName((TimelineTrackKind)kind) == name) {
                    return (TimelineTrackKind)kind;
                }
            }
            throw TimelineJsonFields.Fail(path, "The track kind must be transform, value, activation, audio, animation, event or timeline (found '" + name + "').");
        }

        /// <summary>
        /// Reports whether a track property applies to a track kind.
        /// </summary>
        /// <param name="kind">Track kind.</param>
        /// <param name="name">Property name.</param>
        /// <returns>True when the kind uses the property.</returns>
        static bool TrackAccepts(TimelineTrackKind kind, string name) {
            if (name == "kind" || name == "name" || name == "slot") {
                return true;
            } else if (name == "mode") {
                return kind == TimelineTrackKind.Transform;
            } else if (name == "channel") {
                return kind == TimelineTrackKind.Value;
            } else if (name == "markers") {
                return kind == TimelineTrackKind.Event;
            }
            return kind != TimelineTrackKind.Event;
        }

        /// <summary>
        /// Reports whether a clip property applies to clips of a track kind.
        /// </summary>
        /// <param name="kind">Track kind.</param>
        /// <param name="name">Property name.</param>
        /// <returns>True when the kind uses the property.</returns>
        static bool ClipAccepts(TimelineTrackKind kind, string name) {
            if (name == "start" || name == "duration" || name == "clip_in" || name == "ease_in" || name == "ease_out") {
                return true;
            } else if (name == "position" || name == "rotation" || name == "scale") {
                return kind == TimelineTrackKind.Transform;
            } else if (name == "keyframes") {
                return kind == TimelineTrackKind.Value;
            } else if (name == "audio" || name == "gain") {
                return kind == TimelineTrackKind.Audio;
            } else if (name == "animation") {
                return kind == TimelineTrackKind.Animation;
            } else if (name == "speed") {
                return kind == TimelineTrackKind.Animation || kind == TimelineTrackKind.Timeline;
            }
            return kind == TimelineTrackKind.Timeline;
        }
    }
}
