namespace helengine.animation.runtime {
    /// <summary>
    /// Reads and writes packaged hierarchy animation clips (value kind
    /// <see cref="EditorAssetBinaryValueKind.HierarchyAnimationClipAsset"/>). Files use the shared HELE header with the
    /// packaged asset format id and version, followed by the payload:
    /// <code>
    /// byte layout version (1)
    /// double duration seconds
    /// int track count, then per track:
    ///   string target path, byte interpolation mode
    ///   int position count, position times (double), position values (3 x float)
    ///   int rotation count, rotation times (double), rotation values (4 x float)
    ///   int scale count,    scale times (double),    scale values (3 x float)
    /// </code>
    /// The editor serializer writes the header and delegates the payload to <see cref="Write"/>, so the layout lives in
    /// one place, next to the runtime.
    /// </summary>
    public static class HierarchyAnimationClipReader {
        /// <summary>
        /// Reads one packaged clip, header included.
        /// </summary>
        /// <param name="stream">Stream positioned at the HELE header.</param>
        /// <returns>The clip.</returns>
        [NativeOwnedReturn]
        public static HierarchyAnimationClipAsset Deserialize([NativeNoEscape] Stream stream) {
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
        /// Reads the payload that follows the header.
        /// </summary>
        /// <param name="reader">Reader positioned at the layout byte.</param>
        /// <returns>The clip.</returns>
        [NativeOwnedReturn]
        public static HierarchyAnimationClipAsset ReadPayload([NativeNoEscape] EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte layoutVersion = reader.ReadByte();
            if (layoutVersion != HierarchyAnimationClipAsset.LayoutVersion) {
                throw new InvalidOperationException($"Hierarchy animation clip layout version '{layoutVersion}' is unsupported; version '{HierarchyAnimationClipAsset.LayoutVersion}' is required. Re-import the animation.");
            }

            HierarchyAnimationClipAsset clip = new HierarchyAnimationClipAsset();
            clip.Duration = reader.ReadDouble();
            if (double.IsNaN(clip.Duration) || double.IsInfinity(clip.Duration) || clip.Duration < 0d) {
                throw new InvalidOperationException($"Hierarchy animation clip has an invalid duration {clip.Duration}.");
            }

            clip.Tracks = ReadTracks(reader);
            return clip;
        }

        /// <summary>
        /// Writes the payload (layout byte, duration and tracks) of one clip. The caller writes the HELE header.
        /// </summary>
        /// <param name="writer">Destination writer positioned after the header.</param>
        /// <param name="clip">Clip to write.</param>
        public static void Write([NativeNoEscape] EngineBinaryWriter writer, [NativeNoEscape] HierarchyAnimationClipAsset clip) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            } else if (clip.Tracks == null) {
                throw new InvalidOperationException($"Hierarchy animation clip '{clip.Id}' has no track table.");
            }

            for (int index = 0; index < clip.Tracks.Length; index++) {
                if (clip.Tracks[index] == null) {
                    throw new InvalidOperationException($"Hierarchy animation clip '{clip.Id}' has a null track at index {index}.");
                }
                ValidateTrack(clip.Tracks[index]);
            }

            writer.WriteByte(HierarchyAnimationClipAsset.LayoutVersion);
            writer.WriteDouble(clip.Duration);
            writer.WriteInt32(clip.Tracks.Length);
            for (int index = 0; index < clip.Tracks.Length; index++) {
                HierarchyAnimationTrack track = clip.Tracks[index];
                writer.WriteString(track.TargetPath);
                writer.WriteByte((byte)track.Interpolation);
                WriteTimes(writer, track.PositionTimes);
                for (int key = 0; key < track.PositionValues.Length; key++) {
                    writer.WriteFloat3(track.PositionValues[key]);
                }
                WriteTimes(writer, track.RotationTimes);
                for (int key = 0; key < track.RotationValues.Length; key++) {
                    writer.WriteFloat4(track.RotationValues[key]);
                }
                WriteTimes(writer, track.ScaleTimes);
                for (int key = 0; key < track.ScaleValues.Length; key++) {
                    writer.WriteFloat3(track.ScaleValues[key]);
                }
            }
        }

