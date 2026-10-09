namespace helengine.timeline.tests {
    /// <summary>
    /// Verifies that <see cref="TimelineValidator"/> accepts the rich sample and rejects each kind of mistake with a
    /// readable message located at the exact JSON path a planner has to correct.
    /// </summary>
    public class TimelineValidatorTests {
        /// <summary>
        /// Mutations that each break exactly one rule of the rich sample, keyed by case name.
        /// </summary>
        static readonly Dictionary<string, Action<TimelineAsset>> Mutations = new Dictionary<string, Action<TimelineAsset>> {
            ["bad id"] = timeline => timeline.TimelineId = "Contrast Three",
            ["zero version"] = timeline => timeline.Version = 0,
            ["zero duration"] = timeline => timeline.DurationSeconds = 0,
            ["duplicate slot"] = timeline => timeline.Slots[1].Name = "term_a",
            ["bad slot name"] = timeline => timeline.Slots[2].Name = "Term C",
            ["duplicate cue"] = timeline => timeline.Cues[2].Name = "a",
            ["cue after end"] = timeline => timeline.Cues[0].TimeSeconds = 4.5,
            ["unknown slot"] = timeline => timeline.Tracks[0].Slot = "term_z",
            ["missing slot"] = timeline => timeline.Tracks[1].Slot = string.Empty,
            ["animation on text"] = timeline => timeline.Tracks[5].Slot = "term_a",
            ["audio on rect"] = timeline => timeline.Tracks[4].Slot = "strike",
            ["event with slot"] = timeline => timeline.Tracks[6].Slot = "term_a",
            ["nested track with slot"] = timeline => timeline.Tracks[7].Slot = "term_a",
            ["unknown cue"] = timeline => timeline.Tracks[0].ClipView[0].Cue = "d",
            ["zero clip duration"] = timeline => ((TimelineActivationTrackAsset)timeline.Tracks[0]).Clips[0].DurationSeconds = 0,
            ["clip after end"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[3]).Clips[0].StartSeconds = 3.8,
            ["clip before start"] = timeline => ((TimelineTransformTrackAsset)timeline.Tracks[1]).Clips[0].StartSeconds = -0.3,
            ["ramps longer than clip"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[3]).Clips[0].EaseInSeconds = 0.5,
            ["overlap outside ramps"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[2]).Clips[1].StartSeconds = -1,
            ["clips out of order"] = timeline => ((TimelineAnimationTrackAsset)timeline.Tracks[5]).Clips[0].StartSeconds = 1.7,
            ["keyframes not increasing"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[2]).Clips[0].Keyframes[1].TimeSeconds = 0,
            ["keyframe after clip"] = timeline => ((TimelineTransformTrackAsset)timeline.Tracks[1]).Clips[0].Scale[1].TimeSeconds = 0.6,
            ["unknown curve"] = timeline => ((TimelineTransformTrackAsset)timeline.Tracks[1]).Clips[0].Scale[0].Curve = "bounce.v1",
            ["opacity out of range"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[2]).Clips[0].Keyframes[1].Value = 1.5,
            ["bad channel"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[3]).Channel = "Reveal",
            ["empty value clip"] = timeline => ((TimelineValueTrackAsset)timeline.Tracks[3]).Clips[0].Keyframes.Clear(),
            ["empty transform clip"] = timeline => ClearTransform(((TimelineTransformTrackAsset)timeline.Tracks[1]).Clips[0]),
            ["clip in on transform"] = timeline => ((TimelineTransformTrackAsset)timeline.Tracks[1]).Clips[0].ClipInSeconds = 0.1,
            ["blending activation"] = timeline => ((TimelineActivationTrackAsset)timeline.Tracks[0]).Clips[0].EaseInSeconds = 0.1,
            ["missing audio"] = timeline => ((TimelineAudioTrackAsset)timeline.Tracks[4]).Clips[0].Audio = null,
            ["loud audio"] = timeline => ((TimelineAudioTrackAsset)timeline.Tracks[4]).Clips[0].Gain = 5,
            ["zero speed"] = timeline => ((TimelineAnimationTrackAsset)timeline.Tracks[5]).Clips[1].Speed = 0,
            ["bad event name"] = timeline => ((TimelineEventTrackAsset)timeline.Tracks[6]).Markers[0].Name = "Shake It",
            ["markers out of order"] = timeline => ((TimelineEventTrackAsset)timeline.Tracks[6]).Markers[1].TimeSeconds = 1,
            ["nested without source"] = timeline => ((TimelineNestedTrackAsset)timeline.Tracks[7]).Clips[0].Definition = null,
            ["unmapped nested slot"] = timeline => ((TimelineNestedTrackAsset)timeline.Tracks[7]).Clips[0].SlotMappings.Clear(),
            ["nested kind mismatch"] = timeline => ((TimelineNestedTrackAsset)timeline.Tracks[7]).Clips[0].SlotMappings[0].Outer = "strike",
            ["error inside inline definition"] = timeline => ((TimelineNestedTrackAsset)timeline.Tracks[7]).Clips[0].Definition.Tracks[1].Slot = "nobody"
        };

        /// <summary>
        /// Case name, expected diagnostic path and a fragment the message must contain.
        /// </summary>
        public static TheoryData<string, string, string> InvalidCases => new TheoryData<string, string, string> {
            { "bad id", "id", "lowercase letters" },
            { "zero version", "version", "1 or greater" },
            { "zero duration", "duration", "greater than 0" },
            { "duplicate slot", "slots[1].name", "declared twice" },
            { "bad slot name", "slots[2].name", "found 'Term C'" },
            { "duplicate cue", "cues[2].name", "declared twice" },
            { "cue after end", "cues[0].time", "between 0 and the duration" },
            { "unknown slot", "tracks[0].slot", "Unknown slot 'term_z'; declared slots: term_a, term_b" },
            { "missing slot", "tracks[1].slot", "Transform tracks need a slot" },
            { "animation on text", "tracks[5].slot", "Animation tracks need an entity slot, but 'term_a' is a text slot" },
            { "audio on rect", "tracks[4].slot", "Audio tracks need an entity slot" },
            { "event with slot", "tracks[6].slot", "Event tracks have no slot" },
            { "nested track with slot", "tracks[7].slot", "map slots on each clip" },
            { "unknown cue", "tracks[0].clips[0].start.cue", "Unknown cue 'd'; declared cues: a, b, c" },
            { "zero clip duration", "tracks[0].clips[0].duration", "greater than 0" },
            { "clip after end", "tracks[3].clips[0].duration", "after the timeline duration" },
            { "clip before start", "tracks[1].clips[0].start", "before the timeline starts" },
            { "ramps longer than clip", "tracks[3].clips[0].ease_out", "cannot be longer than the clip" },
            { "overlap outside ramps", "tracks[2].clips[1].start", "only overlap inside the blend ramps" },
            { "clips out of order", "tracks[5].clips[1].start", "listed in start order" },
            { "keyframes not increasing", "tracks[2].clips[0].keyframes[1].time", "Keyframe times must increase" },
            { "keyframe after clip", "tracks[1].clips[0].scale[1].time", "between 0 and the clip duration" },
            { "unknown curve", "tracks[1].clips[0].scale[0].curve", "Unknown curve 'bounce.v1'; use one of linear.v1" },
            { "opacity out of range", "tracks[2].clips[0].keyframes[1].value", "'opacity' channel takes values from 0 to 1" },
            { "bad channel", "tracks[3].channel", "channel name" },
            { "empty value clip", "tracks[3].clips[0].keyframes", "at least one keyframe" },
            { "empty transform clip", "tracks[1].clips[0]", "at least one keyframe in position, rotation or scale" },
            { "clip in on transform", "tracks[1].clips[0].clip_in", "clip_in only applies" },
            { "blending activation", "tracks[0].clips[0]", "Activation clips cannot blend" },
            { "missing audio", "tracks[4].clips[0].audio", "need an audio asset reference" },
            { "loud audio", "tracks[4].clips[0].gain", "between 0 and 4" },
            { "zero speed", "tracks[5].clips[1].speed", "greater than 0" },
            { "bad event name", "tracks[6].markers[0].name", "event name" },
            { "markers out of order", "tracks[6].markers[1].time", "listed in time order" },
            { "nested without source", "tracks[7].clips[0]", "needs 'timeline' (a reference) or 'definition'" },
            { "unmapped nested slot", "tracks[7].clips[0].slots", "nested slot 'target' is not mapped" },
            { "nested kind mismatch", "tracks[7].clips[0].slots.target", "Slot kinds differ" },
            { "error inside inline definition", "tracks[7].clips[0].definition.tracks[1].slot", "Unknown slot 'nobody'" }
        };

        /// <summary>
        /// The rich sample is valid.
        /// </summary>
        [Fact]
        public void Validate_richSample_reportsNothing() {
            IReadOnlyList<TimelineDiagnostic> diagnostics = TimelineValidator.Validate(TimelineSamples.ContrastThree(), null);

            Assert.Empty(diagnostics);
        }

        /// <summary>
        /// Every mutation is reported once, at its path, with an explanatory message.
        /// </summary>
        /// <param name="caseName">Mutation to apply.</param>
        /// <param name="path">Expected diagnostic path.</param>
        /// <param name="fragment">Text the message must contain.</param>
        [Theory]
        [MemberData(nameof(InvalidCases))]
        public void Validate_invalidMutation_reportsLocatedMessage(string caseName, string path, string fragment) {
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            Mutations[caseName](timeline);

            IReadOnlyList<TimelineDiagnostic> diagnostics = TimelineValidator.Validate(timeline, null);

            TimelineDiagnostic diagnostic = Assert.Single(diagnostics, candidate => candidate.Path == path);
            Assert.Contains(fragment, diagnostic.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Every mutation case has an expectation and every expectation has a mutation.
        /// </summary>
        [Fact]
        public void InvalidCases_coverEveryMutation() {
            Assert.Equal(Mutations.Count, InvalidCases.Count);
        }

        /// <summary>
        /// EnsureValid throws a format exception listing every located problem.
        /// </summary>
        [Fact]
        public void EnsureValid_invalidTimeline_throwsWithAllDiagnostics() {
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            timeline.Version = 0;
            timeline.Tracks[0].Slot = "term_z";

            TimelineFormatException exception = Assert.Throws<TimelineFormatException>(() => TimelineValidator.EnsureValid(timeline, null));

            Assert.Equal(2, exception.Diagnostics.Count);
            Assert.Contains("version: The version must be 1 or greater.", exception.Message, StringComparison.Ordinal);
            Assert.Contains("tracks[0].slot: Unknown slot 'term_z'", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// A referenced timeline is followed through the resolver: its mapping is checked and its contents validated.
        /// </summary>
        [Fact]
        public void Validate_resolvedReference_checksMappingAndContents() {
            TimelineAsset pop = TimelineSamples.Pop();
            pop.Tracks[0].Slot = "nobody";
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            timeline.Tracks.Add(TimelineSamples.ReferenceTrack("term_b", "assets/timelines/pop.htimeline", 0.6));
            DictionaryTimelineResolver resolver = new DictionaryTimelineResolver();
            resolver.Add("assets/timelines/pop.htimeline", pop);

            IReadOnlyList<TimelineDiagnostic> diagnostics = TimelineValidator.Validate(timeline, resolver);

            TimelineDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("tracks[8].clips[0].timeline.tracks[0].slot", diagnostic.Path);
        }

        /// <summary>
        /// Without a resolver a reference is not followed; with one, a missing asset is reported.
        /// </summary>
        [Fact]
        public void Validate_unresolvedReference_isReportedOnlyWithAResolver() {
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            timeline.Tracks.Add(TimelineSamples.ReferenceTrack("term_b", "assets/timelines/missing.htimeline", 0.6));

            Assert.Empty(TimelineValidator.Validate(timeline, null));
            TimelineDiagnostic diagnostic = Assert.Single(TimelineValidator.Validate(timeline, new DictionaryTimelineResolver()));
            Assert.Equal("tracks[8].clips[0].timeline", diagnostic.Path);
            Assert.Contains("cannot be found", diagnostic.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Two assets that reference each other form a cycle that is reported instead of recursing forever.
        /// </summary>
        [Fact]
        public void Validate_referenceCycle_isReported() {
            TimelineAsset first = TimelineSamples.Pop();
            first.TimelineId = "first";
            first.Tracks.Add(TimelineSamples.ReferenceTrack("target", "second.htimeline", 0.6));
            TimelineAsset second = TimelineSamples.Pop();
            second.TimelineId = "second";
            second.Tracks.Add(TimelineSamples.ReferenceTrack("target", "first.htimeline", 0.6));
            DictionaryTimelineResolver resolver = new DictionaryTimelineResolver();
            resolver.Add("first.htimeline", first);
            resolver.Add("second.htimeline", second);

            IReadOnlyList<TimelineDiagnostic> diagnostics = TimelineValidator.Validate(first, resolver);

            TimelineDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("tracks[2].clips[0].timeline.tracks[2].clips[0].timeline", diagnostic.Path);
            Assert.Contains("contains itself", diagnostic.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Inline nesting is limited to eight levels below the root.
        /// </summary>
        [Fact]
        public void Validate_nestingDeeperThanEight_isReported() {
            TimelineAsset valid = Nest(TimelineValidator.MaxNestingDepth);
            TimelineAsset tooDeep = Nest(TimelineValidator.MaxNestingDepth + 1);

            Assert.Empty(TimelineValidator.Validate(valid, null));
            TimelineDiagnostic diagnostic = Assert.Single(TimelineValidator.Validate(tooDeep, null));
            Assert.Contains("at most 8 levels deep", diagnostic.Message, StringComparison.Ordinal);
            Assert.EndsWith("tracks[2].clips[0]", diagnostic.Path, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds a chain of inline pops nested the given number of levels below the root.
        /// </summary>
        /// <param name="levels">Nesting levels below the root.</param>
        /// <returns>The root timeline.</returns>
        static TimelineAsset Nest(int levels) {
            TimelineAsset root = TimelineSamples.Pop();
            TimelineAsset current = root;
            for (int level = 0; level < levels; level++) {
                TimelineAsset child = TimelineSamples.Pop();
                TimelineNestedTrackAsset track = new TimelineNestedTrackAsset();
                TimelineNestedClipAsset clip = new TimelineNestedClipAsset { DurationSeconds = 0.6, Definition = child };
                clip.SlotMappings.Add(new TimelineSlotMappingAsset { Inner = "target", Outer = "target" });
                track.Clips.Add(clip);
                current.Tracks.Add(track);
                current = child;
            }
            return root;
        }

        /// <summary>
        /// Empties every keyframe channel of a transform clip.
        /// </summary>
        /// <param name="clip">Clip to empty.</param>
        static void ClearTransform(TimelineTransformClipAsset clip) {
            clip.Position.Clear();
            clip.Rotation.Clear();
            clip.Scale.Clear();
        }
    }
}
