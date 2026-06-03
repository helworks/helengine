using System.Text;
using BepuPhysics;
using BepuUtilities;

namespace helengine {
    /// <summary>
    /// Emits tightly scoped diagnostics for BEPU world synchronization so native-runtime divergences can be isolated.
    /// </summary>
    public static class BepuPhysicsWorld3DDiagnostics {
        /// <summary>
        /// Maximum number of synchronization frames written for one traced scene binding.
        /// </summary>
        const int MaxLoggedFrames = 120;

        /// <summary>
        /// Maximum number of pose-integrator callback frames written for one traced scene binding.
        /// </summary>
        const int MaxLoggedIntegrateVelocityFrames = 16;

        /// <summary>
        /// Maximum number of managed differential trace lines buffered for one traced scene binding.
        /// </summary>
        const int MaxManagedDifferentialTraceLines = 4096;

        /// <summary>
        /// Tolerance used when matching the authored four-way stack-box layout.
        /// </summary>
        const float PositionTolerance = 0.01f;

        /// <summary>
        /// Gets or sets a value indicating whether the current scene binding should emit synchronization diagnostics.
        /// </summary>
        static bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the zero-based synchronization frame index written to the log.
        /// </summary>
        static int LoggedFrameCount { get; set; }

        /// <summary>
        /// Gets or sets the zero-based velocity-integration callback frame index written to the log.
        /// </summary>
        static int LoggedIntegrateVelocityFrameCount { get; set; }

        /// <summary>
        /// Stores the most recent pose-integrator callback snapshots until the host consumes them.
        /// </summary>
        static string PendingIntegrateVelocitySnapshotText { get; set; } = string.Empty;

        /// <summary>
        /// Stores managed differential trace lines until the host consumes them.
        /// </summary>
        static string PendingManagedDifferentialTraceText { get; set; } = string.Empty;

        /// <summary>
        /// Stores one bounded explanation for why stack-box tracing was disabled for the current scene binding.
        /// </summary>
        static string PendingDisableReasonSnapshotText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of managed differential trace lines already buffered for the current scene binding.
        /// </summary>
        static int LoggedManagedDifferentialTraceLineCount { get; set; }

        /// <summary>
        /// Gets or sets the zero-based fixed-step frame index currently being integrated by the managed BEPU world.
        /// </summary>
        static int CurrentSimulationFrameIndex { get; set; }

        /// <summary>
        /// Resets the trace state for one new scene binding and enables logging only for the authored four-way stack-box scene.
        /// </summary>
        /// <param name="handles">Registered body handles for the newly bound scene.</param>
        public static void Reset(IReadOnlyList<BepuBodyHandle3D> handles) {
            if (handles == null) {
                throw new ArgumentNullException(nameof(handles));
            }

            LoggedFrameCount = 0;
            LoggedIntegrateVelocityFrameCount = 0;
            LoggedManagedDifferentialTraceLineCount = 0;
            CurrentSimulationFrameIndex = -1;
            PendingIntegrateVelocitySnapshotText = string.Empty;
            PendingManagedDifferentialTraceText = string.Empty;
            PendingDisableReasonSnapshotText = DescribeTraceDisableReason(handles);
            IsEnabled = string.IsNullOrEmpty(PendingDisableReasonSnapshotText);
            BepuPhysics.BepuNativeConversionDiagnostics.Reset(IsEnabled);
        }

        /// <summary>
        /// Advances the managed diagnostic frame index at the start of each BEPU fixed step.
        /// </summary>
        public static void BeginPhysicsStep() {
            if (!IsEnabled) {
                return;
            }

            CurrentSimulationFrameIndex++;
            BepuPhysics.BepuNativeConversionDiagnostics.BeginPhysicsStep();
        }

        /// <summary>
        /// Buffers one managed differential trace record when tracing is enabled for the current scene binding.
        /// </summary>
        /// <param name="record">Structured differential trace record to buffer.</param>
        public static void RecordManagedDifferentialTrace(BepuDifferentialTraceRecord3D record) {
            if (!IsEnabled || record == null || LoggedManagedDifferentialTraceLineCount >= MaxManagedDifferentialTraceLines) {
                return;
            }

            PendingManagedDifferentialTraceText += BepuDifferentialTraceWriter3D.WriteLine(record) + "\n";
            LoggedManagedDifferentialTraceLineCount++;
        }

