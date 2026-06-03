namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the shared reduced-BEPU native-versus-managed differential trace schema before end-to-end harness wiring begins.
    /// </summary>
    public sealed class BepuNativeManagedDifferentialHarnessTests {
        /// <summary>
        /// Ensures one structured differential trace record formats into a stable line-oriented schema with compact vector fields.
        /// </summary>
        [Fact]
        public void WriteLine_WithStructuredRecord_EmitsStableTraceSchema() {
            BepuDifferentialTraceRecord3D record = new BepuDifferentialTraceRecord3D {
                Frame = 12,
                Phase = BepuDifferentialTracePhase3D.IntegrateVelocityCallback,
                BodyHandle = 3,
                BodyIndex = 2,
                BundleIndex = 1,
                ConstraintBatchIndex = 4,
                TypeBatchIndex = 5,
                BodySlotIndex = 0,
                EncodedReferences = "0,-1,-1,-1",
                IntegrationMask = "-1,0,0,0",
                Position = new float3(1.0f, 2.0f, 3.0f),
                Orientation = new float4(0.0f, 0.0f, 0.0f, 1.0f),
                LinearVelocity = new float3(4.0f, 5.0f, 6.0f),
                AngularVelocity = new float3(7.0f, 8.0f, 9.0f)
            };

            string traceLine = BepuDifferentialTraceWriter3D.WriteLine(record);

            Assert.Equal(
                "frame=12 phase=integrate_velocity_callback body_handle=3 body_index=2 bundle_index=1 constraint_batch=4 type_batch=5 body_slot=0 encoded_refs=0,-1,-1,-1 integration_mask=-1,0,0,0 position=(1,2,3) orientation=(0,0,0,1) linear_velocity=(4,5,6) angular_velocity=(7,8,9)",
                traceLine);
        }

        /// <summary>
        /// Ensures one line emitted by the shared schema can be parsed back into a comparable differential trace record.
        /// </summary>
        [Fact]
        public void ParseLine_WithStableTraceSchema_RecoversStructuredRecord() {
            string traceLine = "frame=7 phase=sync_snapshot body_handle=1 body_index=1 position=(0.5,1.5,0) orientation=(0,0,0,1) linear_velocity=(0,-0.1635,0) angular_velocity=(0,0,-0.028)";

            BepuDifferentialTraceRecord3D record = BepuDifferentialTraceParser.ParseLine(traceLine);

            Assert.Equal(7, record.Frame);
            Assert.Equal(BepuDifferentialTracePhase3D.SyncSnapshot, record.Phase);
            Assert.Equal(1, record.BodyHandle);
            Assert.Equal(1, record.BodyIndex);
            Assert.Equal(0.5f, record.Position.X);
            Assert.Equal(1.5f, record.Position.Y);
            Assert.Equal(-0.1635f, record.LinearVelocity.Y);
            Assert.Equal(-0.028f, record.AngularVelocity.Z);
        }

        /// <summary>
        /// Ensures the parser accepts the native-only reduced-BEPU phase tokens that will be emitted by the Windows differential harness.
        /// </summary>
        [Fact]
        public void ParseLine_WithNativePhaseTokens_RecoversStructuredRecord() {
            string[] traceLines = new[] {
                "frame=3 phase=integration_responsibility_assignment body_handle=1 body_index=-1 constraint_batch=1 type_batch=0 body_slot=1 position=(0,0,0) orientation=(0,0,0,1) linear_velocity=(0,0,0) angular_velocity=(0,0,0)",
                "frame=3 phase=gather_and_integrate_before body_handle=1 body_index=1 bundle_index=0 body_slot=1 encoded_refs=0,1,2,3,-1,-1,-1,-1 position=(0.5,1.5,0) orientation=(0,0,0,1) linear_velocity=(0,-0.1635,0) angular_velocity=(0,0,0)",
                "frame=3 phase=gather_and_integrate_after body_handle=1 body_index=1 bundle_index=0 body_slot=1 encoded_refs=0,1,2,3,-1,-1,-1,-1 position=(0.5,1.5,0) orientation=(0,0,0,1) linear_velocity=(0,-0.1635,0) angular_velocity=(0,0,0)",
                "frame=3 phase=two_body_solve_before body_handle=1 body_index=1 bundle_index=0 body_slot=0 encoded_refs=1,-1,-1,-1,-1,-1,-1,-1 position=(0.5,1.5,0) orientation=(0,0,0,1) linear_velocity=(0,-0.112421,0) angular_velocity=(0,0,-0.041377)",
                "frame=3 phase=two_body_solve_after body_handle=1 body_index=1 bundle_index=0 body_slot=0 encoded_refs=1,-1,-1,-1,-1,-1,-1,-1 position=(0.5,1.5,0) orientation=(0,0,0,1) linear_velocity=(0,-0.128253,0) angular_velocity=(0,0,-0.053551)"
            };

            BepuDifferentialTraceRecord3D[] records = traceLines
                .Select(BepuDifferentialTraceParser.ParseLine)
                .ToArray();

            Assert.Equal(BepuDifferentialTracePhase3D.IntegrationResponsibilityAssignment, records[0].Phase);
            Assert.Equal(BepuDifferentialTracePhase3D.GatherAndIntegrateBefore, records[1].Phase);
            Assert.Equal(BepuDifferentialTracePhase3D.GatherAndIntegrateAfter, records[2].Phase);
            Assert.Equal(BepuDifferentialTracePhase3D.TwoBodySolveBefore, records[3].Phase);
            Assert.Equal(BepuDifferentialTracePhase3D.TwoBodySolveAfter, records[4].Phase);
        }

        /// <summary>
        /// Ensures the comparer reports no mismatch when managed and native traces are identical after stable ordering.
        /// </summary>
        [Fact]
        public void FindFirstMismatch_WithEquivalentTraces_ReturnsEmptyString() {
            BepuDifferentialTraceRecord3D[] managedRecords = new[] {
                CreateComparisonRecord(1, BepuDifferentialTracePhase3D.IntegrateVelocityCallback, 1, 1, new float3(0.5f, 1.5f, 0f), new float3(0f, -0.1635f, 0f)),
                CreateComparisonRecord(2, BepuDifferentialTracePhase3D.SyncSnapshot, 1, 1, new float3(0.50014f, 1.49868f, 0f), new float3(0.0084f, -0.0789f, 0f))
            };
            BepuDifferentialTraceRecord3D[] nativeRecords = new[] {
                CreateComparisonRecord(2, BepuDifferentialTracePhase3D.SyncSnapshot, 1, 1, new float3(0.50014f, 1.49868f, 0f), new float3(0.0084f, -0.0789f, 0f)),
                CreateComparisonRecord(1, BepuDifferentialTracePhase3D.IntegrateVelocityCallback, 1, 1, new float3(0.5f, 1.5f, 0f), new float3(0f, -0.1635f, 0f))
            };

            string mismatch = BepuDifferentialTraceComparer.FindFirstMismatch(managedRecords, nativeRecords);

            Assert.Equal(string.Empty, mismatch);
        }

        /// <summary>
        /// Ensures the comparer reports the first field-level mismatch with enough context to diagnose the divergence.
        /// </summary>
        [Fact]
        public void FindFirstMismatch_WithPositionDifference_ReportsFieldLevelMessage() {
            BepuDifferentialTraceRecord3D managedRecord = CreateComparisonRecord(2, BepuDifferentialTracePhase3D.SyncSnapshot, 1, 1, new float3(0.50014f, 1.49868f, 0f), new float3(0.0084f, -0.0789f, 0f));
            BepuDifferentialTraceRecord3D nativeRecord = CreateComparisonRecord(2, BepuDifferentialTracePhase3D.SyncSnapshot, 1, 1, new float3(0.5f, 1.5f, 0f), new float3(0f, 0f, 0f));

            string mismatch = BepuDifferentialTraceComparer.FindFirstMismatch(new[] { managedRecord }, new[] { nativeRecord });

            Assert.Contains("frame=2 phase=SyncSnapshot body_handle=1", mismatch, StringComparison.Ordinal);
            Assert.Contains("field=position.y", mismatch, StringComparison.Ordinal);
            Assert.Contains("managed=", mismatch, StringComparison.Ordinal);
            Assert.Contains("native=", mismatch, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the live native Windows stack-box trace now shows body handle <c>1</c> moving and rotating instead of remaining pinned at its authored spawn pose.
        /// </summary>
        [Fact]
        public void LoadNativeBodyOneFirstMovingSyncRecord_WithLiveNativeStackBoxesTrace_ReturnsMovingBodyOneState() {
            BepuDifferentialTraceRecord3D nativeRecord = BepuCityDynamicStackBoxesSceneTests.LoadNativeBodyOneFirstMovingSyncRecord();
            float linearSpeedMagnitude = (float)Math.Sqrt(
                nativeRecord.LinearVelocity.X * nativeRecord.LinearVelocity.X
                + nativeRecord.LinearVelocity.Y * nativeRecord.LinearVelocity.Y
                + nativeRecord.LinearVelocity.Z * nativeRecord.LinearVelocity.Z);

            Assert.Equal(BepuDifferentialTracePhase3D.SyncSnapshot, nativeRecord.Phase);
            Assert.Equal(1, nativeRecord.BodyHandle);
            Assert.True(nativeRecord.Position.Y < 1.5f, $"Expected native body 1 to fall below its authored spawn height, but Y remained {nativeRecord.Position.Y}.");
            Assert.True(linearSpeedMagnitude > 0.01f, $"Expected native body 1 to carry nontrivial motion, but speed magnitude remained {linearSpeedMagnitude}.");
            Assert.True(Math.Abs(nativeRecord.Orientation.Z) > 0.001f, $"Expected native body 1 to rotate away from identity, but Z remained {nativeRecord.Orientation.Z}.");
        }

        /// <summary>
        /// Creates one compact comparison record with deterministic defaults for unchanged fields.
        /// </summary>
        /// <param name="frame">Trace frame index.</param>
        /// <param name="phase">Trace phase.</param>
        /// <param name="bodyHandle">Dynamic body handle.</param>
        /// <param name="bodyIndex">Active-set body index.</param>
        /// <param name="position">Body position.</param>
        /// <param name="linearVelocity">Body linear velocity.</param>
        /// <returns>Structured comparison record.</returns>
        static BepuDifferentialTraceRecord3D CreateComparisonRecord(int frame, BepuDifferentialTracePhase3D phase, int bodyHandle, int bodyIndex, float3 position, float3 linearVelocity) {
            return new BepuDifferentialTraceRecord3D {
                Frame = frame,
                Phase = phase,
                BodyHandle = bodyHandle,
                BodyIndex = bodyIndex,
                Position = position,
                Orientation = new float4(0f, 0f, 0f, 1f),
                LinearVelocity = linearVelocity,
                AngularVelocity = new float3(0f, 0f, 0f)
            };
        }
    }
}
