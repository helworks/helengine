using Xunit;

namespace helengine.editor.tests.managers.stats {
    /// <summary>
    /// Verifies the viewport stats frame-rate tracker and stats text formatting.
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
        /// Ensures the stats text builder formats every scene metric on its own line.
        /// </summary>
        [Fact]
        public void TextBuilder_WhenSnapshotIsProvided_FormatsAllMetrics() {
            EditorViewportStatsSnapshot snapshot = new EditorViewportStatsSnapshot {
                Fps = 62.5,
                FrameMilliseconds = 16.0,
                EntityCount = 342,
                VisibleDrawables3D = 128,
                TotalDrawables3D = 590,
                TotalDrawables2D = 210,
                DirectionalLightCount = 1,
                PointLightCount = 3,
                SpotLightCount = 0,
                AmbientLightCount = 1,
                UpdateableCount = 87
            };

            string text = EditorViewportStatsTextBuilder.Build(snapshot);

            Assert.Equal(
                "FPS: 62.5 (16.0 ms)\n" +
                "Entities: 342\n" +
                "Draw 3D: 128 / 590\n" +
                "Draw 2D: 210\n" +
                "Lights: 1 dir  3 pt  0 spot  1 amb\n" +
                "Updates: 87",
                text);
        }
    }
}
