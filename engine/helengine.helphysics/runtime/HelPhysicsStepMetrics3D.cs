namespace helengine {
    /// <summary>
    /// Captures immutable counters for exactly the most recently completed deterministic fixed step.
    /// </summary>
    public readonly struct HelPhysicsStepMetrics3D {
        /// <summary>
        /// Stores the number of active world bodies after deferred commands.
        /// </summary>
        public readonly int BodyCount;

        /// <summary>
        /// Stores the number of active awake dynamic bodies after sleep evaluation.
        /// </summary>
        public readonly int AwakeBodyCount;

        /// <summary>
        /// Stores the final active broadphase candidate count.
        /// </summary>
        public readonly int CandidatePairCount;

        /// <summary>
        /// Stores the number of current manifolds sent through active narrowphase and solver preparation.
        /// </summary>
        public readonly int ManifoldCount;

        /// <summary>
        /// Stores the number of current contact points sent through active solver preparation.
        /// </summary>
        public readonly int ContactPointCount;

        /// <summary>
        /// Stores the number of current dynamic islands, including retained sleeping connectivity.
        /// </summary>
        public readonly int IslandCount;

        /// <summary>
        /// Stores the number of current islands whose complete dynamic membership is asleep.
        /// </summary>
        public readonly int SleepingIslandCount;

        /// <summary>
        /// Stores the configured velocity iteration count executed for active contact work, or zero when no contacts were solved.
        /// </summary>
        public readonly int SolverIterationCount;

        /// <summary>
        /// Stores current-step island wake transitions initiated by explicit force.
        /// </summary>
        public readonly int ExplicitForceWakeCount;

        /// <summary>
        /// Stores current-step island wake transitions initiated by explicit impulse.
        /// </summary>
        public readonly int ExplicitImpulseWakeCount;

        /// <summary>
        /// Stores current-step island wake transitions initiated by meaningful new candidate contact.
        /// </summary>
        public readonly int NewCandidateContactWakeCount;

        /// <summary>
        /// Stores current-step island wake transitions initiated by moving kinematic contact.
        /// </summary>
        public readonly int MovingKinematicContactWakeCount;

        /// <summary>
        /// Initializes one complete immutable step sample from non-negative world-owned counters.
        /// </summary>
        /// <param name="bodyCount">Active body count.</param>
        /// <param name="awakeBodyCount">Awake dynamic body count.</param>
        /// <param name="candidatePairCount">Current candidate pair count.</param>
        /// <param name="manifoldCount">Current active manifold count.</param>
        /// <param name="contactPointCount">Current active contact-point count.</param>
        /// <param name="islandCount">Current dynamic island count.</param>
        /// <param name="sleepingIslandCount">Current sleeping island count.</param>
        /// <param name="solverIterationCount">Velocity iterations executed for active contact work.</param>
        /// <param name="explicitForceWakeCount">Explicit-force wake transition count.</param>
        /// <param name="explicitImpulseWakeCount">Explicit-impulse wake transition count.</param>
        /// <param name="newCandidateContactWakeCount">New-candidate wake transition count.</param>
        /// <param name="movingKinematicContactWakeCount">Moving-kinematic wake transition count.</param>
        public HelPhysicsStepMetrics3D(
            int bodyCount,
            int awakeBodyCount,
            int candidatePairCount,
            int manifoldCount,
            int contactPointCount,
            int islandCount,
            int sleepingIslandCount,
            int solverIterationCount,
            int explicitForceWakeCount,
            int explicitImpulseWakeCount,
            int newCandidateContactWakeCount,
            int movingKinematicContactWakeCount) {
            BodyCount = bodyCount;
            AwakeBodyCount = awakeBodyCount;
            CandidatePairCount = candidatePairCount;
            ManifoldCount = manifoldCount;
            ContactPointCount = contactPointCount;
            IslandCount = islandCount;
            SleepingIslandCount = sleepingIslandCount;
            SolverIterationCount = solverIterationCount;
            ExplicitForceWakeCount = explicitForceWakeCount;
            ExplicitImpulseWakeCount = explicitImpulseWakeCount;
            NewCandidateContactWakeCount = newCandidateContactWakeCount;
            MovingKinematicContactWakeCount = movingKinematicContactWakeCount;
        }
    }
}
