using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.stats {
    /// <summary>
    /// Verifies the viewport stats frame-rate tracker, scene classification, and stats text formatting.
    /// </summary>
    public sealed class EditorViewportStatsTests {
        /// <summary>
        /// Ensures the tracker averages recorded frame deltas into FPS and frame milliseconds.
        /// </summary>
        [Fact]
        public void Tracker_WhenDeltasAreRecorded_ReportsAverageFpsAndMilliseconds() {
            EditorViewportFrameRateTracker tracker = new EditorViewportFrameRateTracker(4);

            tracker.Record(0.02);
            tracker.Record(0.02);

            Assert.Equal(50.0, tracker.AverageFps, 3);
            Assert.Equal(20.0, tracker.AverageFrameMilliseconds, 3);
        }

        /// <summary>
        /// Ensures the tracker window rolls so old samples stop influencing the average.
        /// </summary>
        [Fact]
        public void Tracker_WhenWindowOverflows_DropsOldestSamples() {
            EditorViewportFrameRateTracker tracker = new EditorViewportFrameRateTracker(2);

            tracker.Record(1.0);
            tracker.Record(0.01);
            tracker.Record(0.01);

            Assert.Equal(100.0, tracker.AverageFps, 3);
        }

        /// <summary>
        /// Ensures the tracker reports zero rates before any valid sample arrives and ignores non-positive deltas.
        /// </summary>
        [Fact]
        public void Tracker_WhenNoValidSamplesExist_ReportsZero() {
            EditorViewportFrameRateTracker tracker = new EditorViewportFrameRateTracker(4);

            tracker.Record(0.0);
            tracker.Record(-1.0);

            Assert.Equal(0.0, tracker.AverageFps, 3);
            Assert.Equal(0.0, tracker.AverageFrameMilliseconds, 3);
        }

        /// <summary>
        /// Ensures only authored scene-owned entities classify into the scene stats group.
        /// </summary>
        [Fact]
        public void Classifier_OnlySceneOwnedNonInternalEditorEntities_CountAsScene() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));

            EditorEntity sceneEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            EditorEntity internalEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true, InternalEntity = true };
            EditorEntity editorUiEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            Entity plainEntity = new Entity(Core.Instance);

            Assert.True(EditorViewportStatsSceneClassifier.IsSceneEntity(sceneEntity));
            Assert.False(EditorViewportStatsSceneClassifier.IsSceneEntity(internalEntity));
            Assert.False(EditorViewportStatsSceneClassifier.IsSceneEntity(editorUiEntity));
            Assert.False(EditorViewportStatsSceneClassifier.IsSceneEntity(plainEntity));
            Assert.False(EditorViewportStatsSceneClassifier.IsSceneEntity(null));
        }

        /// <summary>
        /// Ensures the stats text builder formats scene and editor metrics as separate groups.
        /// </summary>
        [Fact]
        public void TextBuilder_WhenSnapshotIsProvided_FormatsSceneAndEditorGroups() {
            EditorViewportStatsSnapshot snapshot = new EditorViewportStatsSnapshot {
                Fps = 238.2,
                FrameMilliseconds = 4.2,
                Scene = new EditorViewportStatsGroup {
                    EntityCount = 42,
                    VisibleDrawables3D = 128,
                    TotalDrawables3D = 140,
                    TotalDrawables2D = 3,
                    DirectionalLightCount = 1,
                    PointLightCount = 0,
                    SpotLightCount = 0,
                    AmbientLightCount = 1
                },
                Editor = new EditorViewportStatsGroup {
                    EntityCount = 885,
                    VisibleDrawables3D = 4,
                    TotalDrawables3D = 12,
                    TotalDrawables2D = 104,
                    DirectionalLightCount = 1,
                    PointLightCount = 0,
                    SpotLightCount = 0,
                    AmbientLightCount = 0
                },
                UpdateableCount = 87
            };

            string text = EditorViewportStatsTextBuilder.Build(snapshot);

            Assert.Equal(
                "FPS: 238.2 (4.2 ms)\n" +
                "-- Scene --\n" +
                "Entities: 42\n" +
                "Draw 3D: 128 / 140\n" +
                "Draw 2D: 3\n" +
                "Lights: 1 dir  0 pt  0 spot  1 amb\n" +
                "-- Editor --\n" +
                "Entities: 885\n" +
                "Draw 3D: 4 / 12\n" +
                "Draw 2D: 104\n" +
                "Lights: 1 dir  0 pt  0 spot  0 amb\n" +
                "Updates: 87",
                text);
        }
    }
}
