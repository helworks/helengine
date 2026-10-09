using System.Text.Json;

namespace helengine.timeline.tests {
    /// <summary>
    /// Verifies the JSON form: exact round trips, defaults for omitted or null fields, structural errors located by path,
    /// validation on parse, asset reference forms and the size cap.
    /// </summary>
    public class TimelineJsonTests {
        /// <summary>
        /// Model to JSON to model to JSON reproduces the same document, and the model keeps every value.
        /// </summary>
        [Fact]
        public void WriteThenParse_richSample_roundTripsExactly() {
            TimelineAsset original = TimelineSamples.ContrastThree();
            string first = TimelineJson.Serialize(original);

            TimelineAsset parsed = TimelineJson.Parse(first, null);
            string second = TimelineJson.Serialize(parsed);

            Assert.Equal(first, second);
            Assert.Equal("contrast_three", parsed.TimelineId);
            Assert.Equal(2, parsed.Version);
            Assert.Equal("Three-term contrast", parsed.DisplayName);
            Assert.Equal(TimelineSlotKind.Rect, parsed.Slots[4].Kind);
            Assert.Equal("First term, e.g. LEGALIZAR.", parsed.Slots[0].Description);
            Assert.Equal(1.4, parsed.Cues[1].TimeSeconds);
            TimelineTransformTrackAsset transform = Assert.IsType<TimelineTransformTrackAsset>(parsed.Tracks[1]);
            Assert.Equal(TimelineTransformMode.Offset, transform.Mode);
            Assert.Equal("a", transform.Clips[0].Cue);
            Assert.Equal(-0.1, transform.Clips[0].StartSeconds);
            Assert.Equal(-4.5, transform.Clips[0].Rotation[0].Z);
            Assert.Equal(CurveCatalog.EaseOutBack, transform.Clips[0].Scale[0].Curve);
            TimelineAudioTrackAsset audio = Assert.IsType<TimelineAudioTrackAsset>(parsed.Tracks[4]);
            Assert.Equal("assets/sfx/whoosh.wav", audio.Clips[0].Audio.RelativePath);
            Assert.Equal(0.8, audio.Clips[0].Gain);
            Assert.Equal(0.05, audio.Clips[0].ClipInSeconds);
            TimelineAnimationTrackAsset animation = Assert.IsType<TimelineAnimationTrackAsset>(parsed.Tracks[5]);
            Assert.Equal(new string('a', 32), animation.Clips[0].Animation.AssetId);
            Assert.Equal("sha256:" + new string('0', 64), animation.Clips[0].Animation.ContentHash);
            Assert.Equal(1.25, animation.Clips[1].Speed);
            TimelineEventTrackAsset events = Assert.IsType<TimelineEventTrackAsset>(parsed.Tracks[6]);
            Assert.Equal("fx", events.Name);
            Assert.Equal("0.3", events.Markers[0].Value);
            Assert.Equal("c", events.Markers[0].Cue);
            TimelineNestedTrackAsset nested = Assert.IsType<TimelineNestedTrackAsset>(parsed.Tracks[7]);
            Assert.Equal("pop", nested.Clips[0].Definition.TimelineId);
            Assert.Equal("term_c", nested.Clips[0].SlotMappings[0].Outer);
            Assert.Equal(1.5, nested.Clips[0].Speed);
        }

        /// <summary>
        /// Write produces a detached element that Parse accepts and that matches the text form.
        /// </summary>
        [Fact]
        public void WriteElement_thenParseElement_matchesTextForm() {
            JsonElement element = TimelineJson.Write(TimelineSamples.ContrastThree());

            TimelineAsset parsed = TimelineJson.Parse(element, null);

            Assert.Equal(TimelineJson.Serialize(TimelineSamples.ContrastThree()), TimelineJson.Serialize(parsed));
            Assert.Equal(TimelineJson.SchemaId, element.GetProperty("schema").GetString());
        }

