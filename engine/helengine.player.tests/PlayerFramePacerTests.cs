namespace helengine.player.tests {
    /// <summary>
    /// Verifies the player frame pacer's scheduling math against a simulated clock.
    /// </summary>
    public sealed class PlayerFramePacerTests {
        /// <summary>Frame slot length at 60 FPS.</summary>
        const double SixtyFpsFrameSeconds = 1d / 60d;

        /// <summary>The pacer refuses a frame rate that is not positive and finite.</summary>
        /// <param name="targetFrameRate">Invalid frame rate.</param>
        [Theory]
        [InlineData(0d)]
        [InlineData(-60d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Ctor_WhenTargetFrameRateNotPositive_Throws(double targetFrameRate) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerFramePacer(targetFrameRate, new SimulatedPlayerFrameClock(0d)));
        }

        /// <summary>The pacer requires a clock.</summary>
        [Fact]
        public void Ctor_WhenClockNull_Throws() {
            Assert.Throws<ArgumentNullException>(() => new PlayerFramePacer(60d, null));
        }

        /// <summary>Waiting before the pacer was started is a programming error.</summary>
        [Fact]
        public void WaitForNextFrame_BeforeStart_Throws() {
            PlayerFramePacer pacer = new PlayerFramePacer(60d, new SimulatedPlayerFrameClock(0d));
            Assert.Throws<InvalidOperationException>(() => pacer.WaitForNextFrame());
        }

        /// <summary>
        /// A frame that finishes early waits until its slot ends: it sleeps for the bulk of the remaining time, leaving
        /// only the spin threshold, then spins up to the deadline without passing it by more than one spin step.
        /// </summary>
        [Fact]
        public void WaitForNextFrame_WhenFrameFinishesEarly_SleepsThenSpinsToDeadline() {
            SimulatedPlayerFrameClock clock = new SimulatedPlayerFrameClock(0d);
            PlayerFramePacer pacer = new PlayerFramePacer(60d, clock);
            pacer.Start();
            clock.Advance(0.005d);

            pacer.WaitForNextFrame();

            Assert.Equal(new[] { 9 }, clock.SleepRequests);
            Assert.True(clock.SpinCount > 0);
            Assert.InRange(clock.ElapsedSeconds, SixtyFpsFrameSeconds, SixtyFpsFrameSeconds + SimulatedPlayerFrameClock.SpinStepSeconds);
            Assert.Equal(2d * SixtyFpsFrameSeconds, pacer.NextFrameDeadlineSeconds, 9);
        }

        /// <summary>
        /// A sleep that overshoots (a coarse OS timer) is followed by another, shorter sleep or by spinning, and the
        /// frame still ends at its deadline instead of oversleeping it.
        /// </summary>
        [Fact]
        public void WaitForNextFrame_WhenSleepOvershoots_StillEndsAtDeadline() {
            SimulatedPlayerFrameClock clock = new SimulatedPlayerFrameClock(0.0015d);
            PlayerFramePacer pacer = new PlayerFramePacer(60d, clock);
            pacer.Start();

            pacer.WaitForNextFrame();

            Assert.InRange(clock.ElapsedSeconds, SixtyFpsFrameSeconds, SixtyFpsFrameSeconds + SimulatedPlayerFrameClock.SpinStepSeconds);
        }

        /// <summary>
        /// Consecutive frames end on the fixed 60 FPS cadence measured from <see cref="PlayerFramePacer.Start"/>,
        /// so per-wait rounding never accumulates into drift.
        /// </summary>
        [Fact]
        public void WaitForNextFrame_OverManyFrames_KeepsFixedCadenceWithoutDrift() {
            SimulatedPlayerFrameClock clock = new SimulatedPlayerFrameClock(0.0003d);
            PlayerFramePacer pacer = new PlayerFramePacer(60d, clock);
            pacer.Start();

            for (int frame = 1; frame <= 600; frame++) {
                clock.Advance(0.004d);
                pacer.WaitForNextFrame();
                double expectedEndSeconds = frame * SixtyFpsFrameSeconds;
                Assert.InRange(clock.ElapsedSeconds, expectedEndSeconds - 1e-9, expectedEndSeconds + SimulatedPlayerFrameClock.SpinStepSeconds);
            }
        }

        /// <summary>
        /// A frame that overruns its slot by less than a frame does not wait and keeps the original cadence, so the
        /// next frame gets the rest of its own slot.
        /// </summary>
        [Fact]
        public void WaitForNextFrame_WhenFrameOverrunsByLessThanAFrame_KeepsCadence() {
            SimulatedPlayerFrameClock clock = new SimulatedPlayerFrameClock(0d);
            PlayerFramePacer pacer = new PlayerFramePacer(60d, clock);
            pacer.Start();
            clock.Advance(0.020d);

            pacer.WaitForNextFrame();

            Assert.Empty(clock.SleepRequests);
            Assert.Equal(0, clock.SpinCount);
            Assert.Equal(0.020d, clock.ElapsedSeconds, 9);
            Assert.Equal(2d * SixtyFpsFrameSeconds, pacer.NextFrameDeadlineSeconds, 9);
        }

        /// <summary>
        /// A frame that overruns by a whole frame or more restarts the cadence from now instead of letting a burst of
        /// unpaced catch-up frames run.
        /// </summary>
        [Fact]
        public void WaitForNextFrame_WhenFrameOverrunsByAWholeFrame_RestartsCadenceFromNow() {
            SimulatedPlayerFrameClock clock = new SimulatedPlayerFrameClock(0d);
            PlayerFramePacer pacer = new PlayerFramePacer(60d, clock);
            pacer.Start();
            clock.Advance(0.100d);

            pacer.WaitForNextFrame();

            Assert.Empty(clock.SleepRequests);
            Assert.Equal(0.100d + SixtyFpsFrameSeconds, pacer.NextFrameDeadlineSeconds, 9);
        }
    }
}
