namespace helengine.timeline.runtime {
    /// <summary>
    /// Reads cooked timelines (value kind <see cref="EditorAssetBinaryValueKind.CookedTimelineAsset"/>) in the packaged
    /// runtime. It is the read half of the cooked layout; the tools-side serializer in helengine.timeline writes the same
    /// layout and delegates its reads here, so the layout lives in one place. Files use the shared HELE header with the
    /// packaged asset format id and version, followed by the asset identity and the cooked body:
    /// <code>
    /// byte layout version (1)
    /// int tick rate, int duration ticks
    /// int slot count, slot names
    /// int string count, strings
    /// int track count, then per track:
    ///   byte kind, int slot index, int receiver id, int channel index, byte mode
    ///   int segment count:   int start, int end, float from, float to, byte curve code
    ///   int interval count:  int start, int end
    ///   int audio count:     int start, int end, int clip-in ticks, float gain, optional asset reference
    ///   int animation count: int start, int end, int clip-in ticks, float speed, optional asset reference
    ///   int marker count:    int tick, int name index, int value index
    /// </code>
    /// </summary>
    public static class CookedTimelineAssetReader {
        /// <summary>
        /// Version of the cooked body layout written after the asset identity.
        /// </summary>
        public const byte LayoutVersion = 1;

        /// <summary>
        /// Reads one packaged cooked timeline, header included.
        /// </summary>
        /// <param name="stream">Stream positioned at the HELE header.</param>
        /// <returns>The cooked timeline.</returns>
        [NativeOwnedReturn]
        public static CookedTimelineAsset Deserialize([NativeNoEscape] Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            try {
                ValidateHeader(header);
                using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
                return ReadPayload(reader);
            } finally {
                NativeOwnership.Delete(header);
            }
        }

        /// <summary>
        /// Reads the identity and cooked body that follow the header.
        /// </summary>
        /// <param name="reader">Reader positioned at the asset identity.</param>
        /// <returns>The cooked timeline.</returns>
        [NativeOwnedReturn]
        public static CookedTimelineAsset ReadPayload([NativeNoEscape] EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            CookedTimelineAsset asset = new CookedTimelineAsset();
            asset.Id = reader.ReadString();
            asset.RuntimeAssetId = (ulong)reader.ReadInt64();
            asset.AuthoringAssetId = reader.ReadString();
            asset.FormerAuthoringAssetIds = ReadStrings(reader);
            byte layoutVersion = reader.ReadByte();
            if (layoutVersion != LayoutVersion) {
                throw new InvalidOperationException($"Cooked timeline layout version '{layoutVersion}' is unsupported; version '{LayoutVersion}' is required. Re-cook the timeline.");
            }

            asset.TickRate = reader.ReadInt32();
            asset.DurationTicks = reader.ReadInt32();
            if (asset.TickRate <= 0) {
                throw new InvalidOperationException($"Cooked timeline '{asset.Id}' has a non-positive tick rate {asset.TickRate}.");
            } else if (asset.DurationTicks <= 0) {
                throw new InvalidOperationException($"Cooked timeline '{asset.Id}' has a non-positive duration of {asset.DurationTicks} ticks.");
            }

            asset.SlotNames = ReadStrings(reader);
            asset.Strings = ReadStrings(reader);
            int trackCount = ReadCount(reader);
            CookedTimelineTrack[] tracks = new CookedTimelineTrack[trackCount];
            for (int index = 0; index < trackCount; index++) {
                tracks[index] = ReadTrack(reader, asset);
            }

            asset.Tracks = tracks;
            return asset;
        }

        /// <summary>
        /// Rejects headers that do not describe a packaged cooked timeline of the current version.
        /// </summary>
        /// <param name="header">Decoded HELE header.</param>
        static void ValidateHeader([NativeNoEscape] EngineBinaryHeader header) {
            if (header.FormatId != PackagedAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)PackagedAssetBinarySerializer.RecordKind) {
                throw new InvalidOperationException($"Unexpected asset record kind '{header.RecordKind}'.");
            } else if (header.Version != PackagedAssetBinarySerializer.CurrentVersion) {
                throw new InvalidOperationException($"Packaged asset version '{header.Version}' is unsupported; version '{PackagedAssetBinarySerializer.CurrentVersion}' is required. Regenerate the packaged asset.");
            } else if (header.ValueKind != (ushort)EditorAssetBinaryValueKind.CookedTimelineAsset) {
                throw new InvalidOperationException($"Serialized payload value kind '{header.ValueKind}' is not a cooked timeline.");
            }
        }

