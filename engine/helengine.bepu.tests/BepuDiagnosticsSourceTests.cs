namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the optional BEPU diagnostics sources can be stripped through the generic disabled-feature codegen seam.
    /// </summary>
    public sealed class BepuDiagnosticsSourceTests {
        /// <summary>
        /// Ensures the managed BEPU diagnostics bridge can be compiled down to the generic disabled-feature path instead of always carrying the heavy trace implementation.
        /// </summary>
        [Fact]
        public void BepuPhysicsWorld3DDiagnostics_source_uses_generic_disabled_feature_guard() {
            string sourcePath = Path.Combine(
                ResolveRepositoryRootPath(),
                "engine",
                "helengine.bepu",
                "BepuPhysicsWorld3DDiagnostics.cs");

            string source = File.ReadAllText(sourcePath);

            Assert.Contains("#if HELENGINE_CODEGEN_FEATURE_DISABLED_PHYSICS3D_DIAGNOSTICS", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the vendor-side BEPU native-conversion diagnostics source also respects the same generic disabled-feature guard.
        /// </summary>
        [Fact]
    public void BepuNativeConversionDiagnostics_source_uses_generic_disabled_feature_guard() {
            string sourcePath = Path.Combine(
                ResolveRepositoryRootPath(),
                "engine",
                "vendor",
                "bepuphysics2",
                "BepuPhysics",
                "BepuNativeConversionDiagnostics.cs");

            string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_CODEGEN_FEATURE_DISABLED_PHYSICS3D_DIAGNOSTICS", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the runtime narrow phase does not construct per-contact diagnostic strings during normal collision processing.
    /// </summary>
    [Fact]
    public void HelengineBepuNarrowPhaseCallbacks_source_omits_per_contact_diagnostics() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.bepu",
            "HelengineBepuNarrowPhaseCallbacks.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("GetCollidableProperties", source, StringComparison.Ordinal);
        Assert.Contains("CollidableProperties[collidable.BodyHandle]", source, StringComparison.Ordinal);
        Assert.Contains("ref BepuCollidableProperties3D firstProperties", source, StringComparison.Ordinal);
        Assert.Contains("ref BepuCollidableProperties3D bodyProperties = ref CollidableProperties[collidable.BodyHandle]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportSceneTransitionStage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportCollidablePropertyReadStage", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures every production BEPU timestep avoids Core diagnostic stage publication.
    /// </summary>
    [Fact]
    public void BepuPhysicsWorld3D_source_omits_per_timestep_core_diagnostics() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.bepu",
            "BepuPhysicsWorld3D.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("ReportSceneTransitionStage(\"BeforeBepuTimestep\")", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the BEPU narrow phase exposes its native overlap-to-batch stages through the world diagnostic bridge.
    /// </summary>
    [Fact]
    public void BepuNarrowPhase_source_reports_static_pair_batch_entry_stages() {
        string repositoryRootPath = ResolveRepositoryRootPath();
        string narrowPhaseSource = File.ReadAllText(Path.Combine(
            repositoryRootPath,
            "engine",
            "vendor",
            "bepuphysics2",
            "BepuPhysics",
            "CollisionDetection",
            "NarrowPhase.cs"));
        string worldSource = File.ReadAllText(Path.Combine(
            repositoryRootPath,
            "engine",
            "helengine.bepu",
            "BepuPhysicsWorld3D.cs"));

        Assert.Contains("BepuNarrowPhaseHandleOverlapBeforeStaticDirectReference", narrowPhaseSource, StringComparison.Ordinal);
        Assert.Contains("BepuNarrowPhaseAddBatchBeforeDiscreteContinuation", narrowPhaseSource, StringComparison.Ordinal);
        Assert.Contains("BepuNarrowPhaseAddBatchAfterDiscreteBatcherAdd", narrowPhaseSource, StringComparison.Ordinal);
        Assert.Contains("SimulationValue.NarrowPhase.StageReported += OnNarrowPhaseStageReported", worldSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures collision-batcher execution diagnostics expose the transition from an enqueued overlap into task execution and contact-result forwarding.
    /// </summary>
    [Fact]
    public void CollisionBatcher_source_reports_task_execution_and_contact_result_stages() {
        string collisionBatcherSource = File.ReadAllText(Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "vendor",
            "bepuphysics2",
            "BepuPhysics",
            "CollisionDetection",
            "CollisionBatcher.cs"));
        string narrowPhaseSource = File.ReadAllText(Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "vendor",
            "bepuphysics2",
            "BepuPhysics",
            "CollisionDetection",
            "NarrowPhase.cs"));
        string boxBoxTaskSource = File.ReadAllText(Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "vendor",
            "bepuphysics2",
            "BepuPhysics",
            "CollisionDetection",
            "CollisionTasks",
            "BoxBoxCollisionTask.cs"));
        string boxPairTesterSource = File.ReadAllText(Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "vendor",
            "bepuphysics2",
            "BepuPhysics",
            "CollisionDetection",
            "CollisionTasks",
            "BoxPairTester.cs"));

        Assert.Contains("BepuCollisionBatcherBeforeFlush", collisionBatcherSource, StringComparison.Ordinal);
        Assert.Contains("BepuCollisionBatcherBeforeExecuteBatch", collisionBatcherSource, StringComparison.Ordinal);
        Assert.Contains("BepuCollisionBatcherAfterExecuteBatch", collisionBatcherSource, StringComparison.Ordinal);
        Assert.Contains("BepuCollisionBatcherBeforeProcessConvexResult", collisionBatcherSource, StringComparison.Ordinal);
        Assert.Contains("BepuCollisionBatcherAfterDirectPairCompleted", collisionBatcherSource, StringComparison.Ordinal);
        Assert.Contains("narrowPhase.ReportStage", narrowPhaseSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxBoxBeforePairTester", boxBoxTaskSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxBoxAfterPairTester", boxBoxTaskSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxBoxBeforeProcessConvexResult", boxBoxTaskSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxBoxAfterProcessConvexResult", boxBoxTaskSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxPairTesterAfterWorldRotationA", boxPairTesterSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxPairTesterBeforeEdgeTestX", boxPairTesterSource, StringComparison.Ordinal);
        Assert.Contains("AfterSquaredDirection", boxPairTesterSource, StringComparison.Ordinal);
        Assert.Contains("BeforeAxisXSquareRoot", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("AfterAxisXInverseLength", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("Vector<float>.One / length", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("Vector<float>.One / normalDot", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("Vector<float>.One / velocity", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("AfterAxisXExtremes", boxPairTesterSource, StringComparison.Ordinal);
        Assert.Contains("AfterAxisX", boxPairTesterSource, StringComparison.Ordinal);
        Assert.Contains("BepuBoxPairTesterAfterEdgeTestX", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterFaceAxesB", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterFaceNormalDotsA", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterFaceNormalBasisA", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterFaceAxisIdsB", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterFaceNormalCalibrationB", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterCandidateStackAllocation", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterAfterEdgeIds", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("ClipBoxBEdgeAgainstBoxAFace(stageReporter", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BeforeClipEdgeVelocityDivision", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("AfterClipEdgeDistances", boxPairTesterSource, StringComparison.Ordinal);
            Assert.Contains("BepuBoxPairTesterBeforeCandidateReduction", boxPairTesterSource, StringComparison.Ordinal);
        Assert.Contains("batcher.StageReporter", boxBoxTaskSource, StringComparison.Ordinal);
    }

    /// <summary>
        /// Resolves the repository root for source-audit assertions.
        /// </summary>
        /// <returns>Absolute HelEngine repository root path.</returns>
        static string ResolveRepositoryRootPath() {
            string currentDirectory = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(currentDirectory)) {
                if (Directory.Exists(Path.Combine(currentDirectory, "engine"))
                    && Directory.Exists(Path.Combine(currentDirectory, "helengine.ui"))
                    && File.Exists(Path.Combine(currentDirectory, "helengine.ui", "helengine.sln"))) {
                    return currentDirectory;
                }

                currentDirectory = Path.GetDirectoryName(currentDirectory);
            }

            throw new InvalidOperationException("Unable to resolve the HelEngine repository root from the current test directory.");
        }
    }
}
