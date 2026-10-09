namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Scrubs the rich sample tick by tick with <see cref="TimelinePlayerComponent.Evaluate"/> and checks transforms,
    /// receiver values, activation and animation poses.
    /// </summary>
    public sealed class TimelinePlayerEvaluateTests {
        /// <summary>
        /// Before the value segment starts, the receiver hears nothing; transform groups the timeline drives show the
        /// bound transform plus the active absolute values; the door is inactive outside its intervals.
        /// </summary>
        [Fact]
        public void Evaluate_earlyTick_appliesOnlyStartedTracks() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            scene.Player.Evaluate(1);

            Assert.Equal(new float3(1, 2, 3), scene.Hero.LocalPosition);
            Assert.Equal(new float3(3, 3, 3), scene.Hero.LocalScale);
            Assert.Equal(0, scene.Receiver.Calls[1]);
            Assert.False(scene.Door.Enabled);
            Assert.Null(scene.Animator.CurrentClip);
        }

        /// <summary>
        /// Mid-timeline ticks interpolate segments, hold finished ones, multiply the offset scale and toggle activation.
        /// </summary>
        [Fact]
        public void Evaluate_midTicks_interpolateAndHold() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            scene.Player.Evaluate(5);
            Assert.Equal(5f, scene.Hero.LocalPosition.X, 4);
            Assert.Equal(3f, scene.Hero.LocalScale.Y, 4);
            Assert.Equal(3.0, scene.Receiver.Values[1], 6);
            Assert.True(scene.Door.Enabled);

            scene.Player.Evaluate(8);
            Assert.Equal(8f, scene.Hero.LocalPosition.X, 4);
            Assert.Equal(4.8f, scene.Hero.LocalScale.Y, 4);
            Assert.Equal(3f, scene.Hero.LocalScale.X, 4);
            Assert.Equal(4.0, scene.Receiver.Values[1], 6);
            Assert.False(scene.Door.Enabled);

            scene.Player.Evaluate(12);
            Assert.Equal(10f, scene.Hero.LocalPosition.X, 4);
            Assert.Equal(6f, scene.Hero.LocalScale.Y, 4);
            Assert.True(scene.Door.Enabled);
        }

        /// <summary>
        /// The smoothstep segment is evaluated with the catalog curve, and the end tick keeps slots whose interval reaches
        /// the end active.
        /// </summary>
        [Fact]
        public void Evaluate_curveAndEndTick() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            scene.Player.Evaluate(15);
            Assert.Equal(10 - 10 * CurveCatalog.Evaluate(CurveCatalog.SmoothstepCode, 0.25), scene.Hero.LocalPosition.X, 4);
            scene.Player.Evaluate(16);
            Assert.Equal(5f, scene.Hero.LocalPosition.X, 4);

            scene.Player.Evaluate(20);
            Assert.Equal(0f, scene.Hero.LocalPosition.X, 4);
            Assert.True(scene.Door.Enabled);
        }

        /// <summary>
        /// The animation track switches the actor's animator to its clip, paused, and seeks it to clip-in plus elapsed
        /// time times speed, holding the last pose after the clip ends.
        /// </summary>
        [Fact]
        public void Evaluate_animationTrack_seeksAnimator() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            scene.Player.Evaluate(12);
            Assert.Same(scene.Animation, scene.Animator.CurrentClip);
            Assert.True(scene.Animator.IsPaused);
            Assert.Equal(0.9f, scene.Animator.CurrentTime, 4);

            scene.Player.Evaluate(18);
            Assert.Equal(1.7f, scene.Animator.CurrentTime, 4);
        }

        /// <summary>
        /// Transforms and activation depend only on the tick: scrubbing backwards reproduces exactly what playing forward
        /// produced.
        /// </summary>
        [Fact]
        public void Evaluate_isHistoryIndependent() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            float3[] positions = new float3[21];
            float3[] scales = new float3[21];
            bool[] active = new bool[21];
            for (int tick = 0; tick <= 20; tick++) {
                scene.Player.Evaluate(tick);
                positions[tick] = scene.Hero.LocalPosition;
                scales[tick] = scene.Hero.LocalScale;
                active[tick] = scene.Door.Enabled;
            }

            for (int tick = 20; tick >= 0; tick -= 3) {
                scene.Player.Evaluate(tick);
                Assert.Equal(positions[tick], scene.Hero.LocalPosition);
                Assert.Equal(scales[tick], scene.Hero.LocalScale);
                Assert.Equal(active[tick], scene.Door.Enabled);
            }
        }

        /// <summary>
        /// Binding resolves each referenced asset exactly once, however many ticks are evaluated.
        /// </summary>
        [Fact]
        public void Bind_resolvesAssetsOnce() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            for (int tick = 0; tick <= 20; tick++) {
                scene.Player.Evaluate(tick);
            }

            Assert.Equal(2, scene.Assets.Resolutions);
            Assert.True(scene.Player.IsBound);
        }

        /// <summary>
        /// A value track whose receiver id is not on the slot entity fails at bind time with a readable message.
        /// </summary>
        [Fact]
        public void Bind_missingReceiver_throws() {
            CookedTimelineAsset timeline = SampleCookedTimelines.Rich();
            timeline.Tracks[2].ReceiverId = 99;
            using PlayerScene scene = new PlayerScene(timeline, new RecordingAudioBackend());

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => scene.Player.Play());

            Assert.Contains("slot 'lamp'", failure.Message);
            Assert.Contains("id 99", failure.Message);
        }

        /// <summary>
        /// Playing the transform, value and activation tracks allocates nothing per frame.
        /// </summary>
        [Fact]
        public void Advance_transformValueActivation_allocatesNothing() {
            CookedTimelineAsset timeline = SampleCookedTimelines.Rich();
            timeline.Tracks = new CookedTimelineTrack[] { timeline.Tracks[0], timeline.Tracks[1], timeline.Tracks[2], timeline.Tracks[3] };
            using PlayerScene scene = new PlayerScene(timeline, new RecordingAudioBackend());
            scene.Player.EndMode = TimelineEndMode.Loop;
            scene.Player.Play();
            scene.AdvanceTicks(45);

            long before = GC.GetAllocatedBytesForCurrentThread();
            scene.AdvanceTicks(45);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocated);
        }
    }
}
