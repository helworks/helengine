namespace helengine.video.tests {
    /// <summary>
    /// Verifies that re-planning keeps every human-locked object and follows the proposal elsewhere.
    /// </summary>
    public class VideoEditMergeTests {
        /// <summary>
        /// A locked entry survives a proposal that changes it; the unlocked hook layer follows the proposal.
        /// </summary>
        [Fact]
        public void Replan_LockedEntrySurvives_UnlockedLayerFollowsProposal() {
            VideoEdit current = VideoEditSamples.TwoScenes();
            VideoEdit proposal = VideoEditSamples.TwoScenes();
            proposal.Scenes[1].Entry = new VideoEntry { Effect = "cut" };
            proposal.Scenes[0].Layers[0].Layout = new VideoLayout { Preset = "full_frame" };

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal("push", merged.Scenes[1].Entry.Effect);
            Assert.Equal("full_frame", merged.Scenes[0].Layers[0].Layout.Preset);
            Assert.Equal("cut", proposal.Scenes[1].Entry.Effect);
        }

        /// <summary>
        /// A locked layer the proposal deleted is restored at its original position.
        /// </summary>
        [Fact]
        public void Replan_DeletedLockedLayerIsRestored() {
            VideoEdit current = VideoEditSamples.TwoScenes();
            current.Scenes[0].Layers[0].By = "human";
            VideoEdit proposal = VideoEditSamples.TwoScenes();
            proposal.Scenes[0].Layers.Clear();

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal("picture", Assert.Single(merged.Scenes[0].Layers).Id);
        }

        /// <summary>
        /// A locked motion on an unlocked layer is kept while the rest of the layer follows the proposal.
        /// </summary>
        [Fact]
        public void Replan_LockedMotionOnUnlockedLayerIsKept() {
            VideoEdit current = VideoEditSamples.TwoScenes();
            current.Scenes[0].Layers[0].Motion.By = "human";
            VideoEdit proposal = VideoEditSamples.TwoScenes();
            proposal.Scenes[0].Layers[0].Motion.ToScale = 2;
            proposal.Scenes[0].Layers[0].Fit = "cover";

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal(1.4, merged.Scenes[0].Layers[0].Motion.ToScale);
            Assert.Equal("cover", merged.Scenes[0].Layers[0].Fit);
        }

        /// <summary>
        /// A locked caption override replaces the proposal's override for the same cue.
        /// </summary>
        [Fact]
        public void Replan_LockedCaptionOverrideSurvives() {
            VideoEdit current = VideoEditSamples.TwoScenes();
            current.Tracks.Captions.Overrides.Add(new VideoCaptionOverride { Scene = "question", Cue = 0, Text = "QUAL É A LEI?", By = "human" });
            VideoEdit proposal = VideoEditSamples.TwoScenes();
            proposal.Tracks.Captions.Overrides.Add(new VideoCaptionOverride { Scene = "question", Cue = 0, Text = "qual lei" });

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal("QUAL É A LEI?", Assert.Single(merged.Tracks.Captions.Overrides).Text);
        }

        /// <summary>
        /// Replanning with human and ai origins keeps both kinds of objects while the proposal wins elsewhere.
        /// </summary>
        [Fact]
        public void Replan_KeepsHumanAndAiOrigins_ProposalWinsElsewhere() {
            VideoEdit current = VideoEditSamples.TwoScenes();
            current.Scenes[0].Layers[0].By = VideoEditMerge.Ai;
            current.Scenes[1].Take.By = VideoEditMerge.Ai;
            VideoEdit proposal = VideoEditSamples.TwoScenes();
            proposal.Scenes[0].Layers.Clear();
            proposal.Scenes[1].Take.InSec = 10;
            proposal.Scenes[1].Entry = new VideoEntry { Effect = "cut" };
            proposal.Scenes[1].Overlays[0].Text = "NOVO";

            VideoEdit merged = VideoEditMerge.Replan(current, proposal, [VideoEditMerge.Human, VideoEditMerge.Ai]);

            Assert.Equal("picture", Assert.Single(merged.Scenes[0].Layers).Id);
            Assert.Equal(12.3, merged.Scenes[1].Take.InSec);
            Assert.Equal("push", merged.Scenes[1].Entry.Effect);
            Assert.Equal("NOVO", merged.Scenes[1].Overlays[0].Text);
        }

        /// <summary>
        /// The two-argument replan ignores ai-marked objects.
        /// </summary>
        [Fact]
        public void Replan_TwoArguments_DropsAiObjects() {
            VideoEdit current = VideoEditSamples.TwoScenes();
            current.Scenes[0].Layers[0].By = VideoEditMerge.Ai;
            VideoEdit proposal = VideoEditSamples.TwoScenes();
            proposal.Scenes[0].Layers.Clear();

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Empty(merged.Scenes[0].Layers);
        }
    }
}
