namespace helengine.animation.runtime.tests {
    /// <summary>
    /// Builds the sample hierarchy clip the tests play and writes clips as packaged <c>.hanimclip</c> files.
    /// </summary>
    static class HierarchyAnimationTestClips {
        /// <summary>
        /// Relative content path the sample clip is written to.
        /// </summary>
        public const string ClipPath = "anim/arm.hanimclip";

        /// <summary>
        /// Creates a four-second clip: track 0 (<c>Arm</c>) moves from the origin to (2, 4, 6) between 0 s and 2 s;
        /// track 1 (<c>Arm/Hand</c>) turns from identity to 90 degrees about Z and scales from 1 to 3 between 0 s and 4 s.
        /// </summary>
        /// <returns>The clip.</returns>
        public static HierarchyAnimationClipAsset Sample() {
            HierarchyAnimationTrack arm = new HierarchyAnimationTrack {
                TargetPath = "Arm",
                PositionTimes = new double[] { 0, 2 },
                PositionValues = new float3[] { new float3(0, 0, 0), new float3(2, 4, 6) },
                Interpolation = AnimationInterpolationMode.Linear
            };
            HierarchyAnimationTrack hand = new HierarchyAnimationTrack {
                TargetPath = "Arm/Hand",
                RotationTimes = new double[] { 0, 4 },
                RotationValues = new float4[] { float4.Identity, QuarterTurnAboutZ() },
                ScaleTimes = new double[] { 0, 4 },
                ScaleValues = new float3[] { new float3(1, 1, 1), new float3(3, 3, 3) },
                Interpolation = AnimationInterpolationMode.Linear
            };
            return new HierarchyAnimationClipAsset {
                Duration = 4,
                Tracks = new HierarchyAnimationTrack[] { arm, hand }
            };
        }

        /// <summary>
        /// Returns the rotation of 90 degrees about the Z axis.
        /// </summary>
        /// <returns>The quaternion.</returns>
        public static float4 QuarterTurnAboutZ() {
            return new float4(0, 0, Math.Sin(Math.PI / 4), Math.Cos(Math.PI / 4));
        }

        /// <summary>
        /// Writes one clip as a packaged file: the HELE header for value kind
        /// <see cref="EditorAssetBinaryValueKind.HierarchyAnimationClipAsset"/>, then the reader's payload.
        /// </summary>
        /// <param name="stream">Destination stream.</param>
        /// <param name="clip">Clip to write.</param>
        public static void WritePackaged(Stream stream, HierarchyAnimationClipAsset clip) {
            WriteHeader(stream);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            HierarchyAnimationClipReader.Write(writer, clip);
        }

        /// <summary>
        /// Writes the little-endian HELE header of a packaged hierarchy animation clip.
        /// </summary>
        /// <param name="stream">Destination stream.</param>
        public static void WriteHeader(Stream stream) {
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                PackagedAssetBinarySerializer.CurrentVersion,
                PackagedAssetBinarySerializer.FormatId,
                (ushort)PackagedAssetBinarySerializer.RecordKind,
                (ushort)EditorAssetBinaryValueKind.HierarchyAnimationClipAsset));
        }
    }
}
