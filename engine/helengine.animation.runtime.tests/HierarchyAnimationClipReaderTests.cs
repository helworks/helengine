namespace helengine.animation.runtime.tests {
    /// <summary>
    /// Checks the packaged <c>.hanimclip</c> layout read by <see cref="HierarchyAnimationClipReader"/>.
    /// </summary>
    public sealed class HierarchyAnimationClipReaderTests {
        /// <summary>
        /// A clip written with the header and <see cref="HierarchyAnimationClipReader.Write"/> reads back unchanged and the
        /// reader consumes the whole stream.
        /// </summary>
        [Fact]
        public void Reader_roundTripsThroughWrite() {
            HierarchyAnimationClipAsset source = HierarchyAnimationTestClips.Sample();
            source.Tracks[1].Interpolation = AnimationInterpolationMode.Step;
            using MemoryStream stream = new MemoryStream();
            HierarchyAnimationTestClips.WritePackaged(stream, source);
            stream.Position = 0;

            using HierarchyAnimationClipAsset read = HierarchyAnimationClipReader.Deserialize(stream);

            Assert.Equal(4.0, read.Duration);
            Assert.Equal(2, read.Tracks.Length);
            for (int index = 0; index < read.Tracks.Length; index++) {
                HierarchyAnimationTrack expected = source.Tracks[index];
                HierarchyAnimationTrack actual = read.Tracks[index];
                Assert.Equal(expected.TargetPath, actual.TargetPath);
                Assert.Equal(expected.Interpolation, actual.Interpolation);
                Assert.Equal(expected.PositionTimes, actual.PositionTimes);
                Assert.Equal(expected.PositionValues, actual.PositionValues);
                Assert.Equal(expected.RotationTimes, actual.RotationTimes);
                Assert.Equal(expected.ScaleTimes, actual.ScaleTimes);
                Assert.Equal(expected.ScaleValues, actual.ScaleValues);
                Assert.Equal(expected.RotationValues.Length, actual.RotationValues.Length);
                for (int key = 0; key < expected.RotationValues.Length; key++) {
                    Assert.Equal((double)expected.RotationValues[key].X, (double)actual.RotationValues[key].X, 6);
                    Assert.Equal((double)expected.RotationValues[key].Y, (double)actual.RotationValues[key].Y, 6);
                    Assert.Equal((double)expected.RotationValues[key].Z, (double)actual.RotationValues[key].Z, 6);
                    Assert.Equal((double)expected.RotationValues[key].W, (double)actual.RotationValues[key].W, 6);
                }
            }
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>
        /// A payload whose layout byte is not <see cref="HierarchyAnimationClipAsset.LayoutVersion"/> is rejected.
        /// </summary>
        [Fact]
        public void Reader_rejectsUnknownLayoutVersion() {
            using MemoryStream prefix = new MemoryStream();
            HierarchyAnimationTestClips.WritePrefix(prefix);
            using MemoryStream stream = new MemoryStream();
            HierarchyAnimationTestClips.WritePackaged(stream, HierarchyAnimationTestClips.Sample());
            byte[] bytes = stream.ToArray();
            bytes[prefix.Length] = HierarchyAnimationClipAsset.LayoutVersion + 1;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => HierarchyAnimationClipReader.Deserialize(new MemoryStream(bytes)));

            Assert.Contains("layout version", error.Message);
        }

        /// <summary>
        /// The three-byte numeric policy marker the editor asset writer puts after the header is read and validated before
        /// the payload; a file that omits it is rejected instead of having its first marker byte misread as the layout.
        /// </summary>
        [Fact]
        public void Reader_readsPolicyMarkerBeforePayload() {
            using MemoryStream header = new MemoryStream();
            HierarchyAnimationTestClips.WriteHeader(header);
            using MemoryStream prefix = new MemoryStream();
            HierarchyAnimationTestClips.WritePrefix(prefix);
            Assert.Equal(3, prefix.Length - header.Length);

            using MemoryStream withMarker = new MemoryStream();
            HierarchyAnimationTestClips.WritePackaged(withMarker, HierarchyAnimationTestClips.Sample());
            withMarker.Position = 0;
            using HierarchyAnimationClipAsset read = HierarchyAnimationClipReader.Deserialize(withMarker);
            Assert.Equal(2, read.Tracks.Length);
            Assert.Equal(withMarker.Length, withMarker.Position);

            using MemoryStream withoutMarker = new MemoryStream();
            HierarchyAnimationTestClips.WriteHeader(withoutMarker);
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(withoutMarker, EngineBinaryEndianness.LittleEndian)) {
                HierarchyAnimationClipReader.Write(writer, HierarchyAnimationTestClips.Sample());
            }
            withoutMarker.Position = 0;
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => HierarchyAnimationClipReader.Deserialize(withoutMarker));
            Assert.Contains("policy", error.Message);
        }

        /// <summary>
        /// Key times outside <c>[0, Duration]</c> are rejected, both when reading (the stored duration is patched to 1 s
        /// while keys reach 4 s) and when writing.
        /// </summary>
        [Fact]
        public void Reader_rejectsKeyTimesOutsideDuration() {
            using MemoryStream prefix = new MemoryStream();
            HierarchyAnimationTestClips.WritePrefix(prefix);
            using MemoryStream stream = new MemoryStream();
            HierarchyAnimationTestClips.WritePackaged(stream, HierarchyAnimationTestClips.Sample());
            byte[] bytes = stream.ToArray();
            BitConverter.GetBytes(1.0).CopyTo(bytes, (int)prefix.Length + 1);

            InvalidOperationException readError = Assert.Throws<InvalidOperationException>(
                () => HierarchyAnimationClipReader.Deserialize(new MemoryStream(bytes)));
            Assert.Contains("outside the clip range", readError.Message);

            HierarchyAnimationClipAsset clip = HierarchyAnimationTestClips.Sample();
            clip.Duration = 1;
            InvalidOperationException writeError = Assert.Throws<InvalidOperationException>(
                () => HierarchyAnimationTestClips.WritePackaged(new MemoryStream(), clip));
            Assert.Contains("outside the clip range", writeError.Message);
        }
    }
}
