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
    }
}
