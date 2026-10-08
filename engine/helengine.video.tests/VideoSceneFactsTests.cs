using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the per-scene facts handed to the AI model.
    /// </summary>
    public class VideoSceneFactsTests {
        /// <summary>
        /// Words and silences are relative to the take start; the leading gap and the inner gaps are reported.
        /// </summary>
        [Fact]
        public void Build_WordsAndSilencesAreTakeRelative() {
            List<VideoSceneFact> facts = VideoSceneFacts.Build(VideoEditSamples.TwoScenes());

            VideoSceneFact question = facts[1];
            Assert.Equal(6, question.Words.Count);
            Assert.Equal("Qual", question.Words[0].Text);
            Assert.Equal(0.1, question.Words[0].StartSec, 6);
            Assert.Equal(2.6, question.Words[5].EndSec, 6);
            Assert.Equal(3, question.DurationSec, 6);
            Assert.Equal(3, question.Silences.Count);
            Assert.Equal(0.9, question.Silences[0].StartSec, 6);
            Assert.Equal(1.2, question.Silences[0].EndSec, 6);
            Assert.Equal(1.7, question.Silences[1].StartSec, 6);
            Assert.Equal(2.1, question.Silences[1].EndSec, 6);
            Assert.Equal(2.6, question.Silences[2].StartSec, 6);
            Assert.Equal(3, question.Silences[2].EndSec, 6);
        }

        /// <summary>
        /// Leading and trailing gaps of at least a quarter second become silences.
        /// </summary>
        [Fact]
        public void Build_LeadingAndTrailingSilence() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Scenes[1].Take.InSec = 12.0;
            edit.Scenes[1].Take.OutSec = 13.5;

            VideoSceneFact question = VideoSceneFacts.Build(edit)[1];

            Assert.Equal(2, question.Silences.Count);
            Assert.Equal(0, question.Silences[0].StartSec, 6);
            Assert.Equal(0.4, question.Silences[0].EndSec, 6);
            Assert.Equal(1.2, question.Silences[1].StartSec, 6);
            Assert.Equal(1.5, question.Silences[1].EndSec, 6);
        }

        /// <summary>
        /// Handles are the unused source seconds on both sides of the take.
        /// </summary>
        [Fact]
        public void Build_HandlesAroundTake() {
            VideoSceneFact question = VideoSceneFacts.Build(VideoEditSamples.TwoScenes())[1];

            Assert.Equal(12.3, question.HandleBeforeSec, 6);
            Assert.Equal(4.7, question.HandleAfterSec, 6);
            Assert.True(question.HasTake);
            Assert.Equal("take", question.Visual);
        }

        /// <summary>
        /// A scene without a take has no words, silences or handles, and section change is computed against the previous scene.
        /// </summary>
        [Fact]
        public void Build_SceneWithoutTakeAndSectionFlags() {
            List<VideoSceneFact> facts = VideoSceneFacts.Build(VideoEditSamples.TwoScenes());

            VideoSceneFact hook = facts[0];
            Assert.False(hook.HasTake);
            Assert.Equal("image", hook.Visual);
            Assert.Equal(3, hook.DurationSec, 6);
            Assert.Empty(hook.Words);
            Assert.Empty(hook.Silences);
            Assert.Equal(0, hook.HandleBeforeSec);
            Assert.Equal(0, hook.HandleAfterSec);
            Assert.False(hook.SectionChanges);
            Assert.True(facts[1].SectionChanges);
        }

        /// <summary>
        /// A scene repeating the previous section does not flag a change, and facts serialize as snake_case.
        /// </summary>
        [Fact]
        public void Build_SameSectionDoesNotChange_SerializesSnakeCase() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Scenes[1].Section = "abertura";

            List<VideoSceneFact> facts = VideoSceneFacts.Build(edit);
            string json = JsonSerializer.Serialize(facts[1], VideoEditJson.Options);

            Assert.False(facts[1].SectionChanges);
            Assert.Contains("\"scene_id\"", json);
            Assert.Contains("\"handle_before_sec\"", json);
            Assert.Contains("\"has_take\"", json);
        }
    }
}
