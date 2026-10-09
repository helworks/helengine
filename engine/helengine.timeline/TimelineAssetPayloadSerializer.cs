using helengine.files;

namespace helengine.timeline {
    /// <summary>
    /// Serializes <see cref="TimelineAsset"/> payloads in the HELE editor asset format under
    /// <see cref="EditorAssetBinaryValueKind.TimelineAsset"/>: identity, slots, cues and tracks in declaration order, with
    /// inline nested definitions written recursively and times stored as exact 64-bit doubles. The timeline module adds it
    /// to <see cref="EditorAssetPayloadSerializerRegistry"/> through <see cref="TimelineSerialization.Register"/>.
    /// </summary>
    public sealed class TimelineAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the HELE header of timeline payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.TimelineAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a timeline.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True for <see cref="TimelineAsset"/> instances.</returns>
        public bool Handles(Asset asset) {
            return asset is TimelineAsset;
        }

        /// <summary>
        /// Rejects timelines with missing lists or null entries before any byte is written. Content rules (times, names,
        /// references) are the validator's job, so drafts can still be stored.
        /// </summary>
        /// <param name="asset">Timeline about to be serialized.</param>
        public void Validate(Asset asset) {
            ValidateStructure((TimelineAsset)asset);
        }

        /// <summary>
        /// Writes the timeline payload after the shared HELE header.
        /// </summary>
        /// <param name="writer">Destination writer positioned after the header.</param>
        /// <param name="asset">Timeline to serialize.</param>
        public void Write(helengine.files.EngineBinaryWriter writer, Asset asset) {
            TimelineAsset timeline = (TimelineAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(timeline);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, timeline);
            WriteBody(writer, timeline);
        }

