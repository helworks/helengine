using helengine.files;
using helengine.timeline.runtime;

namespace helengine.timeline {
    /// <summary>
    /// Writes cooked timelines (value kind <see cref="EditorAssetBinaryValueKind.CookedTimelineAsset"/>) in the HELE asset
    /// format, which the packaged runtime reads with <see cref="CookedTimelineAssetReader"/>. Reads delegate to that
    /// reader so the cooked layout is defined once, next to the runtime. Registered by
    /// <see cref="TimelineSerialization.Register"/>.
    /// </summary>
    public sealed class CookedTimelineAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind reserved in core for cooked timelines.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.CookedTimelineAsset;
            }
        }

        /// <summary>
        /// Returns whether the asset is a cooked timeline.
        /// </summary>
        /// <param name="asset">Asset to test.</param>
        /// <returns>True for <see cref="CookedTimelineAsset"/>.</returns>
        public bool Handles(Asset asset) {
            return asset is CookedTimelineAsset;
        }

        /// <summary>
        /// Rejects cooked timelines the runtime could not play: missing tables, out-of-range indices, unknown curves, or
        /// unsorted or overlapping entries.
        /// </summary>
        /// <param name="asset">Cooked timeline to check.</param>
        public void Validate(Asset asset) {
            CookedTimelineAsset timeline = (CookedTimelineAsset)asset;
            if (timeline.TickRate <= 0 || timeline.DurationTicks <= 0) {
                throw new InvalidOperationException($"Cooked timeline '{timeline.Id}' needs a positive tick rate and duration.");
            } else if (timeline.SlotNames == null || timeline.Strings == null || timeline.Tracks == null) {
                throw new InvalidOperationException($"Cooked timeline '{timeline.Id}' has a missing slot, string or track table.");
            } else if (Array.IndexOf(timeline.SlotNames, null) >= 0 || Array.IndexOf(timeline.Strings, null) >= 0 || Array.IndexOf(timeline.Tracks, null) >= 0) {
                throw new InvalidOperationException($"Cooked timeline '{timeline.Id}' has a null slot name, string or track.");
            }

            for (int index = 0; index < timeline.Tracks.Length; index++) {
                ValidateTrack(timeline, index, timeline.Tracks[index]);
            }
        }

        /// <summary>
        /// Writes the identity and the cooked body.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="asset">Cooked timeline to write.</param>
        public void Write(helengine.files.EngineBinaryWriter writer, Asset asset) {
            CookedTimelineAsset timeline = (CookedTimelineAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(timeline);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, timeline);
            writer.WriteByte(CookedTimelineAssetReader.LayoutVersion);
            writer.WriteInt32(timeline.TickRate);
            writer.WriteInt32(timeline.DurationTicks);
            WriteStrings(writer, timeline.SlotNames);
            WriteStrings(writer, timeline.Strings);
            writer.WriteInt32(timeline.Tracks.Length);
            for (int index = 0; index < timeline.Tracks.Length; index++) {
                WriteTrack(writer, timeline.Tracks[index]);
            }
        }

        /// <summary>
        /// Reads a cooked timeline through the runtime reader.
        /// </summary>
        /// <param name="reader">Reader positioned at the asset identity.</param>
        /// <returns>The cooked timeline.</returns>
        public Asset Read(EngineBinaryReader reader) {
            return CookedTimelineAssetReader.ReadPayload(reader);
        }

        /// <summary>
        /// Checks one track's indices and ordering.
        /// </summary>
        /// <param name="timeline">Timeline being validated.</param>
        /// <param name="index">Track index, for messages.</param>
        /// <param name="track">Track to check.</param>
        static void ValidateTrack(CookedTimelineAsset timeline, int index, CookedTimelineTrack track) {
            string where = $"Cooked timeline '{timeline.Id}' track {index}";
            if (track.Segments == null || track.Intervals == null || track.AudioClips == null || track.AnimationClips == null || track.Markers == null) {
                throw new InvalidOperationException(where + " has a missing entry list.");
            } else if (track.SlotIndex < -1 || track.SlotIndex >= timeline.SlotNames.Length) {
                throw new InvalidOperationException($"{where} is bound to slot {track.SlotIndex}, outside the {timeline.SlotNames.Length} slots.");
            } else if (track.SlotIndex < 0 && track.Kind != CookedTimelineTrackKind.Event && track.Kind != CookedTimelineTrackKind.Audio) {
                throw new InvalidOperationException(where + " needs a slot.");
            } else if (track.Kind == CookedTimelineTrackKind.Transform && (track.ChannelIndex < 0 || track.ChannelIndex > (int)CookedTimelineTransformChannel.ScaleZ)) {
                throw new InvalidOperationException($"{where} drives unknown transform channel {track.ChannelIndex}.");
            }

            int previousEnd = int.MinValue;
            for (int segment = 0; segment < track.Segments.Length; segment++) {
                CookedTimelineSegment value = track.Segments[segment];
                if (value == null || value.EndTick < value.StartTick || value.StartTick < previousEnd || value.CurveCode >= CurveCatalog.Count) {
                    throw new InvalidOperationException($"{where} segment {segment} is null, reversed, overlaps the previous one or uses an unknown curve.");
                }
                previousEnd = value.EndTick;
            }

            previousEnd = int.MinValue;
            for (int interval = 0; interval < track.Intervals.Length; interval++) {
                CookedTimelineInterval value = track.Intervals[interval];
                if (value == null || value.EndTick <= value.StartTick || value.StartTick < previousEnd) {
                    throw new InvalidOperationException($"{where} interval {interval} is null, empty or overlaps the previous one.");
                }
                previousEnd = value.EndTick;
            }

            ValidateClips(where, track);
            int previousTick = int.MinValue;
            for (int marker = 0; marker < track.Markers.Length; marker++) {
                CookedTimelineMarker value = track.Markers[marker];
                if (value == null || value.Tick < previousTick || value.NameIndex < 0 || value.NameIndex >= timeline.Strings.Length || value.ValueIndex < 0 || value.ValueIndex >= timeline.Strings.Length) {
                    throw new InvalidOperationException($"{where} marker {marker} is null, out of order or outside the string table.");
                }
                previousTick = value.Tick;
            }
        }

        /// <summary>
        /// Checks the audio and animation entries of one track.
        /// </summary>
        /// <param name="where">Message prefix naming the track.</param>
        /// <param name="track">Track to check.</param>
        static void ValidateClips(string where, CookedTimelineTrack track) {
            int previousStart = int.MinValue;
            for (int clip = 0; clip < track.AudioClips.Length; clip++) {
                CookedTimelineAudioClip value = track.AudioClips[clip];
                if (value == null || value.Audio == null || value.EndTick <= value.StartTick || value.StartTick < previousStart || value.ClipInTicks < 0) {
                    throw new InvalidOperationException($"{where} audio clip {clip} is null, has no asset, is empty or is out of order.");
                }
                previousStart = value.StartTick;
            }

            previousStart = int.MinValue;
            for (int clip = 0; clip < track.AnimationClips.Length; clip++) {
                CookedTimelineAnimationClip value = track.AnimationClips[clip];
                if (value == null || value.Animation == null || value.EndTick < value.StartTick || value.StartTick < previousStart || value.ClipInTicks < 0) {
                    throw new InvalidOperationException($"{where} animation clip {clip} is null, has no asset, is reversed or is out of order.");
                }
                previousStart = value.StartTick;
            }
        }

        /// <summary>
        /// Writes one track in the layout <see cref="CookedTimelineAssetReader"/> documents.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="track">Track to write.</param>
        static void WriteTrack(helengine.files.EngineBinaryWriter writer, CookedTimelineTrack track) {
            writer.WriteByte((byte)track.Kind);
            writer.WriteInt32(track.SlotIndex);
            writer.WriteInt32(track.ReceiverId);
            writer.WriteInt32(track.ChannelIndex);
            writer.WriteByte((byte)track.Mode);
            writer.WriteInt32(track.Segments.Length);
            for (int index = 0; index < track.Segments.Length; index++) {
                CookedTimelineSegment segment = track.Segments[index];
                writer.WriteInt32(segment.StartTick);
                writer.WriteInt32(segment.EndTick);
                writer.WriteSingle(segment.From);
                writer.WriteSingle(segment.To);
                writer.WriteByte(segment.CurveCode);
            }

            writer.WriteInt32(track.Intervals.Length);
            for (int index = 0; index < track.Intervals.Length; index++) {
                writer.WriteInt32(track.Intervals[index].StartTick);
                writer.WriteInt32(track.Intervals[index].EndTick);
            }

            writer.WriteInt32(track.AudioClips.Length);
            for (int index = 0; index < track.AudioClips.Length; index++) {
                CookedTimelineAudioClip clip = track.AudioClips[index];
                writer.WriteInt32(clip.StartTick);
                writer.WriteInt32(clip.EndTick);
                writer.WriteInt32(clip.ClipInTicks);
                writer.WriteSingle(clip.Gain);
                TimelineAssetReferences.WriteOptional(writer, clip.Audio);
            }

            writer.WriteInt32(track.AnimationClips.Length);
            for (int index = 0; index < track.AnimationClips.Length; index++) {
                CookedTimelineAnimationClip clip = track.AnimationClips[index];
                writer.WriteInt32(clip.StartTick);
                writer.WriteInt32(clip.EndTick);
                writer.WriteInt32(clip.ClipInTicks);
                writer.WriteSingle(clip.Speed);
                TimelineAssetReferences.WriteOptional(writer, clip.Animation);
            }

            writer.WriteInt32(track.Markers.Length);
            for (int index = 0; index < track.Markers.Length; index++) {
                writer.WriteInt32(track.Markers[index].Tick);
                writer.WriteInt32(track.Markers[index].NameIndex);
                writer.WriteInt32(track.Markers[index].ValueIndex);
            }
        }

        /// <summary>
        /// Writes a counted list of strings.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="values">Strings to write.</param>
        static void WriteStrings(helengine.files.EngineBinaryWriter writer, string[] values) {
            writer.WriteInt32(values.Length);
            for (int index = 0; index < values.Length; index++) {
                writer.WriteString(values[index]);
            }
        }
    }
}
