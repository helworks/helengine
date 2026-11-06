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
            using MemoryStream header = new MemoryStream();
            HierarchyAnimationTestClips.WriteHeader(header);
            using MemoryStream stream = new MemoryStream();
            HierarchyAnimationTestClips.WritePackaged(stream, HierarchyAnimationTestClips.Sample());
            byte[] bytes = stream.ToArray();
            bytes[header.Length] = HierarchyAnimationClipAsset.LayoutVersion + 1;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => HierarchyAnimationClipReader.Deserialize(new MemoryStream(bytes)));

            Assert.Contains("layout version", error.Message);
        }
    }
}