        /// <summary>
        /// Omitted and null optional fields take the model defaults.
        /// </summary>
        [Fact]
        public void Parse_omittedAndNullOptionalFields_takeDefaults() {
            string json = """
                {
                  "id": "fade",
                  "version": null,
                  "duration": 1,
                  "description": null,
                  "slots": [ { "name": "target", "kind": "media" } ],
                  "tracks": [
                    { "kind": "value", "slot": "target", "channel": "opacity", "name": null,
                      "clips": [ { "start": 0, "duration": 1, "ease_in": null, "keyframes": [ { "time": 0, "value": 0 }, { "time": 1, "value": 1, "curve": null } ] } ] },
                    { "kind": "transform", "slot": "target", "clips": [ { "start": { "cue": "go" }, "duration": 0.5, "scale": [ { "time": 0, "value": [1, 1, 1] } ] } ] },
                    { "kind": "audio", "clips": [ { "start": 0, "duration": 1, "audio": { "path": "a.wav" } } ] }
                  ],
                  "cues": [ { "name": "go", "time": 0.5 } ]
                }
                """;

            TimelineAsset timeline = TimelineJson.Parse(json, null);

            Assert.Equal(1, timeline.Version);
            Assert.Equal(string.Empty, timeline.Description);
            Assert.Equal(string.Empty, timeline.DisplayName);
            Assert.Equal(string.Empty, timeline.Slots[0].Description);
            TimelineValueTrackAsset value = Assert.IsType<TimelineValueTrackAsset>(timeline.Tracks[0]);
            Assert.Equal(string.Empty, value.Name);
            Assert.Equal(0, value.Clips[0].EaseInSeconds);
            Assert.Equal(0, value.Clips[0].ClipInSeconds);
            Assert.Equal(CurveCatalog.Linear, value.Clips[0].Keyframes[0].Curve);
            Assert.Equal(CurveCatalog.Linear, value.Clips[0].Keyframes[1].Curve);
            TimelineTransformTrackAsset transform = Assert.IsType<TimelineTransformTrackAsset>(timeline.Tracks[1]);
            Assert.Equal(TimelineTransformMode.Absolute, transform.Mode);
            Assert.Equal("go", transform.Clips[0].Cue);
            Assert.Equal(0, transform.Clips[0].StartSeconds);
            Assert.Empty(transform.Clips[0].Position);
            TimelineAudioTrackAsset audio = Assert.IsType<TimelineAudioTrackAsset>(timeline.Tracks[2]);
            Assert.Equal(1, audio.Clips[0].Gain);
            Assert.Equal(string.Empty, audio.Slot);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, audio.Clips[0].Audio.SourceKind);
        }

