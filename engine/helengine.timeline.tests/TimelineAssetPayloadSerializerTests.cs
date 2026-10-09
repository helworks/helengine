using helengine.files;

namespace helengine.timeline.tests {
    /// <summary>
    /// Verifies that timelines survive the HELE editor asset format exactly and that the timeline module, not core or
    /// helengine.files, registers the serializer for value kind 14.
    /// </summary>
    public class TimelineAssetPayloadSerializerTests {
        /// <summary>
        /// Registration makes the timeline serializer resolvable in both directions and is idempotent.
        /// </summary>
        [Fact]
        public void Register_resolvesTimelineAssetsToValueKind14() {
            TimelineSerialization.Register();
            int count = EditorAssetPayloadSerializerRegistry.Count;
            TimelineSerialization.Register();

            IEditorAssetPayloadSerializer serializer = EditorAssetPayloadSerializerRegistry.FindByAsset(new TimelineAsset());
            Assert.IsType<TimelineAssetPayloadSerializer>(serializer);
            Assert.Equal((EditorAssetBinaryValueKind)14, serializer.ValueKind);
            Assert.Same(serializer, EditorAssetPayloadSerializerRegistry.FindByValueKind(EditorAssetBinaryValueKind.TimelineAsset));
            Assert.Equal(count, EditorAssetPayloadSerializerRegistry.Count);
        }

        /// <summary>
        /// Every value of the rich sample, including references and the inline nested definition, survives a binary round
        /// trip; the JSON forms of both sides are identical.
        /// </summary>
        [Fact]
        public void SerializeThenDeserialize_richSample_preservesEverything() {
            TimelineSerialization.Register();
            TimelineAsset original = TimelineSamples.ContrastThree();
            original.Id = "timelines/contrast_three";
            original.AuthoringAssetId = new string('b', 32);
            original.Tracks.Add(TimelineSamples.ReferenceTrack("term_b", "assets/timelines/pop.htimeline", 0.6));
            ((TimelineAudioTrackAsset)original.Tracks[4]).Clips[0].Audio = TimelineAssetReferences.Create(SceneAssetReferenceSourceKind.Generated, "generated/tts/line1.wav", "tts", "line1", string.Empty);

            TimelineAsset restored = RoundTrip(original);

            Assert.Equal(TimelineJson.Serialize(original), TimelineJson.Serialize(restored));
            Assert.Equal("timelines/contrast_three", restored.Id);
            Assert.Equal(new string('b', 32), restored.AuthoringAssetId);
            Assert.Equal(original.RuntimeAssetId, restored.RuntimeAssetId);
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, ((TimelineAudioTrackAsset)restored.Tracks[4]).Clips[0].Audio.SourceKind);
            Assert.Equal("assets/timelines/pop.htimeline", ((TimelineNestedTrackAsset)restored.Tracks[8]).Clips[0].Timeline.RelativePath);
            Assert.Null(((TimelineNestedTrackAsset)restored.Tracks[8]).Clips[0].Definition);
        }

        /// <summary>
        /// Doubles are stored bit for bit, so values JSON or floats would round survive unchanged.
        /// </summary>
        [Fact]
        public void SerializeThenDeserialize_awkwardDoubles_areExact() {
            TimelineSerialization.Register();
            TimelineAsset original = TimelineSamples.Pop();
            original.DurationSeconds = 0.1 + 0.2;
            ((TimelineValueTrackAsset)original.Tracks[1]).Clips[0].Keyframes[1].Value = 1.0 / 3.0;

            TimelineAsset restored = RoundTrip(original);

            Assert.Equal(BitConverter.DoubleToInt64Bits(0.1 + 0.2), BitConverter.DoubleToInt64Bits(restored.DurationSeconds));
            Assert.Equal(BitConverter.DoubleToInt64Bits(1.0 / 3.0), BitConverter.DoubleToInt64Bits(((TimelineValueTrackAsset)restored.Tracks[1]).Clips[0].Keyframes[1].Value));
        }

        /// <summary>
        /// Structurally broken timelines are rejected before any byte is written.
        /// </summary>
        [Fact]
        public void Serialize_missingClipList_isRejected() {
            TimelineSerialization.Register();
            TimelineAsset timeline = TimelineSamples.Pop();
            ((TimelineTransformTrackAsset)timeline.Tracks[0]).Clips = null;
            using MemoryStream stream = new MemoryStream();

            Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Serialize(stream, timeline));
            Assert.Equal(0, stream.Length);
        }

        /// <summary>
        /// TimelineFile saves and loads a valid timeline and refuses to save an invalid one.
        /// </summary>
        [Fact]
        public void TimelineFile_saveThenLoad_roundTripsAndValidates() {
            string directory = Path.Combine(AppContext.BaseDirectory, "timeline-file-test");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "contrast.htimeline");
            TimelineAsset original = TimelineSamples.ContrastThree();

            TimelineFile.Save(path, original, null);
            TimelineAsset loaded = TimelineFile.Load(path, null);

            Assert.Equal(TimelineJson.Serialize(original), TimelineJson.Serialize(loaded));
            original.Version = 0;
            Assert.Throws<TimelineFormatException>(() => TimelineFile.Save(path, original, null));
            Assert.Throws<ArgumentException>(() => TimelineFile.Save(Path.Combine(directory, "contrast.json"), TimelineSamples.Pop(), null));
        }

        /// <summary>
        /// Serializes and deserializes one timeline through the editor asset format.
        /// </summary>
        /// <param name="timeline">Timeline to round-trip.</param>
        /// <returns>The deserialized timeline.</returns>
        static TimelineAsset RoundTrip(TimelineAsset timeline) {
            using MemoryStream stream = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(stream, timeline);
            stream.Position = 0;
            return Assert.IsType<TimelineAsset>(EditorAssetBinarySerializer.Deserialize(stream));
        }
    }
}
