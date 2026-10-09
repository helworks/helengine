using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Cooks the rich sample (nested timeline with speed, cues, a cross-faded blend and every track kind) and checks the
    /// flat result tick by tick, the linearized mode, nested references and the cross-track priority rule.
    /// </summary>
    public class TimelineCookerTests {
        /// <summary>
        /// Native cook of the rich sample at 100 Hz: slots, durations, cue-anchored and nested segments, the cross-fade,
        /// activation, animation, audio and the event string table all land where the authoring says.
        /// </summary>
        [Fact]
        public void Cook_richSample_producesFlatTicks() {
            CookedTimelineAsset cooked = TimelineCooker.Cook(TimelineSamples.ContrastThree(), Options(TimelineCurveMode.Native), null, Catalog());

            Assert.Equal(100, cooked.TickRate);
            Assert.Equal(400, cooked.DurationTicks);
            Assert.Equal(new string[] { "term_a", "term_b", "term_c", "separator", "strike", "presenter" }, cooked.SlotNames);
            Assert.Equal(new string[] { "", "shake", "0.3", "strike" }, cooked.Strings);
            Assert.Equal("Transform slot=0 receiver=0 channel=1 mode=Offset: [10-40 0.05->0 c2] [40-60 0->0 c0]", Track(cooked, CookedTimelineTrackKind.Transform, 0, 1));
            Assert.Equal("Transform slot=0 receiver=0 channel=5 mode=Offset: [10-20 -4.5->-4.5 c0] [20-60 -4.5->-4.5 c0]", Track(cooked, CookedTimelineTrackKind.Transform, 0, 5));
            Assert.Equal("Transform slot=0 receiver=0 channel=6 mode=Offset: [10-45 0.6->1 c4] [45-60 1->1 c0]", Track(cooked, CookedTimelineTrackKind.Transform, 0, 6));
            Assert.Equal("Value slot=1 receiver=21 channel=0 mode=Absolute: [140-170 0->1 c1] [170-190 1->0.4 c0] [190-220 0.4->0.4 c0] [220-260 0.4->0.4 c0]", Track(cooked, CookedTimelineTrackKind.Value, 1, 0));
            Assert.Equal("Value slot=4 receiver=21 channel=1 mode=Absolute: [320-360 0->1 c2]", Track(cooked, CookedTimelineTrackKind.Value, 4, 1));
            Assert.Equal("Transform slot=2 receiver=0 channel=7 mode=Absolute: [250-277 0.5->1 c4] [277-290 1->1 c0]", Track(cooked, CookedTimelineTrackKind.Transform, 2, 7));
            Assert.Equal("Value slot=2 receiver=21 channel=0 mode=Absolute: [250-263 0->1 c0]", Track(cooked, CookedTimelineTrackKind.Value, 2, 0));
            Assert.Equal("Activation slot=0 receiver=0 channel=0 mode=Absolute: [20-400]", Track(cooked, CookedTimelineTrackKind.Activation, 0, 0));
            Assert.Equal("Animation slot=5 receiver=0 channel=0 mode=Absolute: [0-160 in0 x1 assets/anim/point.hanim] [160-400 in10 x1.25 assets/anim/idle.hanim]", Track(cooked, CookedTimelineTrackKind.Animation, 5, 0));
            Assert.Equal("Audio slot=-1 receiver=0 channel=0 mode=Absolute: [260-310 in5 gain0.8 assets/sfx/whoosh.wav]", Track(cooked, CookedTimelineTrackKind.Audio, -1, 0));
            Assert.Equal("Event slot=-1 receiver=0 channel=0 mode=Absolute: [@265 1=2] [@320 3=0]", Track(cooked, CookedTimelineTrackKind.Event, -1, 0));
            Assert.Equal(9 + 2 + 3 + 1 + 1 + 1 + 1 + 1, cooked.Tracks.Length);
        }

        /// <summary>
        /// Linearized mode uses only the linear curve code, and at every tick every curve track stays within the
        /// tolerance of the native cook.
        /// </summary>
        [Fact]
        public void Cook_linearized_usesOnlyLinearCodesWithinTolerance() {
            TimelineCookOptions native = Options(TimelineCurveMode.Native);
            TimelineCookOptions linear = Options(TimelineCurveMode.Linearized);
            CookedTimelineAsset expected = TimelineCooker.Cook(TimelineSamples.ContrastThree(), native, null, Catalog());
            CookedTimelineAsset actual = TimelineCooker.Cook(TimelineSamples.ContrastThree(), linear, null, Catalog());

            Assert.Equal(expected.Tracks.Length, actual.Tracks.Length);
            int linearizedSegments = 0;
            for (int index = 0; index < actual.Tracks.Length; index++) {
                CookedTimelineTrack track = actual.Tracks[index];
                for (int segment = 0; segment < track.Segments.Length; segment++) {
                    Assert.Equal(CurveCatalog.LinearCode, track.Segments[segment].CurveCode);
                }
                linearizedSegments += track.Segments.Length - expected.Tracks[index].Segments.Length;
                for (int tick = 0; tick <= actual.DurationTicks; tick++) {
                    double wanted = Sample(expected.Tracks[index], tick);
                    double got = Sample(actual.Tracks[index], tick);
                    if (double.IsNaN(wanted)) {
                        Assert.True(double.IsNaN(got), $"Track {index} tick {tick}: linearized has a value before the native track starts.");
                        continue;
                    }
                    Assert.True(Math.Abs(wanted - got) <= linear.Tolerance + 1e-5, $"Track {index} tick {tick}: native {wanted}, linearized {got}.");
                }
            }
            Assert.True(linearizedSegments > 0);
        }

        /// <summary>
        /// A nested timeline given by reference is resolved through the resolver, mapped through the slot mapping and
        /// cut to its clip; without a resolver the cook fails with a readable message.
        /// </summary>
        [Fact]
        public void Cook_referencedNestedTimeline_isResolvedAndMapped() {
            TimelineAsset timeline = TimelineSamples.ContrastThree();
            timeline.Tracks.Add(TimelineSamples.ReferenceTrack("term_b", "assets/timelines/pop.htimeline", 0.3));
            DictionaryTimelineResolver resolver = new DictionaryTimelineResolver();
            resolver.Add("assets/timelines/pop.htimeline", TimelineSamples.Pop());

            CookedTimelineAsset cooked = TimelineCooker.Cook(timeline, Options(TimelineCurveMode.Native), resolver, Catalog());

            CookedTimelineTrack scale = Assert.Single(cooked.Tracks, track => track.Kind == CookedTimelineTrackKind.Transform && track.SlotIndex == 1 && track.ChannelIndex == (int)CookedTimelineTransformChannel.ScaleX);
            CookedTimelineSegment first = scale.Segments[0];
            CookedTimelineSegment last = scale.Segments[scale.Segments.Length - 1];
            Assert.Equal(0, first.StartTick);
            Assert.Equal(0.5f, first.From, 5);
            Assert.Equal(30, last.EndTick);
            Assert.Equal(0.5 + 0.5 * CurveCatalog.Evaluate(CurveCatalog.EaseOutBackCode, 0.75), last.To, 4);
            Assert.All(scale.Segments, segment => Assert.Equal(CurveCatalog.LinearCode, segment.CurveCode));
            Assert.Equal("Value slot=1 receiver=21 channel=0 mode=Absolute: [0-20 0->1 c0] [140-170 0->1 c1] [170-190 1->0.4 c0] [190-220 0.4->0.4 c0] [220-260 0.4->0.4 c0]", Track(cooked, CookedTimelineTrackKind.Value, 1, 0));
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => TimelineCooker.Cook(timeline, Options(TimelineCurveMode.Native), null, Catalog()));
            Assert.Contains("no resolver", failure.Message);
        }

        /// <summary>
        /// Where an outer track and a nested timeline drive the same component, the segment that started last wins; the
        /// outer curve is cut exactly (its first half kept) and the nested value is then held.
        /// </summary>
        [Fact]
        public void Cook_overlappingTracks_latestStartWins() {
            TimelineAsset timeline = new TimelineAsset { TimelineId = "priority", DurationSeconds = 3 };
            timeline.Slots.Add(new TimelineSlotAsset { Name = "hero" });
            TimelineTransformTrackAsset slide = new TimelineTransformTrackAsset { Slot = "hero" };
            TimelineTransformClipAsset slideClip = new TimelineTransformClipAsset { DurationSeconds = 2 };
            slideClip.Position.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0 });
            slideClip.Position.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 2, X = 10 });
            slide.Clips.Add(slideClip);
            timeline.Tracks.Add(slide);

            TimelineAsset jump = new TimelineAsset { TimelineId = "jump", DurationSeconds = 1 };
            jump.Slots.Add(new TimelineSlotAsset { Name = "target" });
            TimelineTransformTrackAsset jumpTrack = new TimelineTransformTrackAsset { Slot = "target" };
            TimelineTransformClipAsset jumpClip = new TimelineTransformClipAsset { DurationSeconds = 0.5 };
            jumpClip.Position.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0, X = 100 });
            jumpTrack.Clips.Add(jumpClip);
            jump.Tracks.Add(jumpTrack);
            TimelineNestedTrackAsset nested = new TimelineNestedTrackAsset();
            TimelineNestedClipAsset nestedClip = new TimelineNestedClipAsset { StartSeconds = 1, DurationSeconds = 1, Definition = jump };
            nestedClip.SlotMappings.Add(new TimelineSlotMappingAsset { Inner = "target", Outer = "hero" });
            nested.Clips.Add(nestedClip);
            timeline.Tracks.Insert(0, nested);

            CookedTimelineAsset cooked = TimelineCooker.Cook(timeline, Options(TimelineCurveMode.Native), null, null);

            Assert.Equal("Transform slot=0 receiver=0 channel=0 mode=Absolute: [0-100 0->5 c0] [100-150 100->100 c0]", Track(cooked, CookedTimelineTrackKind.Transform, 0, 0));
        }

        /// <summary>
        /// The cooked sample plays in a game: a player bound to entities with the receiver shows the nested pop's scale
        /// and the opacity cross-fade at the expected ticks.
        /// </summary>
        [Fact]
        public void CookedSample_playsInRuntimePlayer() {
            CookedTimelineAsset cooked = TimelineCooker.Cook(TimelineSamples.ContrastThree(), Options(TimelineCurveMode.Native), null, Catalog());
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Entity root = new Entity(core);
            root.InitChildren();
            Entity[] slots = new Entity[cooked.SlotCount];
            TextEffectReceiver[] receivers = new TextEffectReceiver[cooked.SlotCount];
            SceneEntityReference[] references = new SceneEntityReference[cooked.SlotCount];
            using RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            for (int index = 0; index < slots.Length; index++) {
                slots[index] = new Entity(core);
                slots[index].InitComponents();
                slots[index].AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = 100u + (uint)index });
                receivers[index] = new TextEffectReceiver();
                slots[index].AddComponent(receivers[index]);
                root.AddChild(slots[index]);
                references[index] = new SceneEntityReference { EntityId = 100u + (uint)index };
                fixups.Track(references[index], "timeline.player");
            }
            fixups.Bind(new List<Entity> { root });
            cooked.Tracks = cooked.Tracks.Where(track => track.Kind != CookedTimelineTrackKind.Animation && track.Kind != CookedTimelineTrackKind.Audio).ToArray();
            Entity director = new Entity(core);
            director.InitComponents();
            TimelinePlayerComponent player = new TimelinePlayerComponent { Timeline = cooked, Slots = references };
            director.AddComponent(player);

            player.Evaluate(180);
            Assert.Equal(1 + (0.4 - 1) * 0.5, receivers[1].Values[0], 5);
            player.Evaluate(250);
            Assert.Equal(0.5f, slots[2].LocalScale.X, 4);
            player.Evaluate(285);
            Assert.Equal(1f, slots[2].LocalScale.Y, 4);
            Assert.True(slots[0].Enabled);
            player.Evaluate(5);
            Assert.False(slots[0].Enabled);
        }

        /// <summary>
        /// Invalid options are rejected before cooking.
        /// </summary>
        [Fact]
        public void Cook_invalidOptions_areRejected() {
            Assert.Throws<ArgumentOutOfRangeException>(() => TimelineCooker.Cook(TimelineSamples.Pop(), new TimelineCookOptions { TickRate = 0 }, null, Catalog()));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimelineCooker.Cook(TimelineSamples.Pop(), new TimelineCookOptions { Tolerance = 0 }, null, Catalog()));
        }

        /// <summary>
        /// Builds 100 Hz cook options.
        /// </summary>
        /// <param name="mode">Curve mode.</param>
        /// <returns>The options.</returns>
        static TimelineCookOptions Options(TimelineCurveMode mode) {
            return new TimelineCookOptions { TickRate = 100, CurveMode = mode, Tolerance = 0.001 };
        }

        /// <summary>
        /// Builds the receiver catalog of the samples.
        /// </summary>
        /// <returns>The catalog.</returns>
        static TimelineReceiverCatalog Catalog() {
            TimelineReceiverCatalog catalog = new TimelineReceiverCatalog();
            catalog.Add(typeof(TextEffectReceiver));
            return catalog;
        }

        /// <summary>
        /// Describes the single track matching a kind, slot and channel.
        /// </summary>
        /// <param name="cooked">Cooked timeline.</param>
        /// <param name="kind">Track kind.</param>
        /// <param name="slot">Slot index.</param>
        /// <param name="channel">Channel index.</param>
        /// <returns>The track description.</returns>
        static string Track(CookedTimelineAsset cooked, CookedTimelineTrackKind kind, int slot, int channel) {
            CookedTimelineTrack track = Assert.Single(cooked.Tracks, candidate => candidate.Kind == kind && candidate.SlotIndex == slot && candidate.ChannelIndex == channel);
            return CookedTimelineDump.DescribeTrack(track);
        }

        /// <summary>
        /// Samples a cooked curve track at a tick the way the player does (NaN before the first segment).
        /// </summary>
        /// <param name="track">Cooked track.</param>
        /// <param name="tick">Tick.</param>
        /// <returns>The value.</returns>
        static double Sample(CookedTimelineTrack track, int tick) {
            CookedTimelineSegment current = null;
            for (int index = 0; index < track.Segments.Length; index++) {
                if (track.Segments[index].StartTick <= tick) {
                    current = track.Segments[index];
                }
            }
            return current == null ? double.NaN : current.Evaluate(tick);
        }
    }
}