        /// <summary>
        /// Structurally wrong documents, the JSON path of the first problem and a message fragment.
        /// </summary>
        public static TheoryData<string, string, string> StructuralErrors => new TheoryData<string, string, string> {
            { """{"id":"x","duration":1,"colour":"red"}""", "colour", "Unknown property 'colour'" },
            { """{"id":"x","duration":1,"id":"y"}""", "id", "appears twice" },
            { """{"duration":1}""", "id", "'id' is required" },
            { """{"id":"x","duration":"1s"}""", "duration", "Expected a number, found a string" },
            { """{"id":"x","version":1.5,"duration":1}""", "version", "whole number" },
            { """{"schema":"helengine.timeline.v9","id":"x","duration":1}""", "schema", "Expected schema 'helengine.timeline.v1'" },
            { """{"id":"x","duration":1,"slots":[{"name":"a","kind":"sprite"}]}""", "slots[0].kind", "entity, text, media or rect" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"tween"}]}""", "tracks[0].kind", "transform, value, activation" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"transform","markers":[]}]}""", "tracks[0].markers", "does not apply to transform tracks" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"activation","clips":[{"start":0,"duration":1,"gain":1}]}]}""", "tracks[0].clips[0].gain", "does not apply to activation clips" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"activation","clips":[{"start":"soon","duration":1}]}]}""", "tracks[0].clips[0].start", "Expected seconds or {\"cue\"" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"activation","clips":[{"start":{"offset":1},"duration":1}]}]}""", "tracks[0].clips[0].start.cue", "'cue' is required" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"transform","mode":"relative","clips":[]}]}""", "tracks[0].mode", "absolute or offset" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"transform","clips":[{"start":0,"duration":1,"scale":[{"time":0,"value":[1,1]}]}]}]}""", "tracks[0].clips[0].scale[0].value", "three numbers" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"value","channel":"opacity","clips":[{"start":0,"duration":1,"keyframes":[{"time":0}]}]}]}""", "tracks[0].clips[0].keyframes[0].value", "'value' is required" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"audio","clips":[{"start":0,"duration":1,"audio":{"path":"a.wav","asset_id":"zz"}}]}]}""", "tracks[0].clips[0].audio", "Invalid asset reference" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"timeline","clips":[{"start":0,"duration":1,"slots":["a"]}]}]}""", "tracks[0].clips[0].slots", "Expected an object mapping" },
            { """{"id":"x","duration":1,"tracks":[{"kind":"timeline","clips":[{"start":0,"duration":1,"definition":{"id":"y","duration":1,"oops":1}}]}]}""", "tracks[0].clips[0].definition.oops", "Unknown property 'oops'" },
            { """{"id":"x","duration":1,""", "", "not valid JSON" },
            { """[1,2]""", "", "Expected a timeline object, found a list" }
        };

        /// <summary>
        /// Each structural error is reported once, at its path.
        /// </summary>
        /// <param name="json">Document text.</param>
        /// <param name="path">Expected diagnostic path.</param>
        /// <param name="fragment">Text the message must contain.</param>
        [Theory]
        [MemberData(nameof(StructuralErrors))]
        public void Parse_structuralError_reportsLocatedMessage(string json, string path, string fragment) {
            TimelineFormatException exception = Assert.Throws<TimelineFormatException>(() => TimelineJson.Parse(json, null));

            TimelineDiagnostic diagnostic = Assert.Single(exception.Diagnostics);
            Assert.Equal(path, diagnostic.Path);
            Assert.Contains(fragment, diagnostic.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Parsing validates: a well-formed but invalid timeline is rejected with every validator diagnostic.
        /// </summary>
        [Fact]
        public void Parse_wellFormedButInvalid_reportsValidatorDiagnostics() {
            string json = """{"id":"x","duration":1,"tracks":[{"kind":"value","slot":"ghost","channel":"opacity","clips":[{"start":0,"duration":2,"keyframes":[{"time":0,"value":3}]}]}]}""";

            TimelineFormatException exception = Assert.Throws<TimelineFormatException>(() => TimelineJson.Parse(json, null));

            Assert.Equal(new[] { "tracks[0].slot", "tracks[0].clips[0].duration", "tracks[0].clips[0].keyframes[0].value" }, exception.Diagnostics.Select(diagnostic => diagnostic.Path).ToArray());
            Assert.Null(Record.Exception(() => TimelineJson.ReadUnvalidated(JsonDocument.Parse(json).RootElement)));
        }

        /// <summary>
        /// Generated asset references keep their source and provider through JSON.
        /// </summary>
        [Fact]
        public void WriteThenParse_generatedReference_keepsSourceAndProvider() {
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            ((TimelineAudioTrackAsset)timeline.Tracks[4]).Clips[0].Audio = TimelineAssetReferences.Create(SceneAssetReferenceSourceKind.Generated, "generated/tts/line1.wav", "tts", "line1", string.Empty);

            TimelineAsset parsed = TimelineJson.Parse(TimelineJson.Serialize(timeline), null);

            SceneAssetReference audio = ((TimelineAudioTrackAsset)parsed.Tracks[4]).Clips[0].Audio;
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, audio.SourceKind);
            Assert.Equal("tts", audio.ProviderId);
            Assert.Equal("line1", audio.AssetId);
            Assert.Equal("generated/tts/line1.wav", audio.RelativePath);
        }

        /// <summary>
        /// Documents above the size cap are rejected before parsing.
        /// </summary>
        [Fact]
        public void Parse_documentAboveCap_isRejected() {
            string json = "{\"id\":\"x\",\"duration\":1,\"description\":\"" + new string('a', TimelineJson.MaxDocumentBytes) + "\"}";

            TimelineFormatException exception = Assert.Throws<TimelineFormatException>(() => TimelineJson.Parse(json, null));

            Assert.Contains("the limit is 65536 bytes", exception.Message, StringComparison.Ordinal);
        }
    }
}