        /// <summary>
        /// Returns all buffered managed differential trace lines and clears the managed-only buffer.
        /// </summary>
        /// <returns>Buffered managed differential trace lines or an empty string when no lines are pending.</returns>
        public static string DrainPendingManagedDifferentialTraceText() {
            string pendingManagedDifferentialTraceText = PendingManagedDifferentialTraceText;
            PendingManagedDifferentialTraceText = string.Empty;
            return pendingManagedDifferentialTraceText;
        }

        /// <summary>
        /// Builds one bounded synchronization snapshot for every registered dynamic body in the traced scene.
        /// </summary>
        /// <param name="handles">Registered body handles aligned to the active simulation.</param>
        /// <param name="simulation">Active simulation to inspect.</param>
        /// <returns>Formatted snapshot text when tracing is enabled; otherwise an empty string.</returns>
        public static string BuildSyncSnapshot(IReadOnlyList<BepuBodyHandle3D> handles, Simulation simulation) {
            if (handles == null) {
                throw new ArgumentNullException(nameof(handles));
            }
            if (simulation == null) {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (!IsEnabled) {
                string disableReasonSnapshotText = PendingDisableReasonSnapshotText;
                PendingDisableReasonSnapshotText = string.Empty;
                if (!string.IsNullOrEmpty(disableReasonSnapshotText)) {
                    return disableReasonSnapshotText;
                }

                return "[BepuTraceDisabled] reason=empty_disable_reason\n";
            }
            if (LoggedFrameCount >= MaxLoggedFrames) {
                return string.Empty;
            }
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(PendingManagedDifferentialTraceText)) {
                builder.Append(PendingManagedDifferentialTraceText);
                PendingManagedDifferentialTraceText = string.Empty;
            }

            if (!string.IsNullOrEmpty(PendingIntegrateVelocitySnapshotText)) {
                builder.Append(PendingIntegrateVelocitySnapshotText);
                PendingIntegrateVelocitySnapshotText = string.Empty;
            }

            string pendingNativeSnapshotText = BepuPhysics.BepuNativeConversionDiagnostics.DrainPendingText();
            if (!string.IsNullOrEmpty(pendingNativeSnapshotText)) {
                builder.Append(pendingNativeSnapshotText);
            }

            int handlesWithBodyHandle = 0;
            int handlesWithoutBodyHandle = 0;
            int handlesWithoutEntity = 0;
            for (int index = 0; index < handles.Count; index++) {
                BepuBodyHandle3D handle = handles[index];
                if (handle == null || handle.Entity == null) {
                    handlesWithoutEntity++;
                    continue;
                }

                if (handle.HasBodyHandle) {
                    handlesWithBodyHandle++;
                }
                else {
                    handlesWithoutBodyHandle++;
                }
            }

            builder.Append($"[BepuSyncMeta] frame={LoggedFrameCount} handleCount={handles.Count} withBodyHandle={handlesWithBodyHandle} withoutBodyHandle={handlesWithoutBodyHandle} withoutEntity={handlesWithoutEntity}\n");
            for (int index = 0; index < handles.Count; index++) {
                BepuBodyHandle3D handle = handles[index];
                if (handle == null || !handle.HasBodyHandle || handle.Entity == null) {
                    continue;
                }

                BodyReference bodyReference = simulation.Bodies[handle.BodyHandle];
                RecordManagedSyncSnapshot(builder, handle, bodyReference);
                builder.Append(
                    $"[BepuSync] frame={LoggedFrameCount} index={index} handle={handle.BodyHandle.Value} " +
                    $"setIndex={bodyReference.MemoryLocation.SetIndex} " +
                    $"bodyIndex={bodyReference.MemoryLocation.Index} " +
                    $"awake={bodyReference.Awake} " +
                    $"kinematic={bodyReference.Kinematic} " +
                    $"inverseMass={bodyReference.LocalInertia.InverseMass} " +
                    $"broadPhaseIndex={bodyReference.Collidable.BroadPhaseIndex} " +
                    $"constraintCount={bodyReference.Constraints.Count} " +
                    $"entityLocal=({handle.Entity.LocalPosition.X},{handle.Entity.LocalPosition.Y},{handle.Entity.LocalPosition.Z}) " +
                    $"pose=({bodyReference.Pose.Position.X},{bodyReference.Pose.Position.Y},{bodyReference.Pose.Position.Z}) " +
                    $"orientation=({bodyReference.Pose.Orientation.X},{bodyReference.Pose.Orientation.Y},{bodyReference.Pose.Orientation.Z},{bodyReference.Pose.Orientation.W}) " +
                    $"linear=({bodyReference.Velocity.Linear.X},{bodyReference.Velocity.Linear.Y},{bodyReference.Velocity.Linear.Z}) " +
                    $"angular=({bodyReference.Velocity.Angular.X},{bodyReference.Velocity.Angular.Y},{bodyReference.Velocity.Angular.Z})\n");
            }

            LoggedFrameCount++;
            return builder.ToString();
        }

