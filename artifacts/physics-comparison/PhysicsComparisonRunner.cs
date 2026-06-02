namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Coordinates reference and engine trace generation for the stacked-box and sphere-stack scenes.
    /// </summary>
    public sealed class PhysicsComparisonRunner {
        /// <summary>
        /// Number of fixed simulation steps captured by the comparison harness.
        /// </summary>
        const int StepCount = 1200;

        /// <summary>
        /// Fixed time step duration used by both engines.
        /// </summary>
        const float StepSeconds = 1f / 60f;

        /// <summary>
        /// Runs the BEPU reference scene, runs the helengine scene, and writes the comparison summary.
        /// </summary>
        public void Run() {
            string outputDirectoryPath = ResolveOutputDirectoryPath();
            Directory.CreateDirectory(outputDirectoryPath);

            BepuStackedBoxTraceRunner bepuRunner = new BepuStackedBoxTraceRunner();
            IReadOnlyList<PhysicsTraceSample> bepuSamples = bepuRunner.Run(outputDirectoryPath, StepCount, StepSeconds);

            HelengineStackedBoxTraceRunner helengineRunner = new HelengineStackedBoxTraceRunner();
            IReadOnlyList<PhysicsTraceSample> helengineSamples = helengineRunner.Run(outputDirectoryPath, StepCount, StepSeconds);

            TraceComparisonReporter reporter = new TraceComparisonReporter();
            reporter.WriteReport(outputDirectoryPath, bepuSamples, helengineSamples);

            BepuSphereStackTraceRunner bepuSphereRunner = new BepuSphereStackTraceRunner();
            IReadOnlyList<PhysicsTraceSample> bepuSphereSamples = bepuSphereRunner.Run(outputDirectoryPath, StepCount, StepSeconds);

            HelengineSphereStackTraceRunner helengineSphereRunner = new HelengineSphereStackTraceRunner();
            IReadOnlyList<PhysicsTraceSample> helengineSphereSamples = helengineSphereRunner.Run(outputDirectoryPath, StepCount, StepSeconds);

            SphereStackComparisonReporter sphereReporter = new SphereStackComparisonReporter();
            sphereReporter.WriteReport(outputDirectoryPath, bepuSphereSamples, helengineSphereSamples);

            BepuDynamicStackBoxesTraceRunner bepuDynamicStackRunner = new BepuDynamicStackBoxesTraceRunner();
            IReadOnlyList<PhysicsTraceSample> bepuDynamicStackSamples = bepuDynamicStackRunner.Run(outputDirectoryPath, StepCount, StepSeconds);

            HelengineDynamicStackBoxesTraceRunner helengineDynamicStackRunner = new HelengineDynamicStackBoxesTraceRunner();
            IReadOnlyList<PhysicsTraceSample> helengineDynamicStackSamples = helengineDynamicStackRunner.Run(outputDirectoryPath, StepCount, StepSeconds);

            FrameReplayComparisonReporter dynamicStackFixedStepReporter = new FrameReplayComparisonReporter();
            dynamicStackFixedStepReporter.WriteReport(outputDirectoryPath, bepuDynamicStackSamples, helengineDynamicStackSamples, "dynamic-stack-fixed-step-comparison-summary.txt");

            float[] frameDeltas = FrameReplaySequenceLibrary.CreateSingleHitchRecoverySequence();
            BepuDynamicStackBoxesFrameReplayRunner bepuFrameReplayRunner = new BepuDynamicStackBoxesFrameReplayRunner();
            IReadOnlyList<PhysicsTraceSample> bepuFrameReplaySamples = bepuFrameReplayRunner.Run(outputDirectoryPath, frameDeltas, StepSeconds);

            HelengineDynamicStackBoxesFrameReplayRunner helengineFrameReplayRunner = new HelengineDynamicStackBoxesFrameReplayRunner();
            IReadOnlyList<PhysicsTraceSample> helengineFrameReplaySamples = helengineFrameReplayRunner.Run(outputDirectoryPath, frameDeltas);

            FrameReplayComparisonReporter frameReplayReporter = new FrameReplayComparisonReporter();
            frameReplayReporter.WriteReport(outputDirectoryPath, bepuFrameReplaySamples, helengineFrameReplaySamples, "frame-replay-comparison-summary.txt");

            Console.WriteLine("Trace output: " + outputDirectoryPath);
        }

        /// <summary>
        /// Resolves the stable artifact output directory beside the harness project instead of inside build output.
        /// </summary>
        /// <returns>Absolute output directory path.</returns>
        static string ResolveOutputDirectoryPath() {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        }
    }
}