        /// <summary>
        /// Reads one track and checks its indices against the asset tables.
        /// </summary>
        /// <param name="reader">Reader positioned at the track.</param>
        /// <param name="asset">Asset whose slot and string tables are already read.</param>
        /// <returns>The track.</returns>
        static CookedTimelineTrack ReadTrack(EngineBinaryReader reader, CookedTimelineAsset asset) {
            CookedTimelineTrack track = new CookedTimelineTrack();
            byte kind = reader.ReadByte();
            if (kind > (byte)CookedTimelineTrackKind.Event) {
                throw new InvalidOperationException($"Cooked timeline '{asset.Id}' has an unknown track kind {kind}.");
            }

            track.Kind = (CookedTimelineTrackKind)kind;
            track.SlotIndex = reader.ReadInt32();
            track.ReceiverId = reader.ReadInt32();
            track.ChannelIndex = reader.ReadInt32();
            byte mode = reader.ReadByte();
            if (mode > (byte)CookedTimelineTransformMode.Offset) {
                throw new InvalidOperationException($"Cooked timeline '{asset.Id}' has an unknown transform mode {mode}.");
            }

            track.Mode = (CookedTimelineTransformMode)mode;
            if (track.SlotIndex < -1 || track.SlotIndex >= asset.SlotCount) {
                throw new InvalidOperationException($"Cooked timeline '{asset.Id}' has a track bound to slot {track.SlotIndex}, but it declares {asset.SlotCount} slots.");
            }

            track.Segments = ReadSegments(reader, asset);
            track.Intervals = ReadIntervals(reader);
            track.AudioClips = ReadAudioClips(reader);
            track.AnimationClips = ReadAnimationClips(reader);
            track.Markers = ReadMarkers(reader, asset);
            return track;
        }

        /// <summary>
        /// Reads a segment list and rejects unknown curve codes.
        /// </summary>
        /// <param name="reader">Reader positioned at the segment count.</param>
        /// <param name="asset">Asset being read, for messages.</param>
        /// <returns>The segments.</returns>
        static CookedTimelineSegment[] ReadSegments(EngineBinaryReader reader, CookedTimelineAsset asset) {
            int count = ReadCount(reader);
            if (count == 0) {
                return Array.Empty<CookedTimelineSegment>();
            }

            CookedTimelineSegment[] segments = new CookedTimelineSegment[count];
            for (int index = 0; index < count; index++) {
                CookedTimelineSegment segment = new CookedTimelineSegment();
                segment.StartTick = reader.ReadInt32();
                segment.EndTick = reader.ReadInt32();
                segment.From = reader.ReadSingle();
                segment.To = reader.ReadSingle();
                segment.CurveCode = reader.ReadByte();
                if (segment.CurveCode >= CurveCatalog.Count) {
                    throw new InvalidOperationException($"Cooked timeline '{asset.Id}' uses unknown curve code {segment.CurveCode}.");
                }

                segments[index] = segment;
            }

            return segments;
        }

        /// <summary>
        /// Reads an activation interval list.
        /// </summary>
        /// <param name="reader">Reader positioned at the interval count.</param>
        /// <returns>The intervals.</returns>
        static CookedTimelineInterval[] ReadIntervals(EngineBinaryReader reader) {
            int count = ReadCount(reader);
            if (count == 0) {
                return Array.Empty<CookedTimelineInterval>();
            }

            CookedTimelineInterval[] intervals = new CookedTimelineInterval[count];
            for (int index = 0; index < count; index++) {
                CookedTimelineInterval interval = new CookedTimelineInterval();
                interval.StartTick = reader.ReadInt32();
                interval.EndTick = reader.ReadInt32();
                intervals[index] = interval;
            }

            return intervals;
        }

