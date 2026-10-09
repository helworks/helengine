using System.Text.Json;
using System.Text.Json.Nodes;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the overlay timeline contract: the JSON round trip and the validator rules (definition, slots, bindings,
    /// channels, tracks and cues), each reported with a readable message at a precise path.
    /// </summary>
    public class VideoTimelineValidatorTests {
        /// <summary>
        /// Path of the sample overlay timeline.
        /// </summary>
        const string TimelinePath = "scenes[0].overlays[0].timeline";

        /// <summary>
        /// The sample timeline edit is valid.
        /// </summary>
        [Fact]
        public void Validate_Sample_HasNoErrors() {
            Assert.Empty(VideoEditValidator.Validate(TimelineEditSamples.Contrast(), GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// Writing, reading and writing again keeps the timeline, its bindings and cues, in snake_case.
        /// </summary>
        [Fact]
        public void Json_RoundTripsTheOverlayTimeline() {
            string first = VideoEditJson.Serialize(TimelineEditSamples.Contrast());
            VideoEdit parsed = VideoEditJson.Parse(first);
            string second = VideoEditJson.Serialize(parsed);

            Assert.Equal(first, second);
            VideoOverlayTimeline timeline = parsed.Scenes[0].Overlays[0].Timeline;
            Assert.Equal("contrast_three_terms_video", timeline.Definition.Value.GetProperty("id").GetString());
            Assert.Equal("term_c", timeline.Bindings["strike"].Rect.Match);
            Assert.Equal(0.12, timeline.Bindings["sep_ab"].Size);
            Assert.Equal("tratar", timeline.Cues["c"].Word);
            Assert.Contains("\"bindings\"", first);
            Assert.Contains("\"match\": \"term_c\"", first);
            Assert.Empty(VideoEditValidator.Validate(parsed, GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// A library reference survives the round trip, and an unknown binding property is rejected.
        /// </summary>
        [Fact]
        public void Json_ReadsLibraryReferencesAndRejectsUnknownBindingFields() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Overlays[0].Timeline.Definition = null;
            edit.Scenes[0].Overlays[0].Timeline.Library = new VideoTimelineLibraryReference { Id = "contrast_three_terms", Version = 2 };
            string json = VideoEditJson.Serialize(edit);

            VideoTimelineLibraryReference library = VideoEditJson.Parse(json).Scenes[0].Overlays[0].Timeline.Library;
            Assert.Equal("contrast_three_terms", library.Id);
            Assert.Equal(2, library.Version);
            Assert.ThrowsAny<Exception>(() => VideoEditJson.Parse(json.Replace("\"match\"", "\"matches\"")));
        }

        /// <summary>
        /// Each mutation reports the expected code at the expected path.
        /// </summary>
        /// <param name="mutation">Name of the mutation to apply.</param>
        /// <param name="code">Expected diagnostic code.</param>
        /// <param name="path">Expected path, relative to the overlay timeline (empty for the timeline itself).</param>
        [Theory]
        [InlineData("library_only", "unresolved_timeline", ".library")]
        [InlineData("both_forms", "invalid_timeline", "")]
        [InlineData("no_definition", "invalid_timeline", ".definition")]
        [InlineData("graphic_too", "invalid_overlay", "-overlay")]
        [InlineData("missing_duration", "invalid_timeline", ".definition.duration")]
        [InlineData("bad_keyframe_order", "invalid_timeline", ".definition.tracks[1].clips[0].scale[1].time")]
        [InlineData("nested_error", "invalid_timeline", ".definition.tracks[12].clips[0].definition.tracks[0].clips[0].rotation[0].curve")]
        [InlineData("entity_slot", "invalid_timeline", ".definition.slots[0].kind")]
        [InlineData("unbound_slot", "invalid_timeline", ".bindings")]
        [InlineData("unknown_binding", "invalid_timeline", ".bindings.term_z")]
        [InlineData("kind_mismatch", "invalid_timeline", ".bindings.term_a")]
        [InlineData("two_forms", "invalid_timeline", ".bindings.term_b")]
        [InlineData("blank_text", "invalid_timeline", ".bindings.term_a.text")]
        [InlineData("size_range", "invalid_timeline", ".bindings.term_a.size")]
        [InlineData("text_color", "invalid_timeline", ".bindings.term_a.color")]
        [InlineData("rect_color", "invalid_timeline", ".bindings.strike.rect.color")]
        [InlineData("rect_match", "invalid_timeline", ".bindings.strike.rect.match")]
        [InlineData("rect_width", "invalid_timeline", ".bindings.strike.rect.width")]
        [InlineData("unknown_media", "invalid_timeline", ".bindings.term_a.media")]
        [InlineData("audio_media", "invalid_timeline", ".bindings.term_a.media")]
        [InlineData("unknown_channel", "invalid_timeline", ".definition.tracks[4].channel")]
        [InlineData("reveal_on_text", "invalid_timeline", ".definition.tracks[4].channel")]
        [InlineData("audio_track", "invalid_timeline", ".definition.tracks[17].kind")]
        [InlineData("nested_reference", "invalid_timeline", ".definition.tracks[12].clips[0].timeline")]
        [InlineData("unknown_cue", "invalid_timeline", ".cues.z")]
        [InlineData("bad_cue_moment", "invalid_moment", ".cues.a")]
        public void Validate_ReportsTimelineErrors(string mutation, string code, string path) {
            VideoEdit edit = TimelineEditSamples.Contrast();
            VideoOverlay overlay = edit.Scenes[0].Overlays[0];
            VideoOverlayTimeline timeline = overlay.Timeline;
            switch (mutation) {
                case "library_only": timeline.Definition = null; timeline.Library = new VideoTimelineLibraryReference { Id = "contrast" }; break;
                case "both_forms": timeline.Library = new VideoTimelineLibraryReference { Id = "contrast" }; break;
                case "no_definition": timeline.Definition = null; break;
                case "graphic_too": overlay.Text = "x"; overlay.Graphic = GraphicEditSamples.ContrastChain().Scenes[0].Overlays[0].Graphic; break;
                case "missing_duration": TimelineEditSamples.ChangeDefinition(edit, node => node.Remove("duration")); break;
                case "bad_keyframe_order": TimelineEditSamples.ChangeDefinition(edit, node => node["tracks"][1]["clips"][0]["scale"][1]["time"] = 0); break;
                case "nested_error": TimelineEditSamples.ChangeDefinition(edit, node => node["tracks"][12]["clips"][0]["definition"]["tracks"][0]["clips"][0]["rotation"][0]["curve"] = "bounce.v1"); break;
                case "entity_slot": TimelineEditSamples.ChangeDefinition(edit, node => node["slots"][0]["kind"] = "entity"); timeline.Bindings.Remove("term_a"); break;
                case "unbound_slot": timeline.Bindings.Remove("term_b"); break;
                case "unknown_binding": timeline.Bindings["term_z"] = new VideoTimelineBinding { Text = "x" }; break;
                case "kind_mismatch": timeline.Bindings["term_a"] = new VideoTimelineBinding { Rect = new VideoTimelineRect { Color = JsonSerializer.SerializeToElement("#FFFFFF") } }; break;
                case "two_forms": timeline.Bindings["term_b"].Media = "take"; break;
                case "blank_text": timeline.Bindings["term_a"].Text = " "; break;
                case "size_range": timeline.Bindings["term_a"].Size = 1.5; break;
                case "text_color": timeline.Bindings["term_a"].Color = JsonSerializer.SerializeToElement("yellow"); break;
                case "rect_color": timeline.Bindings["strike"].Rect.Color = JsonSerializer.SerializeToElement(new[] { 1.0, 2.0, 0.0 }); break;
                case "rect_match": timeline.Bindings["strike"].Rect.Match = "strike"; break;
                case "rect_width": timeline.Bindings["strike"].Rect.Match = null; timeline.Bindings["strike"].Rect.Width = 3; break;
                case "unknown_media": SlotKind(edit, 0, "media"); timeline.Bindings["term_a"] = new VideoTimelineBinding { Media = "nope" }; break;
                case "audio_media": SlotKind(edit, 0, "media"); edit.Media[0].Kind = "audio"; timeline.Bindings["term_a"] = new VideoTimelineBinding { Media = "take" }; break;
                case "unknown_channel": TimelineEditSamples.ChangeDefinition(edit, node => node["tracks"][4]["channel"] = "intensity"); break;
                case "reveal_on_text": TimelineEditSamples.ChangeDefinition(edit, node => node["tracks"][4]["channel"] = "reveal"); break;
                case "audio_track": TimelineEditSamples.ChangeDefinition(edit, node => node["tracks"].AsArray().Add(JsonNode.Parse("{\"kind\":\"audio\",\"clips\":[{\"start\":0,\"duration\":0.5,\"audio\":{\"path\":\"assets/sfx/pop.wav\"}}]}"))); break;
                case "nested_reference": TimelineEditSamples.ChangeDefinition(edit, node => { JsonObject clip = node["tracks"][12]["clips"][0].AsObject(); clip.Remove("definition"); clip["timeline"] = JsonNode.Parse("{\"path\":\"assets/timelines/pop.htimeline\"}"); }); break;
                case "unknown_cue": timeline.Cues["z"] = new VideoMoment { Sec = 1 }; break;
                case "bad_cue_moment": timeline.Cues["a"] = new VideoMoment { Sec = 1, Word = "legalizar" }; break;
            }

            IReadOnlyList<VideoDiagnostic> diagnostics = VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog());

            string expected = path == "-overlay" ? "scenes[0].overlays[0]" : TimelinePath + path;
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Path == expected && diagnostic.Severity == VideoDiagnosticSeverity.Error);
            Assert.All(diagnostics, diagnostic => Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message)));
        }

        /// <summary>
        /// Messages name what to do: an unbound slot shows the binding it needs and an unknown cue lists the cues.
        /// </summary>
        [Fact]
        public void Validate_MessagesNameTheFix() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Overlays[0].Timeline.Bindings.Remove("strike");
            edit.Scenes[0].Overlays[0].Timeline.Cues["d"] = new VideoMoment { Sec = 1 };

            IReadOnlyList<VideoDiagnostic> diagnostics = VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog());

            Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("bindings.strike = {\"rect\": {\"color\"", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("declared cues: a, b, c", StringComparison.Ordinal));
        }

        /// <summary>
        /// Publishing timeline support fills the catalog with what the validator and compiler accept.
        /// </summary>
        [Fact]
        public void Capabilities_PublishTimelineSupport() {
            helengine.media.MediaCapabilities catalog = GraphicEditSamples.Catalog();

            VideoTimelineCapabilities.Publish(catalog);

            Assert.Equal("helengine.timeline.v1", catalog.Timeline.Format);
            Assert.Equal(VideoTimelineValidator.SlotKinds, catalog.Timeline.SlotKinds);
            Assert.Equal(["transform", "value", "activation", "event", "timeline"], catalog.Timeline.TrackKinds);
            Assert.Equal(catalog.Curves, catalog.Timeline.Curves);
            Assert.Equal(0.5, catalog.Timeline.DefaultMediaSize);
            Assert.Equal(2, catalog.Describe().GetProperty("timeline").GetProperty("value_channels").GetArrayLength());
        }

        /// <summary>
        /// Changes the kind of one slot of the sample definition.
        /// </summary>
        /// <param name="edit">Edit to change.</param>
        /// <param name="slot">Slot index.</param>
        /// <param name="kind">New slot kind.</param>
        static void SlotKind(VideoEdit edit, int slot, string kind) {
            TimelineEditSamples.ChangeDefinition(edit, node => node["slots"][slot]["kind"] = kind);
        }
    }
}
