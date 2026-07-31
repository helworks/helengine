namespace helengine {
    /// <summary>
    /// Groups side-by-side managed Windows measurements and final workload counters for HelPhysics and BEPU.
    /// </summary>
    public sealed class HelPhysicsBenchmarkReport3D {
        /// <summary>
        /// Initializes one comparison report from completed engine samples and their final runtime counters.
        /// </summary>
        /// <param name="helPhysics">Managed HelPhysics timing and allocation sample.</param>
        /// <param name="bepu">Managed BEPU timing and allocation sample.</param>
        /// <param name="finalHelPhysicsMetrics">Counters published by the final measured HelPhysics step.</param>
        /// <param name="finalBepuBodyCount">Bodies registered in the BEPU scene after measurement.</param>
        /// <param name="finalBepuAwakeBodyCount">Dynamic BEPU bodies awake after the final measured step.</param>
        public HelPhysicsBenchmarkReport3D(
            HelPhysicsBenchmarkSample3D helPhysics,
            HelPhysicsBenchmarkSample3D bepu,
            HelPhysicsStepMetrics3D finalHelPhysicsMetrics,
            int finalBepuBodyCount,
            int finalBepuAwakeBodyCount) {
            if (helPhysics == null) {
                throw new ArgumentNullException(nameof(helPhysics));
            } else if (bepu == null) {
                throw new ArgumentNullException(nameof(bepu));
            } else if (finalBepuBodyCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(finalBepuBodyCount), "The final BEPU body count cannot be negative.");
            } else if (finalBepuAwakeBodyCount < 0 || finalBepuAwakeBodyCount > finalBepuBodyCount) {
                throw new ArgumentOutOfRangeException(nameof(finalBepuAwakeBodyCount), "The final awake count must fit within the registered BEPU body count.");
            }

            HelPhysics = helPhysics;
            Bepu = bepu;
            FinalHelPhysicsMetrics = finalHelPhysicsMetrics;
            FinalBepuBodyCount = finalBepuBodyCount;
            FinalBepuAwakeBodyCount = finalBepuAwakeBodyCount;
        }

        /// <summary>
        /// Gets the managed HelPhysics timing and allocation sample.
        /// </summary>
        public HelPhysicsBenchmarkSample3D HelPhysics { get; }

        /// <summary>
        /// Gets the managed BEPU timing and allocation sample.
        /// </summary>
        public HelPhysicsBenchmarkSample3D Bepu { get; }

        /// <summary>
        /// Gets the complete counters published by the final measured HelPhysics step.
        /// </summary>
        public HelPhysicsStepMetrics3D FinalHelPhysicsMetrics { get; }

        /// <summary>
        /// Gets the number of bodies registered in the BEPU scene after measurement.
        /// </summary>
        public int FinalBepuBodyCount { get; }

        /// <summary>
        /// Gets the number of dynamic BEPU bodies awake after the final measured step.
        /// </summary>
        public int FinalBepuAwakeBodyCount { get; }
    }
}