        /// <summary>
        /// Records one bounded pose-integrator callback snapshot for the traced four-way stack-box scene.
        /// </summary>
        /// <param name="bodyIndices">Active-set body indices provided to the callback.</param>
        /// <param name="integrationMask">Active lane mask provided to the callback.</param>
        /// <param name="bodies">Simulation body collection used to resolve active-set handles.</param>
        /// <param name="gravityAccelerations">Gravity values aligned to active BEPU body handles.</param>
        /// <param name="dt">Per-lane timestep values supplied to the callback.</param>
        public static void RecordIntegrateVelocity(
            System.Numerics.Vector<int> bodyIndices,
            System.Numerics.Vector<int> integrationMask,
            Bodies bodies,
            CollidableProperty<float> gravityAccelerations,
            System.Numerics.Vector<float> dt,
            Vector3Wide position,
            QuaternionWide orientation,
            BodyVelocityWide velocity,
            System.Numerics.Vector<float> linearYBefore,
            System.Numerics.Vector<float> linearYAfter) {
            if (!IsEnabled || LoggedIntegrateVelocityFrameCount >= MaxLoggedIntegrateVelocityFrames) {
                return;
            }
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }
            if (gravityAccelerations == null) {
                throw new ArgumentNullException(nameof(gravityAccelerations));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("[BepuIntegrateVelocity]");
            builder.Append(" frame=");
            builder.Append(LoggedIntegrateVelocityFrameCount);
            builder.Append(" dt=");
            builder.Append(FormatFloatVector(dt));
            builder.Append(" mask=");
            builder.Append(FormatIntVector(integrationMask));
            builder.Append(" bodyIndices=");
            builder.Append(FormatIntVector(bodyIndices));
            builder.Append(" linearYBefore=");
            builder.Append(FormatFloatVector(linearYBefore));
            builder.Append(" linearYAfter=");
            builder.Append(FormatFloatVector(linearYAfter));
            builder.Append(" handles=<");

            for (int laneIndex = 0; laneIndex < System.Numerics.Vector<int>.Count; laneIndex++) {
                if (laneIndex > 0) {
                    builder.Append(", ");
                }

                int bodyIndex = bodyIndices[laneIndex];
                if (bodyIndex < 0) {
                    builder.Append("inactive");
                    continue;
                }

                BodyHandle bodyHandle = bodies.ActiveSet.IndexToHandle[bodyIndex];
                builder.Append(bodyHandle.Value);
            }

            builder.Append("> gravity=<");
            for (int laneIndex = 0; laneIndex < System.Numerics.Vector<int>.Count; laneIndex++) {
                if (laneIndex > 0) {
                    builder.Append(", ");
                }

                int bodyIndex = bodyIndices[laneIndex];
                if (bodyIndex < 0) {
                    builder.Append("inactive");
                    continue;
                }

                BodyHandle bodyHandle = bodies.ActiveSet.IndexToHandle[bodyIndex];
                builder.Append(gravityAccelerations[bodyHandle].ToString());
            }

            builder.Append(">\n");
            PendingIntegrateVelocitySnapshotText += builder.ToString();

            for (int laneIndex = 0; laneIndex < System.Numerics.Vector<int>.Count; laneIndex++) {
                int bodyIndex = bodyIndices[laneIndex];
                if (bodyIndex < 0) {
                    continue;
                }

                BodyHandle bodyHandle = bodies.ActiveSet.IndexToHandle[bodyIndex];
                RecordManagedDifferentialTrace(
                    new BepuDifferentialTraceRecord3D {
                        Frame = GetStructuredTraceFrameIndex(),
                        Phase = BepuDifferentialTracePhase3D.IntegrateVelocityCallback,
                        BodyHandle = bodyHandle.Value,
                        BodyIndex = bodyIndex,
                        BodySlotIndex = laneIndex,
                        IntegrationMask = FormatCompactIntVector(integrationMask),
                        Position = CreateFloat3(position, laneIndex),
                        Orientation = CreateFloat4(orientation, laneIndex),
                        LinearVelocity = CreateFloat3(velocity.Linear, laneIndex),
                        AngularVelocity = CreateFloat3(velocity.Angular, laneIndex)
                    });
            }

            LoggedIntegrateVelocityFrameCount++;
        }

