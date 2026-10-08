using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies scene spans and every moment form, including word anchors and clamping.
    /// </summary>
    public class VideoMomentResolverTests {
        /// <summary>
        /// Scenes are laid end to end using fixed, take and estimated lengths.
        /// </summary>
        [Fact]
        public void Build_LaysScenesEndToEnd() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Scenes.Add(new VideoScene { Id = "close", Duration = new VideoSceneDuration { Mode = "estimate", Sec = 2 } });
            List<VideoDiagnostic> diagnostics = new List<VideoDiagnostic>();

            IReadOnlyList<VideoSceneSpan> spans = VideoSceneTimeline.Build(edit, diagnostics);

            Assert.Empty(diagnostics);
            Assert.Equal([0d, 3d, 6d], spans.Select(span => span.Start.ToSeconds()));
            Assert.Equal(8d, spans[2].End.ToSeconds(), 6);
            Assert.True(spans[2].Estimated);
            Assert.False(spans[1].Estimated);
        }

        /// <summary>
        /// Seconds, seconds before the end and fractions resolve arithmetically inside a three-second take scene.
        /// </summary>
        [Fact]
        public void Resolve_ArithmeticForms() {
            Assert.Equal(1.2, Resolve(new VideoMoment { Sec = 1.2 }).ToSeconds(), 6);
            Assert.Equal(2.7, Resolve(new VideoMoment { FromEnd = 0.3 }).ToSeconds(), 6);
            Assert.Equal(1.5, Resolve(new VideoMoment { Fraction = 0.5 }).ToSeconds(), 6);
        }

        /// <summary>
        /// A word anchor finds the word in the take, ignoring case, accents and punctuation, and honours the occurrence.
        /// </summary>
        [Fact]
        public void Resolve_WordAnchors() {
            Assert.Equal(0.5, Resolve(new VideoMoment { Word = "LEI" }).ToSeconds(), 6);
            Assert.Equal(1.2, Resolve(new VideoMoment { Word = "politica" }).ToSeconds(), 6);
            Assert.Equal(2.1, Resolve(new VideoMoment { Word = "política", Occurrence = 2 }).ToSeconds(), 6);
            Assert.Equal(1.3, Resolve(new VideoMoment { Word = "lei", OffsetSec = 0.8 }).ToSeconds(), 6);
        }

        /// <summary>
        /// A missing word is a pending issue resolving to the scene start, never an exception.
        /// </summary>
        [Fact]
        public void Resolve_MissingWord_IsPendingAtSceneStart() {
            List<VideoDiagnostic> diagnostics = new List<VideoDiagnostic>();
            Assert.Equal(MediaTime.Zero, Resolve(new VideoMoment { Word = "imposto" }, diagnostics));
            Assert.Equal("anchor_unresolved", Assert.Single(diagnostics).Code);
            Assert.Equal(VideoDiagnosticSeverity.Pending, diagnostics[0].Severity);
        }

        /// <summary>
        /// Moments outside the scene are clamped to its edges with a warning.
        /// </summary>
        [Fact]
        public void Resolve_OutsideScene_IsClamped() {
            List<VideoDiagnostic> diagnostics = new List<VideoDiagnostic>();
            Assert.Equal(3d, Resolve(new VideoMoment { Sec = 9 }, diagnostics).ToSeconds(), 6);
            Assert.Equal(0d, Resolve(new VideoMoment { FromEnd = 5 }, diagnostics).ToSeconds(), 6);
            Assert.All(diagnostics, diagnostic => Assert.Equal("moment_clamped", diagnostic.Code));
            Assert.Equal(2, diagnostics.Count);
        }

        /// <summary>
        /// Resolves a moment in the sample take scene.
        /// </summary>
        /// <param name="moment">Moment.</param>
        /// <param name="diagnostics">Optional diagnostics sink.</param>
        /// <returns>Local time.</returns>
        static MediaTime Resolve(VideoMoment moment, List<VideoDiagnostic> diagnostics = null) {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            List<VideoDiagnostic> sink = diagnostics ?? new List<VideoDiagnostic>();
            VideoSceneSpan span = VideoSceneTimeline.Build(edit, sink)[1];
            return VideoMomentResolver.Resolve(edit, span, moment, "test", sink);
        }
    }
}