        /// <summary>
        /// Reads one timeline payload produced by <see cref="Write"/>.
        /// </summary>
        /// <param name="reader">Source reader positioned after the header.</param>
        /// <returns>Deserialized timeline.</returns>
        public Asset Read(EngineBinaryReader reader) {
            TimelineAsset timeline = new TimelineAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, timeline);
            ReadBody(reader, timeline);
            return timeline;
        }

        /// <summary>
        /// Checks a timeline tree for missing lists and null entries.
        /// </summary>
        /// <param name="timeline">Timeline to check.</param>
        static void ValidateStructure(TimelineAsset timeline) {
            if (timeline.Slots == null || timeline.Cues == null || timeline.Tracks == null) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a missing slot, cue or track list.");
            }
            if (timeline.Slots.Contains(null) || timeline.Cues.Contains(null) || timeline.Tracks.Contains(null)) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a null slot, cue or track.");
            }
            for (int index = 0; index < timeline.Tracks.Count; index++) {
                TimelineTrackAsset track = timeline.Tracks[index];
                if (track is TimelineEventTrackAsset eventTrack) {
                    if (eventTrack.Markers == null || eventTrack.Markers.Contains(null)) {
                        throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has an event track with a missing marker list or a null marker.");
                    }
                    continue;
                }
                IReadOnlyList<TimelineClipAsset> clips = track.ClipView;
                if (clips == null) {
                    throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a {TimelineJson.TrackKindName(track.Kind)} track with a missing clip list.");
                }
                for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++) {
                    ValidateClipStructure(timeline, clips[clipIndex]);
                }
            }
        }

        /// <summary>
        /// Checks one clip for null entries and missing keyframe lists, recursing into inline definitions.
        /// </summary>
        /// <param name="timeline">Timeline holding the clip, for messages.</param>
        /// <param name="clip">Clip to check.</param>
        static void ValidateClipStructure(TimelineAsset timeline, TimelineClipAsset clip) {
            if (clip == null) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a null clip.");
            }
            if (clip is TimelineTransformClipAsset transform) {
                if (transform.Position == null || transform.Rotation == null || transform.Scale == null || transform.Position.Contains(null) || transform.Rotation.Contains(null) || transform.Scale.Contains(null)) {
                    throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a transform clip with a missing keyframe list or a null keyframe.");
                }
            } else if (clip is TimelineValueClipAsset value) {
                if (value.Keyframes == null || value.Keyframes.Contains(null)) {
                    throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a value clip with a missing keyframe list or a null keyframe.");
                }
            } else if (clip is TimelineNestedClipAsset nested) {
                if (nested.SlotMappings == null || nested.SlotMappings.Contains(null)) {
                    throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a nested clip with a missing slot mapping or a null entry.");
                }
                if (nested.Definition != null) {
                    ValidateStructure(nested.Definition);
                }
            }
        }

        /// <summary>
        /// Writes everything of a timeline but its asset identity; inline definitions use it directly.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="timeline">Timeline to write.</param>
        static void WriteBody(helengine.files.EngineBinaryWriter writer, TimelineAsset timeline) {
            writer.WriteString(timeline.TimelineId ?? string.Empty);
            writer.WriteInt32(timeline.Version);
            writer.WriteString(timeline.DisplayName ?? string.Empty);
            writer.WriteString(timeline.Description ?? string.Empty);
            WriteDouble(writer, timeline.DurationSeconds);
            writer.WriteInt32(timeline.Slots.Count);
            for (int index = 0; index < timeline.Slots.Count; index++) {
                TimelineSlotAsset slot = timeline.Slots[index];
                writer.WriteString(slot.Name ?? string.Empty);
                writer.WriteInt32((int)slot.Kind);
                writer.WriteString(slot.Description ?? string.Empty);
            }
            writer.WriteInt32(timeline.Cues.Count);
            for (int index = 0; index < timeline.Cues.Count; index++) {
                writer.WriteString(timeline.Cues[index].Name ?? string.Empty);
                WriteDouble(writer, timeline.Cues[index].TimeSeconds);
            }
            writer.WriteInt32(timeline.Tracks.Count);
            for (int index = 0; index < timeline.Tracks.Count; index++) {
                WriteTrack(writer, timeline.Tracks[index]);
            }
        }

        /// <summary>
        /// Reads everything of a timeline but its asset identity.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <param name="timeline">Timeline receiving the values.</param>
        static void ReadBody(EngineBinaryReader reader, TimelineAsset timeline) {
            timeline.TimelineId = reader.ReadString();
            timeline.Version = reader.ReadInt32();
            timeline.DisplayName = reader.ReadString();
            timeline.Description = reader.ReadString();
            timeline.DurationSeconds = ReadDouble(reader);
            int slotCount = ReadCount(reader);
            for (int index = 0; index < slotCount; index++) {
                timeline.Slots.Add(new TimelineSlotAsset {
                    Name = reader.ReadString(),
                    Kind = (TimelineSlotKind)reader.ReadInt32(),
                    Description = reader.ReadString()
                });
            }
            int cueCount = ReadCount(reader);
            for (int index = 0; index < cueCount; index++) {
                timeline.Cues.Add(new TimelineCueAsset { Name = reader.ReadString(), TimeSeconds = ReadDouble(reader) });
            }
            int trackCount = ReadCount(reader);
            for (int index = 0; index < trackCount; index++) {
                timeline.Tracks.Add(ReadTrack(reader));
            }
        }

        /// <summary>
        /// Writes one track: kind byte, label and slot, then the kind-specific content.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="track">Track to write.</param>
        static void WriteTrack(helengine.files.EngineBinaryWriter writer, TimelineTrackAsset track) {
            writer.WriteByte((byte)track.Kind);
            writer.WriteString(track.Name ?? string.Empty);
            writer.WriteString(track.Slot ?? string.Empty);
            if (track is TimelineTransformTrackAsset transformTrack) {
                writer.WriteInt32((int)transformTrack.Mode);
                writer.WriteInt32(transformTrack.Clips.Count);
                for (int index = 0; index < transformTrack.Clips.Count; index++) {
                    TimelineTransformClipAsset clip = transformTrack.Clips[index];
                    WriteTiming(writer, clip);
                    WriteVectorKeyframes(writer, clip.Position);
                    WriteVectorKeyframes(writer, clip.Rotation);
                    WriteVectorKeyframes(writer, clip.Scale);
                }
            } else if (track is TimelineValueTrackAsset valueTrack) {
                writer.WriteString(valueTrack.Channel ?? string.Empty);
                writer.WriteInt32(valueTrack.Clips.Count);
                for (int index = 0; index < valueTrack.Clips.Count; index++) {
                    TimelineValueClipAsset clip = valueTrack.Clips[index];
                    WriteTiming(writer, clip);
                    writer.WriteInt32(clip.Keyframes.Count);
                    for (int keyframe = 0; keyframe < clip.Keyframes.Count; keyframe++) {
                        WriteDouble(writer, clip.Keyframes[keyframe].TimeSeconds);
                        WriteDouble(writer, clip.Keyframes[keyframe].Value);
                        writer.WriteString(clip.Keyframes[keyframe].Curve ?? string.Empty);
                    }
                }
            } else if (track is TimelineActivationTrackAsset activationTrack) {
                writer.WriteInt32(activationTrack.Clips.Count);
                for (int index = 0; index < activationTrack.Clips.Count; index++) {
                    WriteTiming(writer, activationTrack.Clips[index]);
                }
            } else if (track is TimelineAudioTrackAsset audioTrack) {
                writer.WriteInt32(audioTrack.Clips.Count);
                for (int index = 0; index < audioTrack.Clips.Count; index++) {
                    WriteTiming(writer, audioTrack.Clips[index]);
                    TimelineAssetReferences.WriteOptional(writer, audioTrack.Clips[index].Audio);
                    WriteDouble(writer, audioTrack.Clips[index].Gain);
                }
            } else if (track is TimelineAnimationTrackAsset animationTrack) {
                writer.WriteInt32(animationTrack.Clips.Count);
                for (int index = 0; index < animationTrack.Clips.Count; index++) {
                    WriteTiming(writer, animationTrack.Clips[index]);
                    TimelineAssetReferences.WriteOptional(writer, animationTrack.Clips[index].Animation);
                    WriteDouble(writer, animationTrack.Clips[index].Speed);
                }
            } else if (track is TimelineEventTrackAsset eventTrack) {
                writer.WriteInt32(eventTrack.Markers.Count);
                for (int index = 0; index < eventTrack.Markers.Count; index++) {
                    TimelineEventMarkerAsset marker = eventTrack.Markers[index];
                    WriteDouble(writer, marker.TimeSeconds);
                    writer.WriteString(marker.Cue ?? string.Empty);
                    writer.WriteString(marker.Name ?? string.Empty);
                    writer.WriteString(marker.Value ?? string.Empty);
                }
            } else if (track is TimelineNestedTrackAsset nestedTrack) {
                writer.WriteInt32(nestedTrack.Clips.Count);
                for (int index = 0; index < nestedTrack.Clips.Count; index++) {
                    WriteNestedClip(writer, nestedTrack.Clips[index]);
                }
            } else {
                throw new InvalidOperationException($"Track kind '{track.Kind}' has no payload layout.");
            }
        }

        /// <summary>
        /// Reads one track written by <see cref="WriteTrack"/>.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>The track.</returns>
        static TimelineTrackAsset ReadTrack(EngineBinaryReader reader) {
            TimelineTrackKind kind = (TimelineTrackKind)reader.ReadByte();
            string name = reader.ReadString();
            string slot = reader.ReadString();
            TimelineTrackAsset track;
            if (kind == TimelineTrackKind.Transform) {
                TimelineTransformTrackAsset transformTrack = new TimelineTransformTrackAsset { Mode = (TimelineTransformMode)reader.ReadInt32() };
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    TimelineTransformClipAsset clip = new TimelineTransformClipAsset();
                    ReadTiming(reader, clip);
                    ReadVectorKeyframes(reader, clip.Position);
                    ReadVectorKeyframes(reader, clip.Rotation);
                    ReadVectorKeyframes(reader, clip.Scale);
                    transformTrack.Clips.Add(clip);
                }
                track = transformTrack;
            } else if (kind == TimelineTrackKind.Value) {
                TimelineValueTrackAsset valueTrack = new TimelineValueTrackAsset { Channel = reader.ReadString() };
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    TimelineValueClipAsset clip = new TimelineValueClipAsset();
                    ReadTiming(reader, clip);
                    int keyframeCount = ReadCount(reader);
                    for (int keyframe = 0; keyframe < keyframeCount; keyframe++) {
                        clip.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = ReadDouble(reader), Value = ReadDouble(reader), Curve = reader.ReadString() });
                    }
                    valueTrack.Clips.Add(clip);
                }
                track = valueTrack;
            } else if (kind == TimelineTrackKind.Activation) {
                TimelineActivationTrackAsset activationTrack = new TimelineActivationTrackAsset();
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    TimelineClipAsset clip = new TimelineClipAsset();
                    ReadTiming(reader, clip);
                    activationTrack.Clips.Add(clip);
                }
                track = activationTrack;
            } else if (kind == TimelineTrackKind.Audio) {
                TimelineAudioTrackAsset audioTrack = new TimelineAudioTrackAsset();
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    TimelineAudioClipAsset clip = new TimelineAudioClipAsset();
                    ReadTiming(reader, clip);
                    clip.Audio = TimelineAssetReferences.ReadOptional(reader);
                    clip.Gain = ReadDouble(reader);
                    audioTrack.Clips.Add(clip);
                }
                track = audioTrack;
            } else if (kind == TimelineTrackKind.Animation) {
                TimelineAnimationTrackAsset animationTrack = new TimelineAnimationTrackAsset();
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    TimelineAnimationClipAsset clip = new TimelineAnimationClipAsset();
                    ReadTiming(reader, clip);
                    clip.Animation = TimelineAssetReferences.ReadOptional(reader);
                    clip.Speed = ReadDouble(reader);
                    animationTrack.Clips.Add(clip);
                }
                track = animationTrack;
            } else if (kind == TimelineTrackKind.Event) {
                TimelineEventTrackAsset eventTrack = new TimelineEventTrackAsset();
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    eventTrack.Markers.Add(new TimelineEventMarkerAsset {
                        TimeSeconds = ReadDouble(reader),
                        Cue = reader.ReadString(),
                        Name = reader.ReadString(),
                        Value = reader.ReadString()
                    });
                }
                track = eventTrack;
            } else if (kind == TimelineTrackKind.Timeline) {
                TimelineNestedTrackAsset nestedTrack = new TimelineNestedTrackAsset();
                int count = ReadCount(reader);
                for (int index = 0; index < count; index++) {
                    nestedTrack.Clips.Add(ReadNestedClip(reader));
                }
                track = nestedTrack;
            } else {
                throw new InvalidDataException($"Unknown timeline track kind {(int)kind}.");
            }
            track.Name = name;
            track.Slot = slot;
            return track;
        }

        /// <summary>
        /// Writes one nested timeline clip with its optional reference, optional inline definition and slot mapping.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="clip">Clip to write.</param>
        static void WriteNestedClip(helengine.files.EngineBinaryWriter writer, TimelineNestedClipAsset clip) {
            WriteTiming(writer, clip);
            TimelineAssetReferences.WriteOptional(writer, clip.Timeline);
            if (clip.Definition == null) {
                writer.WriteByte(0);
            } else {
                writer.WriteByte(1);
                WriteBody(writer, clip.Definition);
            }
            WriteDouble(writer, clip.Speed);
            writer.WriteInt32(clip.SlotMappings.Count);
            for (int index = 0; index < clip.SlotMappings.Count; index++) {
                writer.WriteString(clip.SlotMappings[index].Inner ?? string.Empty);
                writer.WriteString(clip.SlotMappings[index].Outer ?? string.Empty);
            }
        }

        /// <summary>
        /// Reads one nested timeline clip written by <see cref="WriteNestedClip"/>.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>The clip.</returns>
        static TimelineNestedClipAsset ReadNestedClip(EngineBinaryReader reader) {
            TimelineNestedClipAsset clip = new TimelineNestedClipAsset();
            ReadTiming(reader, clip);
            clip.Timeline = TimelineAssetReferences.ReadOptional(reader);
            if (reader.ReadByte() != 0) {
                clip.Definition = new TimelineAsset();
                ReadBody(reader, clip.Definition);
            }
            clip.Speed = ReadDouble(reader);
            int count = ReadCount(reader);
            for (int index = 0; index < count; index++) {
                clip.SlotMappings.Add(new TimelineSlotMappingAsset { Inner = reader.ReadString(), Outer = reader.ReadString() });
            }
            return clip;
        }

        /// <summary>
        /// Writes the timing fields every clip shares.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="clip">Clip whose timing is written.</param>
        static void WriteTiming(helengine.files.EngineBinaryWriter writer, TimelineClipAsset clip) {
            WriteDouble(writer, clip.StartSeconds);
            writer.WriteString(clip.Cue ?? string.Empty);
            WriteDouble(writer, clip.DurationSeconds);
            WriteDouble(writer, clip.ClipInSeconds);
            WriteDouble(writer, clip.EaseInSeconds);
            WriteDouble(writer, clip.EaseOutSeconds);
        }

        /// <summary>
        /// Reads the timing fields every clip shares.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <param name="clip">Clip receiving the timing.</param>
        static void ReadTiming(EngineBinaryReader reader, TimelineClipAsset clip) {
            clip.StartSeconds = ReadDouble(reader);
            clip.Cue = reader.ReadString();
            clip.DurationSeconds = ReadDouble(reader);
            clip.ClipInSeconds = ReadDouble(reader);
            clip.EaseInSeconds = ReadDouble(reader);
            clip.EaseOutSeconds = ReadDouble(reader);
        }

        /// <summary>
        /// Writes a counted list of vector keyframes.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="keyframes">Keyframes to write.</param>
        static void WriteVectorKeyframes(helengine.files.EngineBinaryWriter writer, List<TimelineVectorKeyframeAsset> keyframes) {
            writer.WriteInt32(keyframes.Count);
            for (int index = 0; index < keyframes.Count; index++) {
                TimelineVectorKeyframeAsset keyframe = keyframes[index];
                WriteDouble(writer, keyframe.TimeSeconds);
                WriteDouble(writer, keyframe.X);
                WriteDouble(writer, keyframe.Y);
                WriteDouble(writer, keyframe.Z);
                writer.WriteString(keyframe.Curve ?? string.Empty);
            }
        }

        /// <summary>
        /// Reads a counted list of vector keyframes.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <param name="keyframes">List receiving the keyframes.</param>
        static void ReadVectorKeyframes(EngineBinaryReader reader, List<TimelineVectorKeyframeAsset> keyframes) {
            int count = ReadCount(reader);
            for (int index = 0; index < count; index++) {
                keyframes.Add(new TimelineVectorKeyframeAsset {
                    TimeSeconds = ReadDouble(reader),
                    X = ReadDouble(reader),
                    Y = ReadDouble(reader),
                    Z = ReadDouble(reader),
                    Curve = reader.ReadString()
                });
            }
        }

        /// <summary>
        /// Writes a double exactly, as its 64-bit IEEE pattern (the editor writer has no double primitive).
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="value">Value to write.</param>
        static void WriteDouble(helengine.files.EngineBinaryWriter writer, double value) {
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>
        /// Reads a double written by <see cref="WriteDouble"/>.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>The value.</returns>
        static double ReadDouble(EngineBinaryReader reader) {
            return BitConverter.Int64BitsToDouble(reader.ReadInt64());
        }

        /// <summary>
        /// Reads a list count and rejects negative values from corrupt payloads.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>The count.</returns>
        static int ReadCount(EngineBinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0) {
                throw new InvalidDataException("Timeline payload has a negative list length.");
            }
            return count;
        }
    }
}
