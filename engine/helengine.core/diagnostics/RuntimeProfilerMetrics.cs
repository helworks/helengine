namespace helengine {
    /// <summary>
    /// Owns the mutable profiler counters for the active core and produces immutable snapshots for platform hosts.
    /// </summary>
    public sealed class RuntimeProfilerMetrics {
        /// <summary>
        /// Gets or sets the current core frame number.
        /// </summary>
        long FrameNumberValue { get; set; }

        /// <summary>
        /// Gets or sets the current frame's consumed fixed-update count.
        /// </summary>
        int FixedUpdateCountValue { get; set; }

        /// <summary>
        /// Gets or sets the current frame's committed scene-operation count.
        /// </summary>
        int SceneOperationCountValue { get; set; }

        /// <summary>
        /// Gets or sets the most recently supplied physics metric sample for the current frame.
        /// </summary>
        RuntimePhysicsProfilerMetrics PhysicsMetricsValue { get; set; }

        /// <summary>
        /// Gets or sets whether the native renderer supplied a draw-call count for the current frame.
        /// </summary>
        bool HasDrawCallCountValue { get; set; }

        /// <summary>
        /// Gets or sets the native renderer draw-call count for the current frame.
        /// </summary>
        int DrawCallCountValue { get; set; }

        /// <summary>
        /// Gets or sets whether the native renderer supplied a triangle count for the current frame.
        /// </summary>
        bool HasTriangleCountValue { get; set; }

        /// <summary>
        /// Gets or sets the native renderer triangle count for the current frame.
        /// </summary>
        int TriangleCountValue { get; set; }

        /// <summary>
        /// Begins a new host frame and explicitly clears all counters that must not roll into it.
        /// </summary>
        public void BeginFrame() {
            checked {
                FrameNumberValue++;
            }
            FixedUpdateCountValue = 0;
            SceneOperationCountValue = 0;
            PhysicsMetricsValue = null;
            HasDrawCallCountValue = false;
            DrawCallCountValue = 0;
            HasTriangleCountValue = false;
            TriangleCountValue = 0;
        }

        /// <summary>
        /// Stores the number of fixed updates consumed during the current host frame.
        /// </summary>
        /// <param name="fixedUpdateCount">Consumed fixed-update count.</param>
        public void SetFixedUpdateCount(int fixedUpdateCount) {
            ValidateCount(fixedUpdateCount, nameof(fixedUpdateCount));
            FixedUpdateCountValue = fixedUpdateCount;
        }

        /// <summary>
        /// Adds committed scene operations to the current host-frame count.
        /// </summary>
        /// <param name="sceneOperationCount">Number of operations committed at one safe point.</param>
        public void AddSceneOperationCount(int sceneOperationCount) {
            ValidateCount(sceneOperationCount, nameof(sceneOperationCount));
            checked {
                SceneOperationCountValue += sceneOperationCount;
            }
        }

        /// <summary>
        /// Stores an authoritative physics metric sample for the current host frame.
        /// </summary>
        /// <param name="metrics">Physics runtime metric sample.</param>
        public void SetPhysicsMetrics(RuntimePhysicsProfilerMetrics metrics) {
            PhysicsMetricsValue = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        /// <summary>
        /// Stores explicit native rendering counters for the current host frame.
        /// </summary>
        /// <param name="drawCallCount">Number of native draw calls submitted.</param>
        /// <param name="triangleCount">Number of native triangles submitted.</param>
        public void SetRenderingMetrics(int drawCallCount, int triangleCount) {
            ValidateCount(drawCallCount, nameof(drawCallCount));
            ValidateCount(triangleCount, nameof(triangleCount));
            HasDrawCallCountValue = true;
            DrawCallCountValue = drawCallCount;
            HasTriangleCountValue = true;
            TriangleCountValue = triangleCount;
        }

        /// <summary>
        /// Creates an immutable snapshot of the metrics accumulated for the current host frame.
        /// </summary>
        /// <returns>Current core-owned profiler metrics.</returns>
        public RuntimeProfilerMetricsSnapshot GetSnapshot() {
            bool hasPhysicsMetrics = PhysicsMetricsValue != null;
            return new RuntimeProfilerMetricsSnapshot(
                FrameNumberValue,
                FixedUpdateCountValue,
                SceneOperationCountValue,
                hasPhysicsMetrics && PhysicsMetricsValue.HasBodyCount,
                hasPhysicsMetrics ? PhysicsMetricsValue.BodyCount : 0,
                hasPhysicsMetrics && PhysicsMetricsValue.HasContactCount,
                hasPhysicsMetrics ? PhysicsMetricsValue.ContactCount : 0,
                hasPhysicsMetrics && PhysicsMetricsValue.HasConstraintCount,
                hasPhysicsMetrics ? PhysicsMetricsValue.ConstraintCount : 0,
                HasDrawCallCountValue,
                DrawCallCountValue,
                HasTriangleCountValue,
                TriangleCountValue);
        }

        /// <summary>
        /// Rejects counters that cannot represent a real runtime quantity.
        /// </summary>
        /// <param name="value">Counter value to validate.</param>
        /// <param name="parameterName">Name of the source argument.</param>
        static void ValidateCount(int value, string parameterName) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(parameterName, "Profiler metric counts cannot be negative.");
            }
        }
    }
}