        /// <summary>
        /// Determines whether the registered scene matches the authored four-way stack-box validation scene.
        /// </summary>
        /// <param name="handles">Registered body handles for the bound scene.</param>
        /// <returns>Empty string when the handle set matches the authored ground-plus-four-stack-box scene; otherwise one bounded disable reason.</returns>
        static string DescribeTraceDisableReason(IReadOnlyList<BepuBodyHandle3D> handles) {
            if (handles.Count != 5) {
                return $"[BepuTraceDisabled] reason=handle_count count={handles.Count}\n";
            }

            bool foundGround = false;
            bool foundFirstBox = false;
            bool foundSecondBox = false;
            bool foundThirdBox = false;
            bool foundFourthBox = false;
            for (int index = 0; index < handles.Count; index++) {
                BepuBodyHandle3D handle = handles[index];
                if (handle == null || handle.Entity == null) {
                    return $"[BepuTraceDisabled] reason=missing_entity index={index}\n";
                }

                float3 localPosition = handle.Entity.LocalPosition;
                if (IsApproximately(localPosition, 0f, -0.5f, 0f)) {
                    foundGround = true;
                    continue;
                }
                if (IsApproximately(localPosition, 0f, 0.5f, 0f)) {
                    foundFirstBox = true;
                    continue;
                }
                if (IsApproximately(localPosition, 0.5f, 1.5f, 0f)) {
                    foundSecondBox = true;
                    continue;
                }
                if (IsApproximately(localPosition, 1f, 2.5f, 0f)) {
                    foundThirdBox = true;
                    continue;
                }
                if (IsApproximately(localPosition, 1.5f, 3.5f, 0f)) {
                    foundFourthBox = true;
                    continue;
                }

                return
                    $"[BepuTraceDisabled] reason=unexpected_position index={index} " +
                    $"position=({localPosition.X},{localPosition.Y},{localPosition.Z})\n";
            }

            if (!foundGround || !foundFirstBox || !foundSecondBox || !foundThirdBox || !foundFourthBox) {
                return
                    $"[BepuTraceDisabled] reason=missing_expected_body " +
                    $"ground={(foundGround ? 1 : 0)} first={(foundFirstBox ? 1 : 0)} second={(foundSecondBox ? 1 : 0)} third={(foundThirdBox ? 1 : 0)} fourth={(foundFourthBox ? 1 : 0)}\n";
            }

            return string.Empty;
        }

        /// <summary>
        /// Determines whether one entity local position matches one expected authored coordinate within a small tolerance.
        /// </summary>
        /// <param name="position">Resolved local position to inspect.</param>
        /// <param name="expectedX">Expected authored X coordinate.</param>
        /// <param name="expectedY">Expected authored Y coordinate.</param>
        /// <param name="expectedZ">Expected authored Z coordinate.</param>
        /// <returns>True when the position matches the expected authored coordinate.</returns>
        static bool IsApproximately(float3 position, float expectedX, float expectedY, float expectedZ) {
            return Math.Abs(position.X - expectedX) <= PositionTolerance
                && Math.Abs(position.Y - expectedY) <= PositionTolerance
                && Math.Abs(position.Z - expectedZ) <= PositionTolerance;
        }

        /// <summary>
        /// Emits one structured sync-snapshot trace record for a registered dynamic or kinematic body.
        /// </summary>
        /// <param name="builder">Snapshot builder receiving the shared-schema line.</param>
        /// <param name="handle">Registered runtime handle aligned to the active scene entity.</param>
        /// <param name="bodyReference">Resolved BEPU body state to serialize.</param>
        static void RecordManagedSyncSnapshot(StringBuilder builder, BepuBodyHandle3D handle, BodyReference bodyReference) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }
            if (handle == null) {
                throw new ArgumentNullException(nameof(handle));
            }

