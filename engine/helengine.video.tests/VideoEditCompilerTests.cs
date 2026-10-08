using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the composition produced from the sample edit: scene timing, layout and motion expansion, transitions with
    /// last-frame holds, captions from take words, word-anchored overlays, audio tracks and voice fades.
    /// </summary>
    public class VideoEditCompilerTests {
        /// <summary>
        /// The sample compiles into contiguous scene groups that the engine's own validator accepts.
        /// </summary>
        [Fact]
        public void Compile_Sample_ProducesValidComposition() {
            VideoCompileResult result = Compile(VideoEditSamples.TwoScenes());

            Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message)));
            CompositionDocument composition = result.Composition;
            Assert.Equal(6d, composition.Duration.ToSeconds(), 6);
            Assert.Empty(CompositionValidator.Validate(composition, VideoEditValidatorTests.Catalog()));
            VisualLayer question = composition.Layers.Single(layer => layer.Id == "scene-question");
            Assert.Equal(3d, question.Start.ToSeconds(), 6);
        }

        /// <summary>
        /// The inset preset becomes a viewport and the zoom preset becomes a zoom track starting at 20% of the scene.
        /// </summary>
        [Fact]
        public void Compile_ExpandsLayoutAndMotionPresets() {
            VisualLayer picture = Compile(VideoEditSamples.TwoScenes()).Composition.Layers.Single(layer => layer.Id == "hook-picture");

            Assert.Equal(0.88, picture.Viewport.Width, 6);
            PropertyAnimation zoom = Assert.Single(picture.Animations, animation => animation.Property == "zoom");
            Assert.Equal(0.6, zoom.Keyframes[0].Time.ToSeconds(), 6);
            Assert.Equal(1.4, zoom.Keyframes[^1].Value, 6);
            Assert.NotEqual(0.5, picture.Transform.FocusX);
        }

        /// <summary>
        /// A push entry adds a transition at the cut and extends the previous scene over the overlap.
        /// </summary>
        [Fact]
        public void Compile_TransitionExtendsPreviousScene() {
            CompositionDocument composition = Compile(VideoEditSamples.TwoScenes()).Composition;

            CompositionTransition transition = Assert.Single(composition.Transitions);
            Assert.Equal("scene-hook", transition.FromLayer);
            Assert.Equal(3d, transition.Start.ToSeconds(), 6);
            Assert.Equal(3.4, composition.Layers.Single(layer => layer.Id == "scene-hook").End.ToSeconds(), 6);
            Assert.Equal(3.4, composition.Layers.Single(layer => layer.Id == "hook-picture").End.ToSeconds(), 6);
        }

        /// <summary>
        /// When the outgoing take has no footage after its out point, the take layer holds its last frame instead.
        /// </summary>
        [Fact]
        public void Compile_TakeWithoutHandleHoldsLastFrame() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            (edit.Scenes[0], edit.Scenes[1]) = (edit.Scenes[1], edit.Scenes[0]);
            edit.Scenes[1].Entry = edit.Scenes[0].Entry;
            edit.Scenes[0].Entry = null;
            edit.Scenes[0].Take.OutSec = 20;
            edit.Scenes[0].Take.InSec = 17;

            VideoCompileResult result = Compile(edit);

            Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Message)));
            VisualLayer take = result.Composition.Layers.Single(layer => layer.Id == "question-take");
            Assert.True(take.HoldLastFrame);
            Assert.Equal(20d, take.SourceOut.ToSeconds(), 6);
        }

        /// <summary>
        /// Take words become caption cues of four words with global word times; the overlay starts on its word.
        /// </summary>
        [Fact]
        public void Compile_CaptionsAndOverlayFollowTheTakeWords() {
            CompositionDocument composition = Compile(VideoEditSamples.TwoScenes()).Composition;

            VisualLayer first = composition.Layers.Single(layer => layer.Id == "caption-question-0");
            VisualLayer second = composition.Layers.Single(layer => layer.Id == "caption-question-1");
            Assert.Equal("Qual é a lei?", first.Text.Cues[0].Text);
            Assert.Equal(4, first.Text.Cues[0].Words.Count);
            Assert.Equal(3.1, first.Start.ToSeconds(), 6);
            Assert.Equal(4.2, second.Start.ToSeconds(), 6);
            Assert.Equal(3.5, composition.Layers.Single(layer => layer.Id == "question-overlay-q").Start.ToSeconds(), 6);
            Assert.Contains("caption-question-0", composition.Layers.Single(layer => layer.Id == "scene-question").Members);
        }

        /// <summary>
        /// Before a take exists, previews estimate captions from the planned speech and finals report caption_alignment.
        /// </summary>
        [Fact]
        public void Compile_PlannedSpeechEstimatesPreviewCaptionsOnly() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Scenes[0].Speech = "Tá. O que isso quer dizer?";

            List<VisualLayer> estimated = Compile(edit).Composition.Layers.Where(layer => layer.Id.StartsWith("caption-hook-", StringComparison.Ordinal)).ToList();
            Assert.Equal(["TÁ. O QUE ISSO", "QUER DIZER?"], estimated.Select(layer => layer.Text.Cues[0].Text.ToUpperInvariant()));
            Assert.Empty(estimated[0].Text.Cues[0].Words);
            Assert.Equal(3.4, estimated[1].End.ToSeconds(), 6);
            VideoCompileResult final = Compile(edit, true);
            Assert.Contains(final.Diagnostics, item => item.Code == "caption_alignment" && item.Scene == "hook");
            Assert.True(final.BlocksFinal);
        }

        /// <summary>
        /// The music track starts half a second into the second scene and runs to the end of the video.
        /// </summary>
        [Fact]
        public void Compile_AudioTrackIsPlacedByItsSceneMoment() {
            AudioClip music = Compile(VideoEditSamples.TwoScenes()).Composition.AudioClips.Single(clip => clip.Id == "track-bed");

            Assert.Equal(3.5, music.Start.ToSeconds(), 6);
            Assert.Equal(6d, music.End.ToSeconds(), 6);
            Assert.Equal(0.3, music.Gain, 6);
        }

        /// <summary>
        /// A take with audio produces a voice clip that fades in when its entry asks for a voice fade.
        /// </summary>
        [Fact]
        public void Compile_VoiceClipFadesInAfterTransition() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Media.Single(media => media.Id == "take").HasAudio = true;

            AudioClip voice = Compile(edit).Composition.AudioClips.Single(clip => clip.Id == "voice-question");

            Assert.Equal(12.3, voice.SourceIn.ToSeconds(), 6);
            AudioEnvelope fade = Assert.Single(voice.Envelopes);
            Assert.Equal("equal_power_in", fade.Type);
            Assert.Equal(0.15, fade.Duration.ToSeconds(), 6);
        }

        /// <summary>
        /// A final render of an estimated scene is blocked with estimated_duration.
        /// </summary>
        [Fact]
        public void Compile_FinalWithEstimateIsPending() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Scenes[0].Duration = new VideoSceneDuration { Mode = "estimate", Sec = 3 };

            Assert.Contains(Compile(edit, true).Diagnostics, item => item.Code == "estimated_duration");
            Assert.DoesNotContain(Compile(edit).Diagnostics, item => item.Code == "estimated_duration");
        }

        /// <summary>
        /// Compiles with the validator catalog.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <param name="final">Whether to compile for a final render.</param>
        /// <returns>Compile result.</returns>
        static VideoCompileResult Compile(VideoEdit edit, bool final = false) {
            return VideoEditCompiler.Compile(edit, new VideoCompileContext { Capabilities = VideoEditValidatorTests.Catalog(), Final = final });
        }
    }
}
