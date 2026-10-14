using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies speaker background blur and replacement: validation, the two-layer compilation with a luma matte, the
    /// pending and conflict diagnostics, silent duplicated layers and the human lock surviving a replan.
    /// </summary>
    public class VideoBackgroundTests {
        /// <summary>
        /// A blurred background compiles into a blurred cover copy of the take drawn under the matted front layer.
        /// </summary>
        [Fact]
        public void Compile_Blur_AddsBlurredBackgroundUnderMattedFront() {
            VideoEdit edit = Sample("blur");
            edit.Scenes[1].Background.Radius = 40;

            VideoCompileResult result = Compile(edit);

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer back = result.Composition.Layers.Single(layer => layer.Id == "question-take-background");
            VisualLayer front = result.Composition.Layers.Single(layer => layer.Id == "question-take");
            Assert.Equal("take", back.MediaId);
            Assert.Equal("cover", back.Fit);
            Assert.Null(back.Mask);
            Assert.Equal(12.3, back.SourceIn.ToSeconds(), 6);
            Assert.Equal(front.End, back.End);
            MediaEffect blur = Assert.Single(back.Effects);
            Assert.Equal("gaussian-blur", blur.Id);
            Assert.Equal(40, blur.Parameters["Radius"].GetDouble());
            Assert.Equal("take-matte", front.Mask.MediaId);
            Assert.Equal("luma", front.Mask.Channel);
            Assert.Equal("take", front.MediaId);
            string[] members = result.Composition.Layers.Single(layer => layer.Id == "scene-question").Members.ToArray();
            Assert.True(Array.IndexOf(members, back.Id) < Array.IndexOf(members, front.Id));
            Assert.Equal(front.Order, back.Order);
            Assert.Empty(CompositionValidator.Validate(result.Composition, Catalog()));
        }

        /// <summary>
        /// A replaced background with an image compiles into a static cover image layer under the matted take.
        /// </summary>
        [Fact]
        public void Compile_ReplaceWithImage_AddsImageBackground() {
            VideoEdit edit = Sample("replace");
            edit.Scenes[1].Background.Media = "post";

            VideoCompileResult result = Compile(edit);

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer back = result.Composition.Layers.Single(layer => layer.Id == "question-take-background");
            Assert.Equal("post", back.MediaId);
            Assert.Equal("cover", back.Fit);
            Assert.Empty(back.Effects);
            Assert.False(back.HoldLastFrame);
            Assert.NotNull(result.Composition.Layers.Single(layer => layer.Id == "question-take").Mask);
        }

        /// <summary>
        /// A replacement video shorter than the scene holds its last frame, like a media layer does.
        /// </summary>
        [Fact]
        public void Compile_ReplaceWithShortVideo_HoldsLastFrame() {
            VideoEdit edit = Sample("replace");
            edit.Media.Add(new VideoMedia { Id = "loop", Kind = "video", Path = "media/loop.mp4", Sha256 = new string('d', 64), Width = 540, Height = 960, DurationSec = 1 });
            edit.Scenes[1].Background.Media = "loop";

            VisualLayer back = Compile(edit).Composition.Layers.Single(layer => layer.Id == "question-take-background");

            Assert.True(back.HoldLastFrame);
            Assert.Equal(1d, back.SourceOut.ToSeconds(), 6);
        }

        /// <summary>
        /// Without a matte the layer renders normally and a pending diagnostic explains the wait.
        /// </summary>
        [Fact]
        public void Compile_WithoutMatte_IsPendingAndKeepsOriginalBackground() {
            VideoEdit edit = Sample("blur");
            edit.Media.Single(media => media.Id == "take").Matte = null;

            VideoCompileResult result = Compile(edit);

            Assert.Contains(result.Diagnostics, item => item.Code == "background_matte_pending" && item.Severity == VideoDiagnosticSeverity.Pending);
            Assert.DoesNotContain(result.Composition.Layers, layer => layer.Id.EndsWith("-background"));
            Assert.Null(result.Composition.Layers.Single(layer => layer.Id == "question-take").Mask);
        }

        /// <summary>
        /// The despilled foreground replaces the take media inside the matte.
        /// </summary>
        [Fact]
        public void Compile_ForegroundMediaIsUsedInsideTheMatte() {
            VideoEdit edit = Sample("blur");
            edit.Media.Add(new VideoMedia { Id = "take-fg", Kind = "video", Path = "media/take-fg.mp4", Sha256 = new string('f', 64), Width = 540, Height = 960, DurationSec = 20 });
            edit.Media.Single(media => media.Id == "take").Foreground = "take-fg";

            VideoCompileResult result = Compile(edit);

            VisualLayer front = result.Composition.Layers.Single(layer => layer.Id == "question-take");
            Assert.Equal("take-fg", front.MediaId);
            Assert.Equal(12.3, front.SourceIn.ToSeconds(), 6);
            Assert.Equal("take", result.Composition.Layers.Single(layer => layer.Id == "question-take-background").MediaId);
        }

        /// <summary>
        /// A layer that already has its own mask keeps it and the background is skipped with a warning.
        /// </summary>
        [Fact]
        public void Compile_LayerWithOwnMask_WarnsAndSkipsBackground() {
            VideoEdit edit = Sample("blur");
            edit.Scenes[1].Layers = [new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Mask = new VideoMask { Media = "take-matte", Channel = "alpha" } }];

            VideoCompileResult result = Compile(edit);

            Assert.Contains(result.Diagnostics, item => item.Code == "background_mask_conflict" && item.Severity == VideoDiagnosticSeverity.Warning);
            Assert.DoesNotContain(result.Composition.Layers, layer => layer.Id.EndsWith("-background"));
            Assert.Equal("alpha", result.Composition.Layers.Single(layer => layer.Id == "question-take").Mask.Channel);
        }

        /// <summary>
        /// The background copy is silent: voice audio comes only from the one take clip.
        /// </summary>
        [Fact]
        public void Compile_BackgroundDoesNotDuplicateAudio() {
            VideoEdit edit = Sample("blur");
            edit.Media.Single(media => media.Id == "take").HasAudio = true;

            CompositionDocument composition = Compile(edit).Composition;

            Assert.Single(composition.AudioClips, clip => clip.MediaId == "take");
            Assert.DoesNotContain(composition.AudioClips, clip => clip.Id.Contains("background"));
        }

        /// <summary>
        /// Each background mistake reports its code.
        /// </summary>
        /// <param name="mutation">Mutation name.</param>
        /// <param name="code">Expected diagnostic code.</param>
        [Theory]
        [InlineData("bad_mode", "invalid_background")]
        [InlineData("radius_negative", "invalid_background")]
        [InlineData("radius_too_big", "invalid_background")]
        [InlineData("radius_nan", "invalid_background")]
        [InlineData("replace_without_media", "invalid_background")]
        [InlineData("replace_with_audio", "invalid_background")]
        [InlineData("no_take", "background_without_take")]
        [InlineData("matte_missing", "invalid_matte")]
        [InlineData("matte_is_image", "invalid_matte")]
        [InlineData("foreground_missing", "invalid_matte")]
        [InlineData("bad_lock", "invalid_lock")]
        public void Validate_Mutation_ReportsCode(string mutation, string code) {
            VideoEdit edit = Sample("blur");
            VideoSceneBackground background = edit.Scenes[1].Background;
            switch (mutation) {
                case "bad_mode": background.Mode = "sepia"; break;
                case "radius_negative": background.Radius = -1; break;
                case "radius_too_big": background.Radius = 97; break;
                case "radius_nan": background.Radius = double.NaN; break;
                case "replace_without_media": background.Mode = "replace"; break;
                case "replace_with_audio": background.Mode = "replace"; background.Media = "music"; break;
                case "no_take": edit.Scenes[0].Background = new VideoSceneBackground { Mode = "blur" }; break;
                case "matte_missing": edit.Media.Single(media => media.Id == "take").Matte = "nothing"; break;
                case "matte_is_image": edit.Media.Single(media => media.Id == "take").Matte = "post"; break;
                case "foreground_missing": edit.Media.Single(media => media.Id == "take").Foreground = "nothing"; break;
                case "bad_lock": background.By = "robot"; break;
                default: throw new ArgumentException(mutation);
            }
            Assert.Contains(VideoEditValidator.Validate(edit, Catalog()), diagnostic => diagnostic.Code == code);
        }

        /// <summary>
        /// A valid blur and a valid replace background raise no validation error.
        /// </summary>
        [Fact]
        public void Validate_ValidBackgrounds_AreAccepted() {
            VideoEdit edit = Sample("replace");
            edit.Scenes[1].Background.Media = "post";
            Assert.Empty(VideoEditValidator.Validate(edit, Catalog()));
            Assert.Empty(VideoEditValidator.Validate(Sample("blur"), Catalog()));
        }

        /// <summary>
        /// The new fields round-trip through the snake_case JSON and appear in the schema.
        /// </summary>
        [Fact]
        public void Json_RoundTripsBackgroundAndMatte() {
            VideoEdit edit = Sample("blur");
            edit.Scenes[1].Background.By = "human";

            string json = VideoEditJson.Serialize(edit);
            VideoEdit parsed = VideoEditJson.Parse(json);

            Assert.Contains("\"background\"", json);
            Assert.Contains("\"matte\": \"take-matte\"", json);
            Assert.Equal("blur", parsed.Scenes[1].Background.Mode);
            Assert.Equal("human", parsed.Scenes[1].Background.By);
            Assert.Equal("take-matte", parsed.Media.Single(media => media.Id == "take").Matte);
            string schema = VideoEditSchema.Create().GetRawText();
            Assert.Contains("\"matte\"", schema);
            Assert.Contains("\"foreground\"", schema);
            Assert.Contains("\"background\"", schema);
        }

        /// <summary>
        /// A human-locked background survives a replan that proposes another one.
        /// </summary>
        [Fact]
        public void Replan_HumanBackgroundSurvives() {
            VideoEdit current = Sample("blur");
            current.Scenes[1].Background.By = "human";
            current.Scenes[1].Background.Radius = 60;
            VideoEdit proposal = Sample("original");

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal("blur", merged.Scenes[1].Background.Mode);
            Assert.Equal(60, merged.Scenes[1].Background.Radius);
            Assert.Equal("original", proposal.Scenes[1].Background.Mode);
        }

        /// <summary>
        /// An unlocked background follows the proposal.
        /// </summary>
        [Fact]
        public void Replan_UnlockedBackgroundFollowsProposal() {
            VideoEdit merged = VideoEditMerge.Replan(Sample("blur"), Sample("original"));

            Assert.Equal("original", merged.Scenes[1].Background.Mode);
        }

        /// <summary>
        /// Builds the sample edit with a matted take media and a background on the take scene.
        /// </summary>
        /// <param name="mode">Background mode of the take scene.</param>
        /// <returns>Sample edit.</returns>
        static VideoEdit Sample(string mode) {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Media.Add(new VideoMedia { Id = "take-matte", Kind = "video", Path = "media/take-matte.mp4", Sha256 = new string('e', 64), Width = 270, Height = 480, DurationSec = 20 });
            edit.Media.Single(media => media.Id == "take").Matte = "take-matte";
            edit.Scenes[1].Background = new VideoSceneBackground { Mode = mode };
            return edit;
        }

        /// <summary>
        /// Compiles an edit with the catalog extended by the gaussian blur.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <returns>Compile result.</returns>
        static VideoCompileResult Compile(VideoEdit edit) {
            return VideoEditCompiler.Compile(edit, new VideoCompileContext { Capabilities = Catalog() });
        }

        /// <summary>
        /// Builds the validator catalog plus the gaussian blur layer effect.
        /// </summary>
        /// <returns>Capabilities.</returns>
        static MediaCapabilities Catalog() {
            MediaCapabilities catalog = VideoEditValidatorTests.Catalog();
            catalog.Effects.Add(new MediaEffectDescriptor {
                Id = "gaussian-blur", Version = 1, Category = "layer", MainInputRole = "Source", InputRoles = ["Source"],
                Parameters = new(StringComparer.Ordinal) { ["Radius"] = new MediaParameterDescriptor { Type = "number", Minimum = 0, Maximum = 96 } }
            });
            return catalog;
        }

        /// <summary>
        /// Formats the diagnostics of a result for assertion messages.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <returns>One line per diagnostic.</returns>
        static string Describe(VideoCompileResult result) {
            return string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message));
        }
    }
}
