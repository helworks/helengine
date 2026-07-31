namespace helengine {
    /// <summary>
    /// Describes one engine's low-noise managed timing sample for the canonical Windows physics comparison workload.
    /// </summary>
    public sealed class HelPhysicsBenchmarkSample3D {
        /// <summary>
        /// Initializes one immutable benchmark sample from validated timing and allocation measurements.
        /// </summary>
        /// <param name="engineName">Human-readable engine label.</param>
        /// <param name="sampleCount">Number of individually timed fixed steps.</param>
        /// <param name="medianMilliseconds">Median fixed-step duration in milliseconds.</param>
        /// <param name="p95Milliseconds">Nearest-rank ninety-fifth-percentile fixed-step duration in milliseconds.</param>
        /// <param name="maximumMilliseconds">Maximum fixed-step duration in milliseconds.</param>
        /// <param name="allocatedBytes">Managed bytes allocated on the measuring thread during the timed interval.</param>
        public HelPhysicsBenchmarkSample3D(
            string engineName,
            int sampleCount,
            double medianMilliseconds,
            double p95Milliseconds,
            double maximumMilliseconds,
            long allocatedBytes) {
            if (string.IsNullOrWhiteSpace(engineName)) {
                throw new ArgumentException("An engine name is required for a benchmark sample.", nameof(engineName));
            } else if (sampleCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "A benchmark sample must contain at least one timed step.");
            } else if (!double.IsFinite(medianMilliseconds) ||
                !double.IsFinite(p95Milliseconds) ||
                !double.IsFinite(maximumMilliseconds) ||
                medianMilliseconds < 0d ||
                p95Milliseconds < 0d ||
                maximumMilliseconds < 0d) {
                throw new ArgumentOutOfRangeException(nameof(medianMilliseconds), "Benchmark durations cannot be negative.");
            } else if (allocatedBytes < 0) {
                throw new ArgumentOutOfRangeException(nameof(allocatedBytes), "Measured allocation bytes cannot be negative.");
            }

            EngineName = engineName;
            SampleCount = sampleCount;
            MedianMilliseconds = medianMilliseconds;
            P95Milliseconds = p95Milliseconds;
            MaximumMilliseconds = maximumMilliseconds;
            AllocatedBytes = allocatedBytes;
        }

        /// <summary>
        /// Gets the human-readable engine label associated with this measurement.
        /// </summary>
        public string EngineName { get; }

        /// <summary>
        /// Gets the number of individually timed fixed steps represented by this sample.
        /// </summary>
        public int SampleCount { get; }

        /// <summary>
        /// Gets the median fixed-step duration in milliseconds.
        /// </summary>
        public double MedianMilliseconds { get; }

        /// <summary>
        /// Gets the nearest-rank ninety-fifth-percentile fixed-step duration in milliseconds.
        /// </summary>
        public double P95Milliseconds { get; }

        /// <summary>
        /// Gets the maximum fixed-step duration in milliseconds.
        /// </summary>
        public double MaximumMilliseconds { get; }

        /// <summary>
        /// Gets the managed bytes allocated on the measuring thread during timed stepping.
        /// </summary>
        public long AllocatedBytes { get; }
    }
}
