using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies that the sample edit is valid and that each structural rule reports its own code.
    /// </summary>
    public class VideoEditValidatorTests {
        /// <summary>
        /// The sample edit passes against a catalog that publishes the push transition.
        /// </summary>
        [Fact]
        public void Validate_Sample_IsValid() {
            Assert.Empty(VideoEditValidator.Validate(VideoEditSamples.TwoScenes(), Catalog()));
        }

        /// <summary>
        /// Each mutation reports the expected code.
        /// </summary>
        /// <param name="mutation">Name of the mutation to apply.</param>
        /// <param name="code">Expected diagnostic code.</param>
        [Theory]
        [InlineData("duplicate_scene", "duplicate_id")]
        [InlineData("take_past_media", "invalid_take")]
        [InlineData("take_reversed", "invalid_take")]
        [InlineData("from_take_without_take", "invalid_duration")]
        [InlineData("estimate_without_seconds", "invalid_duration")]
        [InlineData("transition_on_first_scene", "invalid_entry")]
        [InlineData("transition_longer_than_scene", "invalid_entry")]
        [InlineData("unknown_transition", "invalid_entry")]
        [InlineData("bad_transition_parameter", "invalid_entry")]
        [InlineData("voice_fade_too_long", "invalid_entry")]
        [InlineData("two_moment_forms", "invalid_moment")]
        [InlineData("fraction_above_one", "invalid_moment")]
        [InlineData("unknown_layout", "invalid_layout")]
        [InlineData("viewport_outside", "invalid_layout")]
        [InlineData("zoom_too_far", "invalid_motion")]
        [InlineData("layer_media_missing", "missing_media")]
        [InlineData("transition_as_layer_effect", "invalid_effect")]
        [InlineData("unknown_animation_property", "invalid_animation")]
        [InlineData("track_unknown_scene", "invalid_track")]
        [InlineData("captions_too_many_words", "invalid_captions")]
        [InlineData("bad_lock", "invalid_lock")]
        [InlineData("unknown_text_style", "missing_text_style")]
        public void Validate_Mutation_ReportsCode(string mutation, string code) {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            Mutate(edit, mutation);
            Assert.Contains(VideoEditValidator.Validate(edit, Catalog()), diagnostic => diagnostic.Code == code);
        }

        /// <summary>
        /// Applies one named mutation to the sample.
        /// </summary>
        /// <param name="edit">Sample edit.</param>
        /// <param name="mutation">Mutation name.</param>
        static void Mutate(VideoEdit edit, string mutation) {
            VideoScene hook = edit.Scenes[0], question = edit.Scenes[1];
            switch (mutation) {
                case "duplicate_scene": question.Id = "hook"; break;
                case "take_past_media": question.Take.OutSec = 25; break;
                case "take_reversed": question.Take.OutSec = 12; break;
                case "from_take_without_take": question.Take = null; question.Overlays.Clear(); break;
                case "estimate_without_seconds": hook.Duration = new VideoSceneDuration { Mode = "estimate" }; break;
                case "transition_on_first_scene": hook.Entry = new VideoEntry { Effect = "push", DurationSec = 0.3 }; break;
                case "transition_longer_than_scene": question.Entry.DurationSec = 4; break;
                case "unknown_transition": question.Entry.Effect = "spin"; break;
                case "bad_transition_parameter": question.Entry.Parameters["Direction"] = JsonSerializer.SerializeToElement("Sideways"); break;
                case "voice_fade_too_long": question.Entry.AudioFadeSec = 3; break;
                case "two_moment_forms": question.Overlays[0].At.Sec = 1; break;
                case "fraction_above_one": hook.Layers[0].Motion.Start = new VideoMoment { Fraction = 1.5 }; break;
                case "unknown_layout": hook.Layers[0].Layout = new VideoLayout { Preset = "corner" }; break;
                case "viewport_outside": hook.Layers[0].Layout = new VideoLayout { Viewport = new VideoViewport { X = 0.5, Y = 0, Width = 0.8, Height = 1 } }; break;
                case "zoom_too_far": hook.Layers[0].Motion.ToScale = 9; break;
                case "layer_media_missing": hook.Layers[0].Media = "nothing"; break;
                case "transition_as_layer_effect": hook.Layers[0].Effects.Add(new VideoEffect { Id = "push", Version = 1 }); break;
                case "unknown_animation_property": hook.Layers[0].Animations.Add(new VideoAnimation { Property = "blur", Keyframes = [new VideoKeyframe { At = new VideoMoment { Sec = 0 } }] }); break;
                case "track_unknown_scene": edit.Tracks.Audio[0].Start.Scene = "outro"; break;
                case "captions_too_many_words": edit.Tracks.Captions.WordsPerCue = 40; break;
                case "bad_lock": question.Entry.By = "robot"; break;
                case "unknown_text_style": question.Overlays[0].Style = "neon"; break;
                default: throw new ArgumentException(mutation);
            }
        }

        /// <summary>
        /// Builds a catalog with the basic operations plus the push transition.
        /// </summary>
        /// <returns>Capabilities used by the validator tests.</returns>
        internal static MediaCapabilities Catalog() {
            MediaCapabilities catalog = MediaCapabilities.Basic();
            catalog.Effects.Add(new MediaEffectDescriptor {
                Id = "push", Version = 1, Category = "transition", MainInputRole = "From", InputRoles = ["From", "To"],
                Parameters = new(StringComparer.Ordinal) { ["Direction"] = new MediaParameterDescriptor { Type = "enum", AllowedValues = ["Left", "Right", "Up", "Down"] } }
            });
            return catalog;
        }
    }
}
