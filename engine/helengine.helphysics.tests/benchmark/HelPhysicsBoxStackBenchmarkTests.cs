namespace helengine {
    /// <summary>
    /// Verifies the Windows comparison harness reports repeatable timing and final-state contracts without treating managed timing ratios as console gates.
    /// </summary>
    [Collection(HelPhysicsBenchmarkCollection.Name)]
    public sealed class HelPhysicsBoxStackBenchmarkTests {
        /// <summary>
        /// Verifies a short canonical four-box run records both engines, useful final counters, and allocation-free HelPhysics stepping.
        /// </summary>
        [Fact]
        public void RunFourBoxStack_WithShortMeasurement_ReportsTimingCountersAndZeroHelPhysicsAllocations() {
            HelPhysicsBenchmarkReport3D report = HelPhysicsBenchmarkRunner3D.RunFourBoxStack(16, 32);

            Assert.Equal(32, report.HelPhysics.SampleCount);
            Assert.Equal(32, report.Bepu.SampleCount);
            Assert.True(report.HelPhysics.MedianMilliseconds > 0d);
            Assert.True(report.HelPhysics.P95Milliseconds > 0d);
            Assert.True(report.HelPhysics.MaximumMilliseconds > 0d);
            Assert.True(report.Bepu.MedianMilliseconds > 0d);
            Assert.True(report.Bepu.P95Milliseconds > 0d);
            Assert.True(report.Bepu.MaximumMilliseconds > 0d);
            Assert.Equal(0, report.HelPhysics.AllocatedBytes);
            Assert.Equal(5, report.FinalHelPhysicsMetrics.BodyCount);
            Assert.InRange(report.FinalHelPhysicsMetrics.ContactPointCount, 0, 256);
            Assert.InRange(report.FinalHelPhysicsMetrics.ManifoldCount, 0, 64);
            Assert.True(report.FinalHelPhysicsMetrics.ContactPointCount >= report.FinalHelPhysicsMetrics.ManifoldCount);
            Assert.Equal(5, report.FinalBepuBodyCount);
            Assert.InRange(report.FinalBepuAwakeBodyCount, 0, 4);
        }

        /// <summary>
        /// Verifies median and nearest-rank percentile selection against fixed sorted samples without relying on wall-clock behavior.
        /// </summary>
        [Fact]
        public void TimingSelection_WithKnownSamples_ReturnsMedianAndP95() {
            long[] oddSamples = [10, 20, 30, 40, 50];
            long[] evenSamples = [10, 20, 30, 40];

            Assert.Equal(30d, HelPhysicsBenchmarkRunner3D.CalculateMedianTicks(oddSamples, oddSamples.Length));
            Assert.Equal(25d, HelPhysicsBenchmarkRunner3D.CalculateMedianTicks(evenSamples, evenSamples.Length));
            Assert.Equal(50L, HelPhysicsBenchmarkRunner3D.SelectPercentileTicks(oddSamples, oddSamples.Length, 0.95d));
        }

        /// <summary>
        /// Verifies public sample construction rejects non-finite durations instead of admitting unusable benchmark reports.
        /// </summary>
        [Fact]
        public void Sample_WithNonFiniteTiming_RejectsInvalidReportValues() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsBenchmarkSample3D(
                "HelPhysics",
                1,
                double.NaN,
                double.PositiveInfinity,
                1d,
                0));
        }
    }
}