        /// <summary>
        /// Reads an audio clip list.
        /// </summary>
        /// <param name="reader">Reader positioned at the audio clip count.</param>
        /// <returns>The audio clips.</returns>
        static CookedTimelineAudioClip[] ReadAudioClips(EngineBinaryReader reader) {
            int count = ReadCount(reader);
            if (count == 0) {
                return Array.Empty<CookedTimelineAudioClip>();
            }

            CookedTimelineAudioClip[] clips = new CookedTimelineAudioClip[count];
            for (int index = 0; index < count; index++) {
                CookedTimelineAudioClip clip = new CookedTimelineAudioClip();
                clip.StartTick = reader.ReadInt32();
                clip.EndTick = reader.ReadInt32();
                clip.ClipInTicks = reader.ReadInt32();
                clip.Gain = reader.ReadSingle();
                clip.Audio = SceneAssetReferenceFactory.ReadOptionalReference(reader);
                clips[index] = clip;
            }

            return clips;
        }

        /// <summary>
        /// Reads an animation clip list.
        /// </summary>
        /// <param name="reader">Reader positioned at the animation clip count.</param>
        /// <returns>The animation clips.</returns>
        static CookedTimelineAnimationClip[] ReadAnimationClips(EngineBinaryReader reader) {
            int count = ReadCount(reader);
            if (count == 0) {
                return Array.Empty<CookedTimelineAnimationClip>();
            }

            CookedTimelineAnimationClip[] clips = new CookedTimelineAnimationClip[count];
            for (int index = 0; index < count; index++) {
                CookedTimelineAnimationClip clip = new CookedTimelineAnimationClip();
                clip.StartTick = reader.ReadInt32();
                clip.EndTick = reader.ReadInt32();
                clip.ClipInTicks = reader.ReadInt32();
                clip.Speed = reader.ReadSingle();
                clip.Animation = SceneAssetReferenceFactory.ReadOptionalReference(reader);
                clips[index] = clip;
            }

            return clips;
        }

        /// <summary>
        /// Reads an event marker list and checks its string indices.
        /// </summary>
        /// <param name="reader">Reader positioned at the marker count.</param>
        /// <param name="asset">Asset whose string table is already read.</param>
        /// <returns>The markers.</returns>
        static CookedTimelineMarker[] ReadMarkers(EngineBinaryReader reader, CookedTimelineAsset asset) {
            int count = ReadCount(reader);
            if (count == 0) {
                return Array.Empty<CookedTimelineMarker>();
            }

            CookedTimelineMarker[] markers = new CookedTimelineMarker[count];
            for (int index = 0; index < count; index++) {
                CookedTimelineMarker marker = new CookedTimelineMarker();
                marker.Tick = reader.ReadInt32();
                marker.NameIndex = reader.ReadInt32();
                marker.ValueIndex = reader.ReadInt32();
                if (marker.NameIndex < 0 || marker.NameIndex >= asset.Strings.Length || marker.ValueIndex < 0 || marker.ValueIndex >= asset.Strings.Length) {
                    throw new InvalidOperationException($"Cooked timeline '{asset.Id}' has an event marker outside its string table.");
                }

                markers[index] = marker;
            }

            return markers;
        }

        /// <summary>
        /// Reads a counted list of strings.
        /// </summary>
        /// <param name="reader">Reader positioned at the count.</param>
        /// <returns>The strings.</returns>
        static string[] ReadStrings(EngineBinaryReader reader) {
            int count = ReadCount(reader);
            if (count == 0) {
                return Array.Empty<string>();
            }

            string[] values = new string[count];
            for (int index = 0; index < count; index++) {
                values[index] = reader.ReadString();
            }

            return values;
        }

        /// <summary>
        /// Reads a list count and rejects negative values from corrupt payloads.
        /// </summary>
        /// <param name="reader">Reader positioned at the count.</param>
        /// <returns>The count.</returns>
        static int ReadCount(EngineBinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0) {
                throw new InvalidOperationException($"Cooked timeline list count {count} is negative.");
            }

            return count;
        }
    }
}
