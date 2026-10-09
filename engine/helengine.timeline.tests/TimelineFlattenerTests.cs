using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Verifies the seconds-based flattening shared by the cooker and other compilers: nested mapping with speed, cue
    /// resolution, cross-fades and exact curves.
    /// </summary>
    public class TimelineFlattenerTests {
        /// <summary>
        /// The nested pop (cue c - 0.1, speed 1.5) lands on the root slot it is mapped to, time-compressed, with its
        /// original catalog curve.
        /// </summary>
        [Fact]
        public void Flatten_nestedTimeline_isMappedAndCompressed() {
            FlattenedTimeline flat = TimelineFlattener.Flatten(TimelineSamples.ContrastThree(), null);

            FlattenedCurveTrack scale = Assert.Single(flat.CurveTracks, track => track.Slot == "term_c" && track.TransformChannel == CookedTimelineTransformChannel.ScaleY);
            FlattenedCurveSegment pop = scale.Segments[0];
            Assert.Equal(2.5, pop.StartSeconds, 9);
            Assert.Equal(2.5 + 0.4 / 1.5, pop.EndSeconds, 9);
            Assert.Equal(CurveCatalog.EaseOutBack, pop.Curve);
            Assert.True(pop.IsWholeCurve);
            Assert.Equal(1, scale.Segments[1].EndValue, 9);
            Assert.Equal(2.9, scale.Segments[1].EndSeconds, 9);
        }

        /// <summary>
        /// Overlapping clips cross-fade linearly between the outgoing and incoming values across the overlap.
        /// </summary>
        [Fact]
        public void Flatten_blendRamps_crossFade() {
            FlattenedTimeline flat = TimelineFlattener.Flatten(TimelineSamples.ContrastThree(), null);

            FlattenedCurveTrack fade = Assert.Single(flat.CurveTracks, track => track.Slot == "term_b" && track.Channel == "opacity");
            double value;
            Assert.False(fade.TryEvaluate(1.3, out value));
            Assert.True(fade.TryEvaluate(1.7, out value));
            Assert.Equal(1.0, value, 6);
            Assert.True(fade.TryEvaluate(1.8, out value));
            Assert.Equal(0.7, value, 6);
            Assert.True(fade.TryEvaluate(1.9, out value));
            Assert.Equal(0.4, value, 6);
        }

        /// <summary>
        /// Events, sounds and animations resolve their cues and keep authoring values; overlapping animations are cut.
        /// </summary>
        [Fact]
        public void Flatten_lanes_resolveCuesAndCutAnimationOverlaps() {
            FlattenedTimeline flat = TimelineFlattener.Flatten(TimelineSamples.ContrastThree(), null);

            Assert.Equal(2.65, flat.Events[0].TimeSeconds, 9);
            Assert.Equal("shake", flat.Events[0].Name);
            Assert.Equal("0.3", flat.Events[0].Value);
            Assert.Equal(2.6, Assert.Single(flat.AudioClips).StartSeconds, 9);
            FlattenedAnimationTrack animation = Assert.Single(flat.AnimationTracks);
            Assert.Equal(1.6, animation.Clips[0].EndSeconds, 9);
            Assert.Equal(1.25, animation.Clips[1].Speed, 9);
        }

        /// <summary>
        /// Moving a cue shifts the clips anchored on it without stretching them, grows the timeline by the shift and keeps
        /// an activation that lasted until the authored end alive until the new end.
        /// </summary>
        [Fact]
        public void Flatten_withCueTimes_shiftsAnchoredClipsAndExtendsTheEnd() {
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            Dictionary<string, double> cues = new Dictionary<string, double>(StringComparer.Ordinal) { ["c"] = 3.6 };

            FlattenedTimeline flat = TimelineFlattener.Flatten(timeline, null, cues, TimelineFlattener.DefaultBlendTolerance);

            Assert.Equal(5, flat.DurationSeconds, 9);
            FlattenedCurveTrack scale = Assert.Single(flat.CurveTracks, track => track.Slot == "term_c" && track.TransformChannel == CookedTimelineTransformChannel.ScaleY);
            Assert.Equal(3.5, scale.Segments[0].StartSeconds, 9);
            Assert.Equal(3.5 + 0.4 / 1.5, scale.Segments[0].EndSeconds, 9);
            Assert.Equal(3.65, Assert.Single(flat.Events, marker => marker.Name == "shake").TimeSeconds, 9);
            FlattenedActivationTrack termA = Assert.Single(flat.ActivationTracks, track => track.Slot == "term_a");
            Assert.Equal(0.2, termA.Intervals[0].StartSeconds, 9);
            Assert.Equal(5, termA.Intervals[0].EndSeconds, 9);
            Assert.Equal(2.6, timeline.Cues[2].TimeSeconds, 9);
            Assert.Equal(4, timeline.DurationSeconds, 9);
        }

        /// <summary>
        /// Cue overrides must name existing cues and use non-negative finite times.
        /// </summary>
        [Fact]
        public void Flatten_withCueTimes_rejectsUnknownCuesAndNegativeTimes() {
            Assert.Throws<ArgumentException>(() => TimelineFlattener.Flatten(TimelineSamples.ContrastThree(), null, new Dictionary<string, double> { ["z"] = 1 }, TimelineFlattener.DefaultBlendTolerance));
            Assert.Throws<ArgumentException>(() => TimelineFlattener.Flatten(TimelineSamples.ContrastThree(), null, new Dictionary<string, double> { ["a"] = -1 }, TimelineFlattener.DefaultBlendTolerance));
        }

        /// <summary>
        /// When moved cues make two clips of one track overlap, the retimed timeline fails validation with located paths.
        /// </summary>
        [Fact]
        public void Flatten_withCueTimes_reportsOverlapsTheMoveCreates() {
            Dictionary<string, double> cues = new Dictionary<string, double>(StringComparer.Ordinal) { ["b"] = 2.0 };

            TimelineFormatException error = Assert.Throws<TimelineFormatException>(() => TimelineFlattener.Flatten(TimelineSamples.ContrastThree(), null, cues, TimelineFlattener.DefaultBlendTolerance));

            Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Path.StartsWith("tracks[2].clips[", StringComparison.Ordinal));
        }
    }
}
