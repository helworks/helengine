using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Stores one per-body physics state sample for CSV output and comparison.
    /// </summary>
    public sealed class PhysicsTraceSample {
        /// <summary>
        /// Gets or sets the engine that produced this sample.
        /// </summary>
        public string EngineName { get; set; }

        /// <summary>
        /// Gets or sets the fixed-step index.
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Gets or sets elapsed simulation time in seconds.
        /// </summary>
        public float TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the body label within the scene.
        /// </summary>
        public string BodyName { get; set; }

        /// <summary>
        /// Gets or sets the sampled world position.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Gets or sets the sampled world orientation.
        /// </summary>
        public Quaternion Orientation { get; set; }

        /// <summary>
        /// Gets or sets the sampled linear velocity.
        /// </summary>
        public Vector3 LinearVelocity { get; set; }

        /// <summary>
        /// Gets or sets the sampled angular velocity in radians per second.
        /// </summary>
        public Vector3 AngularVelocity { get; set; }

        /// <summary>
        /// Gets or sets the approximate force inferred from linear velocity delta over the fixed step.
        /// </summary>
        public Vector3 LinearForceApproximation { get; set; }

        /// <summary>
        /// Gets or sets the approximate angular force inferred from angular velocity delta over the fixed step.
        /// </summary>
        public Vector3 AngularForceApproximation { get; set; }
    }
}