            builder.Append(
                BepuDifferentialTraceWriter3D.WriteLine(
                    new BepuDifferentialTraceRecord3D {
                        Frame = GetStructuredTraceFrameIndex(),
                        Phase = BepuDifferentialTracePhase3D.SyncSnapshot,
                        BodyHandle = handle.BodyHandle.Value,
                        BodyIndex = bodyReference.MemoryLocation.Index,
                        Position = new float3(bodyReference.Pose.Position.X, bodyReference.Pose.Position.Y, bodyReference.Pose.Position.Z),
                        Orientation = new float4(bodyReference.Pose.Orientation.X, bodyReference.Pose.Orientation.Y, bodyReference.Pose.Orientation.Z, bodyReference.Pose.Orientation.W),
                        LinearVelocity = new float3(bodyReference.Velocity.Linear.X, bodyReference.Velocity.Linear.Y, bodyReference.Velocity.Linear.Z),
                        AngularVelocity = new float3(bodyReference.Velocity.Angular.X, bodyReference.Velocity.Angular.Y, bodyReference.Velocity.Angular.Z)
                    }));
            builder.Append('\n');
        }

        /// <summary>
        /// Returns the structured-trace frame index for the current managed physics step.
        /// </summary>
        /// <returns>Zero-based frame index used by shared-schema records.</returns>
        static int GetStructuredTraceFrameIndex() {
            if (CurrentSimulationFrameIndex < 0) {
                return 0;
            }

            return CurrentSimulationFrameIndex;
        }

        /// <summary>
        /// Extracts one scalar XYZ tuple from one BEPU wide vector lane.
        /// </summary>
        /// <param name="value">Wide vector value supplying the lane data.</param>
        /// <param name="laneIndex">Zero-based SIMD lane to extract.</param>
        /// <returns>Scalar float3 reconstructed from the selected lane.</returns>
        static float3 CreateFloat3(Vector3Wide value, int laneIndex) {
            return new float3(value.X[laneIndex], value.Y[laneIndex], value.Z[laneIndex]);
        }

        /// <summary>
        /// Extracts one scalar quaternion from one BEPU wide quaternion lane.
        /// </summary>
        /// <param name="value">Wide quaternion value supplying the lane data.</param>
        /// <param name="laneIndex">Zero-based SIMD lane to extract.</param>
        /// <returns>Scalar float4 reconstructed from the selected lane.</returns>
        static float4 CreateFloat4(QuaternionWide value, int laneIndex) {
            return new float4(value.X[laneIndex], value.Y[laneIndex], value.Z[laneIndex], value.W[laneIndex]);
        }

        /// <summary>
        /// Formats one integer vector into the shared-schema compact comma-delimited representation.
        /// </summary>
        /// <param name="value">Vector value to format.</param>
        /// <returns>Comma-delimited lane list without wrappers.</returns>
        static string FormatCompactIntVector(System.Numerics.Vector<int> value) {
            StringBuilder builder = new StringBuilder();
            for (int laneIndex = 0; laneIndex < System.Numerics.Vector<int>.Count; laneIndex++) {
                if (laneIndex > 0) {
                    builder.Append(',');
                }

                builder.Append(value[laneIndex].ToString());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formats one integer vector for bounded trace output.
        /// </summary>
        /// <param name="value">Vector value to format.</param>
        /// <returns>Compact lane list enclosed in angle brackets.</returns>
        static string FormatIntVector(System.Numerics.Vector<int> value) {
            StringBuilder builder = new StringBuilder();
            builder.Append('<');
            for (int laneIndex = 0; laneIndex < System.Numerics.Vector<int>.Count; laneIndex++) {
                if (laneIndex > 0) {
                    builder.Append(", ");
                }

                builder.Append(value[laneIndex].ToString());
            }

            builder.Append('>');
            return builder.ToString();
        }

        /// <summary>
        /// Formats one floating-point vector for bounded trace output.
        /// </summary>
        /// <param name="value">Vector value to format.</param>
        /// <returns>Compact lane list enclosed in angle brackets.</returns>
        static string FormatFloatVector(System.Numerics.Vector<float> value) {
            StringBuilder builder = new StringBuilder();
            builder.Append('<');
            for (int laneIndex = 0; laneIndex < System.Numerics.Vector<float>.Count; laneIndex++) {
                if (laneIndex > 0) {
                    builder.Append(", ");
                }

                builder.Append(value[laneIndex].ToString());
            }

            builder.Append('>');
            return builder.ToString();
        }
    }
}
