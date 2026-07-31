using System.Diagnostics;

namespace helengine {
    /// <summary>
    /// Runs the canonical four-box workload through managed HelPhysics and managed BEPU for repeatable Windows orientation measurements.
    /// </summary>
    public static class HelPhysicsBenchmarkRunner3D {
        /// <summary>
        /// Warms and measures the same twenty-hertz ground-and-four-box workload through both managed physics engines.
        /// </summary>
        /// <param name="warmupStepCount">Number of untimed fixed steps performed before each engine measurement.</param>
        /// <param name="sampleCount">Positive number of fixed steps timed independently for each engine.</param>
        /// <returns>A side-by-side report containing timing, allocation, and final-state counters.</returns>
        public static HelPhysicsBenchmarkReport3D RunFourBoxStack(int warmupStepCount, int sampleCount) {
            if (warmupStepCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(warmupStepCount), "The warmup step count cannot be negative.");
            } else if (sampleCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "At least one measured step is required.");
            }

            HelPhysicsWorld3D helPhysicsWorld = CreateHelPhysicsWorld();
            long[] helPhysicsSamples = new long[sampleCount];
            StepHelPhysics(helPhysicsWorld, warmupStepCount);
            ForceCollection();
            long helPhysicsBytesBefore = GC.GetAllocatedBytesForCurrentThread();
            MeasureHelPhysics(helPhysicsWorld, helPhysicsSamples);
            long helPhysicsAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - helPhysicsBytesBefore;
            HelPhysicsStepMetrics3D finalHelPhysicsMetrics = helPhysicsWorld.LastStepMetrics;
            HelPhysicsBenchmarkSample3D helPhysicsSample = CreateSample("HelPhysics", helPhysicsSamples, helPhysicsAllocatedBytes);

            using HelPhysicsBepuBenchmarkWorld3D bepuWorld = new HelPhysicsBepuBenchmarkWorld3D();
            long[] bepuSamples = new long[sampleCount];
            StepBepu(bepuWorld, warmupStepCount);
            ForceCollection();
            long bepuBytesBefore = GC.GetAllocatedBytesForCurrentThread();
            MeasureBepu(bepuWorld, bepuSamples);
            long bepuAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bepuBytesBefore;
            HelPhysicsBenchmarkSample3D bepuSample = CreateSample("BEPU", bepuSamples, bepuAllocatedBytes);