        /// <summary>
        /// Rejects headers that do not describe a packaged hierarchy animation clip of the current packaged version.
        /// </summary>
        /// <param name="header">Decoded HELE header.</param>
        static void ValidateHeader([NativeNoEscape] EngineBinaryHeader header) {
            if (header.FormatId != PackagedAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)PackagedAssetBinarySerializer.RecordKind) {
                throw new InvalidOperationException($"Unexpected asset record kind '{header.RecordKind}'.");
            } else if (header.Version != PackagedAssetBinarySerializer.CurrentVersion) {
                throw new InvalidOperationException($"Packaged asset version '{header.Version}' is unsupported; version '{PackagedAssetBinarySerializer.CurrentVersion}' is required. Regenerate the packaged asset.");
            } else if (header.ValueKind != (ushort)EditorAssetBinaryValueKind.HierarchyAnimationClipAsset) {
                throw new InvalidOperationException($"Serialized payload value kind '{header.ValueKind}' is not a hierarchy animation clip.");
            }
        }

        /// <summary>
        /// Reads the track count and every track, validating each one.
        /// </summary>
        /// <param name="reader">Reader positioned at the track count.</param>
        /// <returns>A new array of tracks.</returns>
        [NativeOwnedReturn]
        static HierarchyAnimationTrack[] ReadTracks([NativeNoEscape] EngineBinaryReader reader) {
            int trackCount = ReadCount(reader);
            HierarchyAnimationTrack[] tracks = new HierarchyAnimationTrack[trackCount];
            for (int index = 0; index < trackCount; index++) {
                tracks[index] = ReadTrack(reader);
                ValidateTrack(tracks[index]);
            }

            return tracks;
        }

        /// <summary>
        /// Reads one track.
        /// </summary>
        /// <param name="reader">Reader positioned at the track.</param>
        /// <returns>The track.</returns>
        [NativeOwnedReturn]
        static HierarchyAnimationTrack ReadTrack([NativeNoEscape] EngineBinaryReader reader) {
            HierarchyAnimationTrack track = new HierarchyAnimationTrack();
            track.TargetPath = reader.ReadString();
            byte interpolation = reader.ReadByte();
            if (interpolation > (byte)AnimationInterpolationMode.Linear) {
                throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has an unknown interpolation mode {interpolation}.");
            }

            track.Interpolation = (AnimationInterpolationMode)interpolation;
            track.PositionTimes = ReadTimes(reader);
            track.PositionValues = ReadFloat3Values(reader, track.PositionTimes.Length);
            track.RotationTimes = ReadTimes(reader);
            track.RotationValues = ReadFloat4Values(reader, track.RotationTimes.Length);
            track.ScaleTimes = ReadTimes(reader);
            track.ScaleValues = ReadFloat3Values(reader, track.ScaleTimes.Length);
            return track;
        }

        /// <summary>
        /// Reads a key count followed by that many key times.
        /// </summary>
        /// <param name="reader">Reader positioned at the count.</param>
        /// <returns>A new array of key times.</returns>
        [NativeOwnedReturn]
        static double[] ReadTimes([NativeNoEscape] EngineBinaryReader reader) {
            int count = ReadCount(reader);
            double[] times = new double[count];
            for (int index = 0; index < count; index++) {
                times[index] = reader.ReadDouble();
            }

            return times;
        }

        /// <summary>
        /// Reads a fixed number of three-component key values.
        /// </summary>
        /// <param name="reader">Reader positioned at the values.</param>
        /// <param name="count">Number of values.</param>
        /// <returns>A new array of values.</returns>
        [NativeOwnedReturn]
        static float3[] ReadFloat3Values([NativeNoEscape] EngineBinaryReader reader, int count) {
            float3[] values = new float3[count];
            for (int index = 0; index < count; index++) {
                values[index] = reader.ReadFloat3();
            }

            return values;
        }

        /// <summary>
        /// Reads a fixed number of quaternion key values.
        /// </summary>
        /// <param name="reader">Reader positioned at the values.</param>
        /// <param name="count">Number of values.</param>
        /// <returns>A new array of values.</returns>
        [NativeOwnedReturn]
        static float4[] ReadFloat4Values([NativeNoEscape] EngineBinaryReader reader, int count) {
            float4[] values = new float4[count];
            for (int index = 0; index < count; index++) {
                values[index] = reader.ReadFloat4();
            }

            return values;
        }

        /// <summary>
        /// Writes a key count followed by the key times.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="times">Key times.</param>
        static void WriteTimes([NativeNoEscape] EngineBinaryWriter writer, [NativeNoEscape] double[] times) {
            writer.WriteInt32(times.Length);
            for (int index = 0; index < times.Length; index++) {
                writer.WriteDouble(times[index]);
            }
        }

        /// <summary>
        /// Reads one non-negative element count.
        /// </summary>
        /// <param name="reader">Reader positioned at the count.</param>
        /// <returns>The count.</returns>
        static int ReadCount([NativeNoEscape] EngineBinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0) {
                throw new InvalidOperationException($"Hierarchy animation clip has a negative element count {count}.");
            }

            return count;
        }

        /// <summary>
        /// Rejects a track the evaluator could not sample: missing arrays, value counts that differ from time counts, or
        /// times that are not finite and strictly increasing.
        /// </summary>
        /// <param name="track">Track to check.</param>
        static void ValidateTrack([NativeNoEscape] HierarchyAnimationTrack track) {
            if (track.TargetPath == null) {
                throw new InvalidOperationException("Hierarchy animation track has no target path.");
            } else if (track.PositionTimes == null || track.PositionValues == null || track.RotationTimes == null
                || track.RotationValues == null || track.ScaleTimes == null || track.ScaleValues == null) {
                throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has a missing key array.");
            } else if (track.PositionTimes.Length != track.PositionValues.Length) {
                throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has {track.PositionTimes.Length} position times but {track.PositionValues.Length} values.");
            } else if (track.RotationTimes.Length != track.RotationValues.Length) {
                throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has {track.RotationTimes.Length} rotation times but {track.RotationValues.Length} values.");
            } else if (track.ScaleTimes.Length != track.ScaleValues.Length) {
                throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has {track.ScaleTimes.Length} scale times but {track.ScaleValues.Length} values.");
            }

            ValidateTimes(track, track.PositionTimes, "position");
            ValidateTimes(track, track.RotationTimes, "rotation");
            ValidateTimes(track, track.ScaleTimes, "scale");
        }

        /// <summary>
        /// Rejects key times that are not finite and strictly increasing, which the evaluator's binary search requires.
        /// </summary>
        /// <param name="track">Track owning the times, for messages.</param>
        /// <param name="times">Key times to check.</param>
        /// <param name="channel">Channel name, for messages.</param>
        static void ValidateTimes([NativeNoEscape] HierarchyAnimationTrack track, [NativeNoEscape] double[] times, string channel) {
            for (int index = 0; index < times.Length; index++) {
                if (double.IsNaN(times[index]) || double.IsInfinity(times[index])) {
                    throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has a non-finite {channel} time at key {index}.");
                } else if (index > 0 && times[index] <= times[index - 1]) {
                    throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has {channel} times that do not increase at key {index}.");
                }
            }
        }
    }
}
