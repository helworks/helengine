namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Plays the rich sample through <see cref="TimelinePlayerComponent.Advance"/> and checks events, seeking, end modes,
    /// speed and audio requests.
    /// </summary>
    public sealed class TimelinePlayerPlaybackTests {
        /// <summary>
        /// Events fire once as playback crosses them, including the tick-0 event on play and the end event; the Stop end
        /// mode stops at the end.
        /// </summary>
        [Fact]
        public void Play_firesEventsOnceAndStopsAtEnd() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            scene.Player.Play();
            Assert.Equal(new string[] { "start=" }, scene.Listener.Events);
            scene.AdvanceTicks(5);
            Assert.Equal(new string[] { "start=", "mid=x" }, scene.Listener.Events);
            scene.AdvanceTicks(14);
            Assert.Equal(2, scene.Listener.Events.Count);
            Assert.True(scene.Player.IsPlaying);
            scene.AdvanceTicks(1);

            Assert.Equal(new string[] { "start=", "mid=x", "end=" }, scene.Listener.Events);
            Assert.False(scene.Player.IsPlaying);
            Assert.Equal(2.0, scene.Player.Time, 9);
            Assert.Equal(0f, scene.Hero.LocalPosition.X, 4);
        }

        /// <summary>
        /// Seeking forward skips the events of the skipped span; playing on fires later ones.
        /// </summary>
        [Fact]
        public void SeekForward_skipsEventsInSkippedSpan() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            scene.Player.Play();
            scene.AdvanceTicks(2);

            scene.Player.Seek(1.0);
            Assert.Equal(10, scene.Player.CurrentTick);
            Assert.Equal(10f, scene.Hero.LocalPosition.X, 4);
            scene.AdvanceTicks(10);

            Assert.Equal(new string[] { "start=", "end=" }, scene.Listener.Events);
        }

        /// <summary>
        /// Seeking backwards recomputes the cursors, so an event crossed again fires again; a marker exactly at the seek
        /// target does not fire.
        /// </summary>
        [Fact]
        public void SeekBackward_replaysCrossedEvents_butNotTheTarget() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            scene.Player.Play();
            scene.AdvanceTicks(6);

            scene.Player.Seek(0.3);
            Assert.Equal(3f, scene.Hero.LocalPosition.X, 4);
            scene.AdvanceTicks(3);
            Assert.Equal(new string[] { "start=", "mid=x", "mid=x" }, scene.Listener.Events);

            scene.Player.Seek(0.5);
            scene.AdvanceTicks(3);
            Assert.Equal(3, scene.Listener.Events.Count);
        }

        /// <summary>
        /// Seeking a stopped player applies the target and pauses there; play resumes from it.
        /// </summary>
        [Fact]
        public void SeekWhileStopped_pausesAtTargetAndPlayResumes() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());

            scene.Player.Seek(1.2);
            Assert.True(scene.Player.IsPaused);
            Assert.True(scene.Door.Enabled);
            scene.Player.Play();
            scene.AdvanceTicks(1);

            Assert.Equal(13, scene.Player.CurrentTick);
            Assert.Empty(scene.Listener.Events);
        }

        /// <summary>
        /// The Loop end mode wraps around and fires the start events again.
        /// </summary>
        [Fact]
        public void Loop_wrapsAndReplaysEvents() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            scene.Player.EndMode = TimelineEndMode.Loop;

            scene.Player.Play();
            scene.AdvanceTicks(25);

            Assert.Equal(new string[] { "start=", "mid=x", "end=", "start=", "mid=x" }, scene.Listener.Events);
            Assert.True(scene.Player.IsPlaying);
            Assert.Equal(0.5, scene.Player.Time, 6);
            Assert.Equal(5f, scene.Hero.LocalPosition.X, 4);
        }

        /// <summary>
        /// The Hold end mode stays at the end, keeps playing and re-applies the final frame.
        /// </summary>
        [Fact]
        public void Hold_staysAtEnd() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            scene.Player.EndMode = TimelineEndMode.Hold;
            scene.Player.Play();
            scene.AdvanceTicks(22);

            scene.Hero.LocalPosition = new float3(50, 0, 0);
            scene.AdvanceTicks(1);

            Assert.True(scene.Player.IsPlaying);
            Assert.Equal(2.0, scene.Player.Time, 9);
            Assert.Equal(0f, scene.Hero.LocalPosition.X, 4);
            Assert.True(scene.Door.Enabled);
            Assert.Equal(new string[] { "start=", "mid=x", "end=" }, scene.Listener.Events);
        }

        /// <summary>
        /// Stop rewinds; the next play starts over and fires the start event again.
        /// </summary>
        [Fact]
        public void Stop_thenPlay_restarts() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            scene.Player.Play();
            scene.AdvanceTicks(7);

            scene.Player.Stop();
            Assert.Equal(0, scene.Player.Time);
            scene.Player.Play();

            Assert.Equal(new string[] { "start=", "mid=x", "start=" }, scene.Listener.Events);
            Assert.Equal(0, scene.Player.CurrentTick);
        }

        /// <summary>
        /// Speed scales the time step.
        /// </summary>
        [Fact]
        public void Speed_scalesTime() {
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), new RecordingAudioBackend());
            scene.Player.Speed = 2f;
            scene.Player.Play();

            scene.AdvanceTicks(4);

            Assert.Equal(8, scene.Player.CurrentTick);
            Assert.Equal(8f, scene.Hero.LocalPosition.X, 4);
        }

        /// <summary>
        /// A sound starts when playback crosses its start, offset by its clip-in and with its gain, and stops at its end.
        /// </summary>
        [Fact]
        public void Audio_startsAtClipStartWithClipInAndStopsAtEnd() {
            SeekableRecordingAudioBackend backend = new SeekableRecordingAudioBackend();
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), backend);
            scene.Player.Play();

            scene.AdvanceTicks(2);
            Assert.Empty(backend.PlayedOffsets);
            scene.AdvanceTicks(1);
            Assert.Equal(new float[] { 0.2f }, backend.PlayedOffsets);
            Assert.Equal(new float[] { 0.5f }, backend.PlayedGains);
            scene.AdvanceTicks(5);
            Assert.Empty(backend.StoppedVoices);
            scene.AdvanceTicks(1);

            Assert.Equal(new int[] { 1 }, backend.StoppedVoices);
        }

        /// <summary>
        /// Seeking into a sound restarts it with a start offset on a backend that can seek.
        /// </summary>
        [Fact]
        public void Audio_seekIntoClip_requestsStartOffset() {
            SeekableRecordingAudioBackend backend = new SeekableRecordingAudioBackend();
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), backend);
            scene.Player.Play();
            scene.AdvanceTicks(4);

            scene.Player.Seek(0.6);

            Assert.Equal(new int[] { 1 }, backend.StoppedVoices);
            Assert.Equal(2, backend.PlayedOffsets.Count);
            Assert.Equal(0.5f, backend.PlayedOffsets[1], 5);
        }

        /// <summary>
        /// On a backend that cannot seek, a seek into a sound skips it, while a normal crossing still plays it.
        /// </summary>
        [Fact]
        public void Audio_nonSeekableBackend_skipsMidClipStarts() {
            RecordingAudioBackend backend = new RecordingAudioBackend();
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), backend);
            scene.Player.Play();

            scene.Player.Seek(0.6);
            scene.AdvanceTicks(5);
            Assert.Empty(backend.PlayedOffsets);

            scene.Player.Seek(0.1);
            scene.AdvanceTicks(2);
            Assert.Single(backend.PlayedOffsets);
        }

        /// <summary>
        /// Pausing silences the sound and resuming restarts it at the paused position.
        /// </summary>
        [Fact]
        public void Audio_pauseAndResume_restartsAtPosition() {
            SeekableRecordingAudioBackend backend = new SeekableRecordingAudioBackend();
            using PlayerScene scene = new PlayerScene(SampleCookedTimelines.Rich(), backend);
            scene.Player.Play();
            scene.AdvanceTicks(4);

            scene.Player.Pause();
            scene.AdvanceTicks(3);
            Assert.Equal(4, scene.Player.CurrentTick);
            scene.Player.Play();

            Assert.Equal(new int[] { 1 }, backend.StoppedVoices);
            Assert.Equal(0.3f, backend.PlayedOffsets[1], 5);
        }
    }
}