            return new HelPhysicsBenchmarkReport3D(
                helPhysicsSample,
                bepuSample,
                finalHelPhysicsMetrics,
                bepuWorld.BodyCount,
                bepuWorld.AwakeDynamicBodyCount);
        }

        /// <summary>
        /// Creates the raw HelPhysics half of the comparison with geometry and equivalent exposed settings matched to the raw BEPU fixture.
        /// </summary>
        /// <returns>A fixed-capacity world containing one static ground and four dynamic unit boxes pending their first step.</returns>
        static HelPhysicsWorld3D CreateHelPhysicsWorld() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                32,
                32,
                128,
                64,
                256,
                32,
                128,
                4,
                1,
                HelPhysicsWorldFixture.StepSeconds,
                new PhysicsVector3(0f, -9.81f, 0f));
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsMaterial3D material = new HelPhysicsMaterial3D(
                PhysicsScalar.FromFloat(0.6f),
                PhysicsScalar.FromFloat(0.6f),
                PhysicsScalar.Zero);
            world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(5f, 0.5f, 5f)),
                BodyKind3D.Static,
                new PhysicsVector3(0f, -0.5f, 0f),
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.Zero,
                material,
                1,
                ushort.MaxValue,
                0,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                HelPhysicsWorldFixture.SleepTicks,
                false));
            for (int boxIndex = 0; boxIndex < 4; boxIndex++) {
                world.CreateBody(new HelPhysicsBodyDescription3D(
                    new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                    BodyKind3D.Dynamic,
                    new PhysicsVector3(0f, 0.5f + boxIndex, 0f),
                    PhysicsQuaternion.Identity,
                    PhysicsVector3.Zero,
                    PhysicsVector3.Zero,
                    PhysicsScalar.One,
                    material,
                    1,
                    ushort.MaxValue,
                    boxIndex + 1,
                    PhysicsScalar.One,
                    PhysicsScalar.Zero,
                    PhysicsScalar.Zero,
                    PhysicsScalar.FromFloat(0.2f),
                    PhysicsScalar.FromFloat(0.2f),
                    HelPhysicsWorldFixture.SleepTicks,
                    true));
            }

            return world;
        }

        /// <summary>
        /// Calculates the median timestamp delta from a sorted sample prefix, averaging the two middle values for even counts.
        /// </summary>
        /// <param name="sortedSamples">Ascending timestamp deltas.</param>
        /// <param name="sampleCount">Positive prefix length to inspect.</param>
        /// <returns>Median timestamp ticks, including a half tick when required.</returns>
        public static double CalculateMedianTicks(long[] sortedSamples, int sampleCount) {
            ValidateSampleRange(sortedSamples, sampleCount);
            int middleIndex = sampleCount / 2;
            if ((sampleCount & 1) != 0) {
                return sortedSamples[middleIndex];
            }

            return (sortedSamples[middleIndex - 1] / 2d) + (sortedSamples[middleIndex] / 2d);
        }

        /// <summary>
        /// Selects one nearest-rank percentile timestamp delta from a sorted sample prefix.
        /// </summary>
        /// <param name="sortedSamples">Ascending timestamp deltas.</param>
        /// <param name="sampleCount">Positive prefix length to inspect.</param>
        /// <param name="percentile">Percentile in the interval greater than zero through one.</param>
        /// <returns>The timestamp delta at the requested nearest rank.</returns>
        public static long SelectPercentileTicks(long[] sortedSamples, int sampleCount, double percentile) {
            ValidateSampleRange(sortedSamples, sampleCount);
            if (double.IsNaN(percentile) || double.IsInfinity(percentile) || percentile <= 0d || percentile > 1d) {
                throw new ArgumentOutOfRangeException(nameof(percentile), "A percentile must be finite and greater than zero through one.");
            }

            int rank = (int)Math.Ceiling(sampleCount * percentile);
            return sortedSamples[rank - 1];
        }

        /// <summary>
        /// Advances one HelPhysics world without timing or test-framework work inside the warmup loop.
        /// </summary>
        /// <param name="world">HelPhysics world to warm.</param>
        /// <param name="stepCount">Non-negative number of fixed steps.</param>
        static void StepHelPhysics(HelPhysicsWorld3D world, int stepCount) {
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++) {
                world.Step(HelPhysicsWorldFixture.StepSeconds);
            }
        }

        /// <summary>
        /// Advances one BEPU world without timing or test-framework work inside the warmup loop.
        /// </summary>
        /// <param name="world">BEPU world to warm.</param>
        /// <param name="stepCount">Non-negative number of fixed steps.</param>
        static void StepBepu(HelPhysicsBepuBenchmarkWorld3D world, int stepCount) {
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++) {
                world.Step(HelPhysicsWorldFixture.StepSeconds);
            }
        }

        /// <summary>
        /// Records one timestamp delta for each requested HelPhysics fixed step into preallocated storage.
        /// </summary>
        /// <param name="world">Warmed HelPhysics world to measure.</param>
        /// <param name="samples">Preallocated destination receiving timestamp deltas.</param>
        static void MeasureHelPhysics(HelPhysicsWorld3D world, long[] samples) {
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
                long startTimestamp = Stopwatch.GetTimestamp();
                world.Step(HelPhysicsWorldFixture.StepSeconds);
                samples[sampleIndex] = Stopwatch.GetTimestamp() - startTimestamp;
            }
        }

        /// <summary>
        /// Records one timestamp delta for each requested BEPU fixed step into preallocated storage.
        /// </summary>
        /// <param name="world">Warmed BEPU world to measure.</param>
        /// <param name="samples">Preallocated destination receiving timestamp deltas.</param>
        static void MeasureBepu(HelPhysicsBepuBenchmarkWorld3D world, long[] samples) {
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
                long startTimestamp = Stopwatch.GetTimestamp();
                world.Step(HelPhysicsWorldFixture.StepSeconds);
                samples[sampleIndex] = Stopwatch.GetTimestamp() - startTimestamp;
            }
        }

        /// <summary>
        /// Sorts completed timestamp samples and converts their summary statistics to milliseconds.
        /// </summary>
        /// <param name="engineName">Human-readable engine label.</param>
        /// <param name="samples">Completed timestamp deltas to summarize in place.</param>
        /// <param name="allocatedBytes">Managed bytes allocated during the associated timed interval.</param>
        /// <returns>An immutable benchmark sample.</returns>
        static HelPhysicsBenchmarkSample3D CreateSample(string engineName, long[] samples, long allocatedBytes) {
            Array.Sort(samples);
            double millisecondsPerTick = 1000d / Stopwatch.Frequency;
            return new HelPhysicsBenchmarkSample3D(
                engineName,
                samples.Length,
                CalculateMedianTicks(samples, samples.Length) * millisecondsPerTick,
                SelectPercentileTicks(samples, samples.Length, 0.95d) * millisecondsPerTick,
                samples[samples.Length - 1] * millisecondsPerTick,
                allocatedBytes);
        }

        /// <summary>
        /// Validates a sorted timing array and the positive prefix requested by a statistic calculation.
        /// </summary>
        /// <param name="sortedSamples">Timing sample array to inspect.</param>
        /// <param name="sampleCount">Positive prefix length that must fit within the array.</param>
        static void ValidateSampleRange(long[] sortedSamples, int sampleCount) {
            if (sortedSamples == null) {
                throw new ArgumentNullException(nameof(sortedSamples));
            } else if (sampleCount <= 0 || sampleCount > sortedSamples.Length) {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "The sample count must select a non-empty prefix of the timing array.");
            }
        }

        /// <summary>
        /// Forces pending managed collections to finish before measuring current-thread allocations.
        /// </summary>
        static void ForceCollection() {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
