#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
namespace helengine {
    /// <summary>
    /// Provides the immutable, core-owned profiler counters for one host frame after generated runtime execution.
    /// </summary>
    public sealed class RuntimeProfilerMetricsSnapshot {
        /// <summary>
        /// Initializes one immutable profiler snapshot from the current core-owned counters.
        /// </summary>
        /// <param name="frameNumber">Monotonically increasing host-frame number.</param>
        /// <param name="fixedUpdateCount">Fixed physics updates consumed during the frame.</param>
        /// <param name="sceneOperationCount">Deferred scene operations committed during the frame.</param>
        /// <param name="hasPhysicsBodyCount">Whether the physics body count is available.</param>
        /// <param name="physicsBodyCount">Available physics body count.</param>
        /// <param name="hasPhysicsContactCount">Whether the physics contact count is available.</param>
        /// <param name="physicsContactCount">Available physics contact count.</param>
        /// <param name="hasPhysicsConstraintCount">Whether the physics constraint count is available.</param>
        /// <param name="physicsConstraintCount">Available physics constraint count.</param>
        /// <param name="hasDrawCallCount">Whether the native renderer reported a draw-call count.</param>
        /// <param name="drawCallCount">Available native renderer draw-call count.</param>
        /// <param name="hasTriangleCount">Whether the native renderer reported a triangle count.</param>
        /// <param name="triangleCount">Available native renderer triangle count.</param>
        public RuntimeProfilerMetricsSnapshot(
            long frameNumber,
            int fixedUpdateCount,
            int sceneOperationCount,
            bool hasPhysicsBodyCount,
            int physicsBodyCount,
            bool hasPhysicsContactCount,
            int physicsContactCount,
            bool hasPhysicsConstraintCount,
            int physicsConstraintCount,
            bool hasDrawCallCount,
            int drawCallCount,
            bool hasTriangleCount,
            int triangleCount) {
            ValidateCount(frameNumber, nameof(frameNumber));
            ValidateCount(fixedUpdateCount, nameof(fixedUpdateCount));
            ValidateCount(sceneOperationCount, nameof(sceneOperationCount));
            ValidateAvailableCount(hasPhysicsBodyCount, physicsBodyCount, nameof(physicsBodyCount));
            ValidateAvailableCount(hasPhysicsContactCount, physicsContactCount, nameof(physicsContactCount));
            ValidateAvailableCount(hasPhysicsConstraintCount, physicsConstraintCount, nameof(physicsConstraintCount));
            ValidateAvailableCount(hasDrawCallCount, drawCallCount, nameof(drawCallCount));
            ValidateAvailableCount(hasTriangleCount, triangleCount, nameof(triangleCount));
            FrameNumber = frameNumber;
            FixedUpdateCount = fixedUpdateCount;
            SceneOperationCount = sceneOperationCount;
            HasPhysicsBodyCount = hasPhysicsBodyCount;
            PhysicsBodyCount = physicsBodyCount;
            HasPhysicsContactCount = hasPhysicsContactCount;
            PhysicsContactCount = physicsContactCount;
            HasPhysicsConstraintCount = hasPhysicsConstraintCount;
            PhysicsConstraintCount = physicsConstraintCount;
            HasDrawCallCount = hasDrawCallCount;
            DrawCallCount = drawCallCount;
            HasTriangleCount = hasTriangleCount;
            TriangleCount = triangleCount;
        }

        /// <summary>
        /// Gets the monotonically increasing number assigned when core began the host frame.
        /// </summary>
        public long FrameNumber { get; }

        /// <summary>
        /// Gets the number of fixed updates consumed by core during the host frame.
        /// </summary>
        public int FixedUpdateCount { get; }

        /// <summary>
        /// Gets the number of deferred scene operations committed by core during the host frame.
        /// </summary>
        public int SceneOperationCount { get; }

        /// <summary>
        /// Gets whether <see cref="PhysicsBodyCount"/> is available from the attached physics runtime.
        /// </summary>
        public bool HasPhysicsBodyCount { get; }

        /// <summary>
        /// Gets the active physics-body count when <see cref="HasPhysicsBodyCount"/> is true.
        /// </summary>
        public int PhysicsBodyCount { get; }

        /// <summary>
        /// Gets whether <see cref="PhysicsContactCount"/> is available from the attached physics runtime.
        /// </summary>
        public bool HasPhysicsContactCount { get; }

        /// <summary>
        /// Gets the active physics-contact count when <see cref="HasPhysicsContactCount"/> is true.
        /// </summary>
        public int PhysicsContactCount { get; }

        /// <summary>
        /// Gets whether <see cref="PhysicsConstraintCount"/> is available from the attached physics runtime.
        /// </summary>
        public bool HasPhysicsConstraintCount { get; }

        /// <summary>
        /// Gets the active physics-constraint count when <see cref="HasPhysicsConstraintCount"/> is true.
        /// </summary>
        public int PhysicsConstraintCount { get; }

        /// <summary>
        /// Gets whether <see cref="DrawCallCount"/> was explicitly reported by the current native renderer.
        /// </summary>
        public bool HasDrawCallCount { get; }

        /// <summary>
        /// Gets the native renderer draw-call count when <see cref="HasDrawCallCount"/> is true.
        /// </summary>
        public int DrawCallCount { get; }

        /// <summary>
        /// Gets whether <see cref="TriangleCount"/> was explicitly reported by the current native renderer.
        /// </summary>
        public bool HasTriangleCount { get; }

        /// <summary>
        /// Gets the native renderer triangle count when <see cref="HasTriangleCount"/> is true.
        /// </summary>
        public int TriangleCount { get; }

        /// <summary>
        /// Rejects invalid count values.
        /// </summary>
        /// <param name="value">Count value to validate.</param>
        /// <param name="parameterName">Name of the source argument.</param>
        static void ValidateCount(long value, string parameterName) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(parameterName, "Profiler metric counts cannot be negative.");
            }
        }

        /// <summary>
        /// Rejects negative values only when the corresponding metric is available.
        /// </summary>
        /// <param name="isAvailable">Whether the metric has a runtime-owned value.</param>
        /// <param name="value">Metric value to validate.</param>
        /// <param name="parameterName">Name of the source argument.</param>
        static void ValidateAvailableCount(bool isAvailable, int value, string parameterName) {
            if (isAvailable && value < 0) {
                throw new ArgumentOutOfRangeException(parameterName, "Available profiler metric counts cannot be negative.");
            }
        }
    }
}
#endif
