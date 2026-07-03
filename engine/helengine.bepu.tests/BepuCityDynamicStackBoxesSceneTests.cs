using System.Text;

namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the cooked city stacked-box validation scene topples when loaded through the BEPU-backed runtime registration path.
    /// </summary>
    public sealed class BepuCityDynamicStackBoxesSceneTests {
        /// <summary>
        /// Relative packaged-scene path beneath one cooked city Windows build output.
        /// </summary>
        const string CityCookedStackBoxesSceneRelativePath = @"cooked\scenes\physics\test_scene_dynamic_stack_boxes.hasset";

        /// <summary>
        /// Ensures the authored city stacked-boxes scene no longer remains unrealistically stacked after runtime loading and simulation.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenSimulated_OverhungTowerTopples() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAsset(sceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            List<Entity> stackBoxEntities = FindDynamicUnitBoxEntities(rootEntities);
            Assert.Equal(4, stackBoxEntities.Count);
            SortEntitiesByAscendingX(stackBoxEntities);
            Entity thirdBoxEntity = stackBoxEntities[2];
            Entity fourthBoxEntity = stackBoxEntities[3];
            for (int index = 0; index < 240; index++) {
                world.Step(1.0 / 60.0);
            }

            bool fourthBoxStayedStacked =
                fourthBoxEntity.LocalPosition.Y > 3.3f &&
                Math.Abs(fourthBoxEntity.LocalPosition.X - 1.5f) < 0.2f;
            bool thirdBoxStayedStacked =
                thirdBoxEntity.LocalPosition.Y > 2.3f &&
                Math.Abs(thirdBoxEntity.LocalPosition.X - 1.0f) < 0.2f;

            Assert.False(
                fourthBoxStayedStacked && thirdBoxStayedStacked,
                $"Expected the authored city stacked-boxes scene to topple, but box03 ended at ({thirdBoxEntity.LocalPosition.X}, {thirdBoxEntity.LocalPosition.Y}, {thirdBoxEntity.LocalPosition.Z}) and box04 ended at ({fourthBoxEntity.LocalPosition.X}, {fourthBoxEntity.LocalPosition.Y}, {fourthBoxEntity.LocalPosition.Z}).");
        }

        /// <summary>
        /// Ensures the cooked city stacked-boxes scene also topples when physics advances through the core fixed-step scheduler used by the real host.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenAdvancedThroughCoreUpdate_OverhungTowerTopples() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAsset(cookedSceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            List<Entity> stackBoxEntities = FindDynamicUnitBoxEntities(rootEntities);
            Assert.Equal(4, stackBoxEntities.Count);
            SortEntitiesByAscendingX(stackBoxEntities);
            Entity thirdBoxEntity = stackBoxEntities[2];
            Entity fourthBoxEntity = stackBoxEntities[3];
            for (int index = 0; index < 240; index++) {
                core.Update(1.0d / 60.0d);
            }

            bool fourthBoxStayedStacked =
                fourthBoxEntity.LocalPosition.Y > 3.3f &&
                Math.Abs(fourthBoxEntity.LocalPosition.X - 1.5f) < 0.2f;
            bool thirdBoxStayedStacked =
                thirdBoxEntity.LocalPosition.Y > 2.3f &&
                Math.Abs(thirdBoxEntity.LocalPosition.X - 1.0f) < 0.2f;

            Assert.False(
                fourthBoxStayedStacked && thirdBoxStayedStacked,
                $"Expected the cooked city stacked-boxes scene to topple through the core fixed-step scheduler, but box03 ended at ({thirdBoxEntity.LocalPosition.X}, {thirdBoxEntity.LocalPosition.Y}, {thirdBoxEntity.LocalPosition.Z}) and box04 ended at ({fourthBoxEntity.LocalPosition.X}, {fourthBoxEntity.LocalPosition.Y}, {fourthBoxEntity.LocalPosition.Z}).");
        }

        /// <summary>
        /// Ensures the cooked city stacked-boxes scene leaves its initial stacked silhouette quickly enough to be obvious during live inspection.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenAdvancedThroughCoreUpdate_TopplesVisiblyWithinTwoSeconds() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAsset(cookedSceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            List<Entity> stackBoxEntities = FindDynamicUnitBoxEntities(rootEntities);
            Assert.Equal(4, stackBoxEntities.Count);
            SortEntitiesByAscendingX(stackBoxEntities);
            Entity thirdBoxEntity = stackBoxEntities[2];
            Entity fourthBoxEntity = stackBoxEntities[3];
            for (int index = 0; index < 120; index++) {
                core.Update(1.0d / 60.0d);
            }

            bool visiblyToppled =
                fourthBoxEntity.LocalPosition.X > 1.9f
                || fourthBoxEntity.LocalPosition.Y < 2.9f
                || thirdBoxEntity.LocalPosition.X > 1.3f
                || thirdBoxEntity.LocalPosition.Y < 2.2f;

            Assert.True(
                visiblyToppled,
                $"Expected the cooked city stacked-boxes scene to visibly topple within two seconds, but box03 ended at ({thirdBoxEntity.LocalPosition.X}, {thirdBoxEntity.LocalPosition.Y}, {thirdBoxEntity.LocalPosition.Z}) and box04 ended at ({fourthBoxEntity.LocalPosition.X}, {fourthBoxEntity.LocalPosition.Y}, {fourthBoxEntity.LocalPosition.Z}).");
        }

        /// <summary>
        /// Ensures the second support box does not remain perfectly frozen while the overhung upper boxes start toppling.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenAdvancedThroughCoreUpdate_SecondSupportBoxRespondsToOverhungLoad() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAsset(cookedSceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            List<Entity> stackBoxEntities = FindDynamicUnitBoxEntities(rootEntities);
            Assert.Equal(4, stackBoxEntities.Count);
            SortEntitiesByAscendingX(stackBoxEntities);
            Entity secondBoxEntity = stackBoxEntities[1];
            for (int index = 0; index < 120; index++) {
                core.Update(1.0d / 60.0d);
            }

            bool secondBoxResponded =
                secondBoxEntity.LocalPosition.X > 0.52f
                || Math.Abs(secondBoxEntity.LocalOrientation.Z) > 0.01f
                || Math.Abs(secondBoxEntity.LocalOrientation.X) > 0.01f;

            Assert.True(
                secondBoxResponded,
                $"Expected the second support box to respond to the overhung load, but it stayed at ({secondBoxEntity.LocalPosition.X}, {secondBoxEntity.LocalPosition.Y}, {secondBoxEntity.LocalPosition.Z}) with orientation ({secondBoxEntity.LocalOrientation.X}, {secondBoxEntity.LocalOrientation.Y}, {secondBoxEntity.LocalOrientation.Z}, {secondBoxEntity.LocalOrientation.W}).");
        }

        /// <summary>
        /// Ensures the authored scenario-parent hierarchy does not freeze the second support box under the overhung load.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenAdvancedThroughCoreUpdate_WithScenarioParent_SecondSupportBoxRespondsToOverhungLoad() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAssetPreservingScenarioParent(cookedSceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            List<Entity> stackBoxEntities = FindDynamicUnitBoxEntities(rootEntities);
            Assert.Equal(4, stackBoxEntities.Count);
            SortEntitiesByAscendingX(stackBoxEntities);
            Entity secondBoxEntity = stackBoxEntities[1];
            for (int index = 0; index < 120; index++) {
                core.Update(1.0d / 60.0d);
            }

            bool secondBoxResponded =
                secondBoxEntity.LocalPosition.X > 0.52f
                || Math.Abs(secondBoxEntity.LocalOrientation.Z) > 0.01f
                || Math.Abs(secondBoxEntity.LocalOrientation.X) > 0.01f;

            Assert.True(
                secondBoxResponded,
                $"Expected the scenario-parented second support box to respond to the overhung load, but it stayed at ({secondBoxEntity.LocalPosition.X}, {secondBoxEntity.LocalPosition.Y}, {secondBoxEntity.LocalPosition.Z}) with orientation ({secondBoxEntity.LocalOrientation.X}, {secondBoxEntity.LocalOrientation.Y}, {secondBoxEntity.LocalOrientation.Z}, {secondBoxEntity.LocalOrientation.W}).");
        }

        /// <summary>
        /// Emits a compact headless C# trace for the simplified four-way stack so native-runtime divergence can be compared against managed behavior.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenAdvancedThroughCoreUpdate_HeadlessTraceShowsSecondSupportBoxMoving() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAsset(cookedSceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            List<Entity> stackBoxEntities = FindDynamicUnitBoxEntities(rootEntities);
            Assert.Equal(4, stackBoxEntities.Count);
            SortEntitiesByAscendingX(stackBoxEntities);

            Entity firstBoxEntity = stackBoxEntities[0];
            Entity secondBoxEntity = stackBoxEntities[1];
            Entity thirdBoxEntity = stackBoxEntities[2];
            Entity fourthBoxEntity = stackBoxEntities[3];

            StringBuilder traceBuilder = new StringBuilder();
            const int TraceFrameCount = 200;
            AppendHeadlessTraceLine(traceBuilder, 0, firstBoxEntity, secondBoxEntity, thirdBoxEntity, fourthBoxEntity);
            for (int frameIndex = 1; frameIndex <= TraceFrameCount; frameIndex++) {
                core.Update(1.0d / 60.0d);
                AppendHeadlessTraceLine(traceBuilder, frameIndex, firstBoxEntity, secondBoxEntity, thirdBoxEntity, fourthBoxEntity);
            }

            Console.WriteLine(traceBuilder.ToString());
            Console.WriteLine(world.TryBuildStackBoxesDebugSnapshot());

            bool secondBoxResponded =
                secondBoxEntity.LocalPosition.X > 0.52f
                || Math.Abs(secondBoxEntity.LocalOrientation.Z) > 0.01f
                || Math.Abs(secondBoxEntity.LocalOrientation.X) > 0.01f;

            Assert.True(
                secondBoxResponded,
                $"Expected the headless managed trace to show the second support box moving, but it stayed at ({secondBoxEntity.LocalPosition.X}, {secondBoxEntity.LocalPosition.Y}, {secondBoxEntity.LocalPosition.Z}) with orientation ({secondBoxEntity.LocalOrientation.X}, {secondBoxEntity.LocalOrientation.Y}, {secondBoxEntity.LocalOrientation.Z}, {secondBoxEntity.LocalOrientation.W}).");
        }

        /// <summary>
        /// Ensures the reduced managed stack-box run emits structured differential trace records for the shared harness schema.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenDifferentialTraceCaptured_EmitsStructuredManagedGoldenTrace() {
            string traceText = CaptureManagedDifferentialTrace(4);
            List<BepuDifferentialTraceRecord3D> traceRecords = ParseStructuredTraceRecords(traceText);

            List<BepuDifferentialTraceRecord3D> integrateRecords = traceRecords
                .Where(
                    record => record.Phase == BepuDifferentialTracePhase3D.IntegrateVelocityCallback
                        && record.BodyHandle == 1)
                .ToList();
            List<BepuDifferentialTraceRecord3D> syncRecords = traceRecords
                .Where(
                    record => record.Phase == BepuDifferentialTracePhase3D.SyncSnapshot
                        && record.BodyHandle == 1)
                .ToList();

            Assert.NotEmpty(integrateRecords);
            Assert.NotEmpty(syncRecords);

            BepuDifferentialTraceRecord3D integrateRecord = integrateRecords[integrateRecords.Count - 1];
            BepuDifferentialTraceRecord3D syncRecord = syncRecords[syncRecords.Count - 1];

            Assert.Equal(1, integrateRecord.BodyIndex);
            Assert.Equal(1, syncRecord.BodyIndex);
            Assert.True(syncRecord.Position.Y < 1.5f, $"Expected the second support box to start falling in the managed golden trace, but sync position Y remained {syncRecord.Position.Y}.");
            Assert.True(Math.Abs(syncRecord.LinearVelocity.Y) > 0.01f, $"Expected the second support box to carry nonzero vertical velocity in the managed golden trace, but sync velocity Y remained {syncRecord.LinearVelocity.Y}.");
        }

        /// <summary>
        /// Ensures the cooked city stacked-boxes scene still materializes one playable camera and one directional light through the runtime loader.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenLoadedThroughRuntimeLoader_RegistersCameraAndDirectionalLight() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(cookedSceneAsset);

            Assert.NotEmpty(rootEntities);
            Assert.Single(core.ObjectManager.Cameras);
            Assert.Single(core.ObjectManager.DirectionalLights);
        }

        /// <summary>
        /// Ensures the cooked city stacked-boxes scene preserves the authored key-light orientation instead of collapsing it to the identity quaternion.
        /// </summary>
        [Fact]
        public void LoadCityStackBoxesScene_WhenDeserialized_PreservesAuthoredKeyLightOrientation() {
            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

            SceneEntityAsset keyLightEntity = FindDirectionalLightEntity(cookedSceneAsset.RootEntities);

            Assert.NotNull(keyLightEntity);
            Assert.False(IsIdentityQuaternion(keyLightEntity.LocalOrientation));
        }

        /// <summary>
        /// Resolves the absolute path to the most recent cooked city stacked-boxes scene asset from a Windows build output.
        /// </summary>
        /// <returns>Absolute packaged scene path.</returns>
        static string ResolveCityCookedStackBoxesScenePath() {
            DirectoryInfo currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (currentDirectory != null) {
                string cityProjectRootPath = Path.Combine(currentDirectory.FullName, "helprojs", "city");
                if (Directory.Exists(cityProjectRootPath)) {
                    string[] buildDirectories = Directory.GetDirectories(cityProjectRootPath, "windows-build*", SearchOption.TopDirectoryOnly);
                    Array.Sort(buildDirectories, CompareBuildDirectoriesByDescendingWriteTime);
                    for (int index = 0; index < buildDirectories.Length; index++) {
                        string scenePath = Path.Combine(buildDirectories[index], CityCookedStackBoxesSceneRelativePath);
                        if (File.Exists(scenePath)) {
                            return scenePath;
                        }
                    }
                }

                currentDirectory = currentDirectory.Parent;
            }

            throw new FileNotFoundException("The cooked city stacked-boxes scene asset could not be found from the current test execution hierarchy.");
        }

        /// <summary>
        /// Appends one compact frame snapshot for the simplified four-way stack trace.
        /// </summary>
        /// <param name="traceBuilder">Target text builder receiving the frame snapshot.</param>
        /// <param name="frameIndex">Zero-based fixed-step frame index.</param>
        /// <param name="firstBoxEntity">First stack box on the ground contact.</param>
        /// <param name="secondBoxEntity">Second stack box that diverges in the native build.</param>
        /// <param name="thirdBoxEntity">Third stack box in the overhung tower.</param>
        /// <param name="fourthBoxEntity">Fourth stack box at the top of the tower.</param>
        static void AppendHeadlessTraceLine(StringBuilder traceBuilder, int frameIndex, Entity firstBoxEntity, Entity secondBoxEntity, Entity thirdBoxEntity, Entity fourthBoxEntity) {
            if (traceBuilder == null) {
                throw new ArgumentNullException(nameof(traceBuilder));
            }
            if (firstBoxEntity == null) {
                throw new ArgumentNullException(nameof(firstBoxEntity));
            }
            if (secondBoxEntity == null) {
                throw new ArgumentNullException(nameof(secondBoxEntity));
            }
            if (thirdBoxEntity == null) {
                throw new ArgumentNullException(nameof(thirdBoxEntity));
            }
            if (fourthBoxEntity == null) {
                throw new ArgumentNullException(nameof(fourthBoxEntity));
            }

            traceBuilder.Append("[ManagedStackTrace] frame=");
            traceBuilder.Append(frameIndex);
            traceBuilder.Append(" first=");
            traceBuilder.Append(FormatEntityTrace(firstBoxEntity));
            traceBuilder.Append(" second=");
            traceBuilder.Append(FormatEntityTrace(secondBoxEntity));
            traceBuilder.Append(" third=");
            traceBuilder.Append(FormatEntityTrace(thirdBoxEntity));
            traceBuilder.Append(" fourth=");
            traceBuilder.Append(FormatEntityTrace(fourthBoxEntity));
            traceBuilder.Append('\n');
        }

        /// <summary>
        /// Captures one structured managed differential trace for the reduced four-way stack-box scene over a bounded number of fixed steps.
        /// </summary>
        /// <param name="frameCount">Number of fixed steps to advance before completing the trace.</param>
        /// <returns>Structured trace text drained from the managed BEPU diagnostics path.</returns>
        static string CaptureManagedDifferentialTrace(int frameCount) {
            if (frameCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            string scenePath = ResolveCityCookedStackBoxesScenePath();
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset cookedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyStackBoxesSceneAsset(cookedSceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            StringBuilder traceBuilder = new StringBuilder();
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
                core.Update(1.0d / 60.0d);
                traceBuilder.Append(world.TryBuildStackBoxesDebugSnapshot());
            }

            return traceBuilder.ToString();
        }

        /// <summary>
        /// Captures the first managed sync snapshot in which body handle <c>1</c> has visibly started moving.
        /// </summary>
        /// <param name="frameCount">Number of fixed simulation steps to capture.</param>
        /// <returns>First moving managed sync snapshot for body handle <c>1</c>.</returns>
        internal static BepuDifferentialTraceRecord3D CaptureManagedBodyOneFirstMovingSyncRecord(int frameCount) {
            List<BepuDifferentialTraceRecord3D> traceRecords = ParseStructuredTraceRecords(CaptureManagedDifferentialTrace(frameCount));

            for (int index = 0; index < traceRecords.Count; index++) {
                BepuDifferentialTraceRecord3D traceRecord = traceRecords[index];
                if (traceRecord.Phase != BepuDifferentialTracePhase3D.SyncSnapshot || traceRecord.BodyHandle != 1) {
                    continue;
                }
                if (traceRecord.Position.Y < 1.5f || Math.Abs(traceRecord.LinearVelocity.Y) > 0.01f || Math.Abs(traceRecord.Orientation.Z) > 0.001f) {
                    return traceRecord;
                }
            }

            throw new InvalidOperationException("The managed differential trace did not contain a moving sync snapshot for body handle 1.");
        }

        /// <summary>
        /// Loads the first native sync snapshot recorded for body handle <c>1</c> from the latest differential-harness package output.
        /// </summary>
        /// <returns>First native sync snapshot for body handle <c>1</c>.</returns>
        internal static BepuDifferentialTraceRecord3D LoadNativeBodyOneFirstSyncRecord() {
            string tracePath = ResolveLatestNativeDifferentialTracePath();
            string traceText = File.ReadAllText(tracePath);
            List<BepuDifferentialTraceRecord3D> traceRecords = ParseStructuredTraceRecords(traceText);

            for (int index = 0; index < traceRecords.Count; index++) {
                BepuDifferentialTraceRecord3D traceRecord = traceRecords[index];
                if (traceRecord.Phase == BepuDifferentialTracePhase3D.SyncSnapshot && traceRecord.BodyHandle == 1) {
                    return traceRecord;
                }
            }

            throw new InvalidOperationException($"The native differential trace '{tracePath}' did not contain a sync snapshot for body handle 1.");
        }

        /// <summary>
        /// Loads the first native sync snapshot in which body handle <c>1</c> has visibly started moving from the latest differential-harness package output.
        /// </summary>
        /// <returns>First moving native sync snapshot for body handle <c>1</c>.</returns>
        internal static BepuDifferentialTraceRecord3D LoadNativeBodyOneFirstMovingSyncRecord() {
            string tracePath = ResolveLatestNativeDifferentialTracePath();
            string traceText = File.ReadAllText(tracePath);
            List<BepuDifferentialTraceRecord3D> traceRecords = ParseStructuredTraceRecords(traceText);

            for (int index = 0; index < traceRecords.Count; index++) {
                BepuDifferentialTraceRecord3D traceRecord = traceRecords[index];
                if (traceRecord.Phase != BepuDifferentialTracePhase3D.SyncSnapshot || traceRecord.BodyHandle != 1) {
                    continue;
                }
                if (traceRecord.Position.Y < 1.5f || Math.Abs(traceRecord.LinearVelocity.Y) > 0.01f || Math.Abs(traceRecord.Orientation.Z) > 0.001f) {
                    return traceRecord;
                }
            }

            throw new InvalidOperationException($"The native differential trace '{tracePath}' did not contain a moving sync snapshot for body handle 1.");
        }

        /// <summary>
        /// Parses the shared-schema differential trace lines from one mixed diagnostic text payload.
        /// </summary>
        /// <param name="traceText">Combined managed trace payload to scan.</param>
        /// <returns>Structured differential trace records recovered from the payload.</returns>
        static List<BepuDifferentialTraceRecord3D> ParseStructuredTraceRecords(string traceText) {
            if (string.IsNullOrWhiteSpace(traceText)) {
                throw new ArgumentException("The trace text must contain at least one line.", nameof(traceText));
            }

            List<BepuDifferentialTraceRecord3D> traceRecords = new List<BepuDifferentialTraceRecord3D>();
            using StringReader reader = new StringReader(traceText);
            string traceLine = reader.ReadLine();
            while (traceLine != null) {
                if (traceLine.StartsWith("frame=", StringComparison.Ordinal)) {
                    traceRecords.Add(BepuDifferentialTraceParser.ParseLine(traceLine));
                }

                traceLine = reader.ReadLine();
            }

            return traceRecords;
        }

        /// <summary>
        /// Resolves the latest native differential trace file emitted by the dedicated stack-box harness package.
        /// </summary>
        /// <returns>Absolute path to the latest native differential trace file.</returns>
        static string ResolveLatestNativeDifferentialTracePath() {
            DirectoryInfo currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (currentDirectory != null) {
                string cityProjectRootPath = Path.Combine(currentDirectory.FullName, "helprojs", "city");
                if (Directory.Exists(cityProjectRootPath)) {
                    string[] buildDirectories = Directory.GetDirectories(cityProjectRootPath, "windows-build*stack-boxes*differential-harness*", SearchOption.TopDirectoryOnly);
                    Array.Sort(buildDirectories, CompareBuildDirectoriesByDescendingWriteTime);
                    for (int index = 0; index < buildDirectories.Length; index++) {
                        string tracePath = Path.Combine(buildDirectories[index], "helengine_windows.bepu_differential_trace.log");
                        if (File.Exists(tracePath)) {
                            return tracePath;
                        }
                    }
                }

                currentDirectory = currentDirectory.Parent;
            }

            throw new FileNotFoundException("The native stack-box differential trace file could not be found from the current test execution hierarchy.");
        }

        /// <summary>
        /// Formats one entity position and orientation for the compact managed stack trace.
        /// </summary>
        /// <param name="entity">Entity to format.</param>
        /// <returns>Compact position and orientation text for the supplied entity.</returns>
        static string FormatEntityTrace(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return $"pos=({entity.LocalPosition.X:F6},{entity.LocalPosition.Y:F6},{entity.LocalPosition.Z:F6}) orient=({entity.LocalOrientation.X:F6},{entity.LocalOrientation.Y:F6},{entity.LocalOrientation.Z:F6},{entity.LocalOrientation.W:F6})";
        }

        /// <summary>
        /// Orders build directories by most-recent write time first.
        /// </summary>
        /// <param name="first">First directory path to compare.</param>
        /// <param name="second">Second directory path to compare.</param>
        /// <returns>Descending write-time comparison result.</returns>
        static int CompareBuildDirectoriesByDescendingWriteTime(string first, string second) {
            if (string.IsNullOrWhiteSpace(first)) {
                throw new ArgumentException("First directory path must be provided.", nameof(first));
            }
            if (string.IsNullOrWhiteSpace(second)) {
                throw new ArgumentException("Second directory path must be provided.", nameof(second));
            }

            DateTime firstWriteTime = Directory.GetLastWriteTimeUtc(first);
            DateTime secondWriteTime = Directory.GetLastWriteTimeUtc(second);
            if (secondWriteTime > firstWriteTime) {
                return 1;
            }
            if (secondWriteTime < firstWriteTime) {
                return -1;
            }

            return string.Compare(second, first, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds one physics-only scene asset from the cooked city stack-boxes scene so runtime simulation tests can ignore unrelated gameplay components.
        /// </summary>
        /// <param name="cookedSceneAsset">Cooked city stack-boxes scene asset.</param>
        /// <returns>Scene asset containing only the rigid-body and collider entities required for the stack-boxes scenario.</returns>
        static SceneAsset CreatePhysicsOnlyStackBoxesSceneAsset(SceneAsset cookedSceneAsset) {
            if (cookedSceneAsset == null) {
                throw new ArgumentNullException(nameof(cookedSceneAsset));
            }

            SceneEntityAsset scenarioEntity = FindScenarioEntity(cookedSceneAsset.RootEntities);
            List<SceneEntityAsset> physicsEntities = new List<SceneEntityAsset>();
            for (int index = 0; index < scenarioEntity.Children.Length; index++) {
                SceneEntityAsset childEntity = scenarioEntity.Children[index];
                if (string.Equals(childEntity.Name, "Ground", StringComparison.Ordinal)
                    || childEntity.Name.StartsWith("StackBox", StringComparison.Ordinal)) {
                    physicsEntities.Add(CreatePhysicsOnlyEntity(childEntity));
                }
            }

            return new SceneAsset {
                Id = cookedSceneAsset.Id,
                RootEntities = physicsEntities.ToArray(),
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Builds one physics-only scene asset that preserves the authored scenario parent around the stack-box children.
        /// </summary>
        /// <param name="cookedSceneAsset">Cooked city stack-boxes scene asset.</param>
        /// <returns>Scene asset containing the authored scenario parent and only the rigid-body and collider entities required for the stack-boxes scenario.</returns>
        static SceneAsset CreatePhysicsOnlyStackBoxesSceneAssetPreservingScenarioParent(SceneAsset cookedSceneAsset) {
            if (cookedSceneAsset == null) {
                throw new ArgumentNullException(nameof(cookedSceneAsset));
            }

            SceneEntityAsset scenarioEntity = FindScenarioEntity(cookedSceneAsset.RootEntities);
            List<SceneEntityAsset> physicsChildren = new List<SceneEntityAsset>();
            for (int index = 0; index < scenarioEntity.Children.Length; index++) {
                SceneEntityAsset childEntity = scenarioEntity.Children[index];
                if (string.Equals(childEntity.Name, "Ground", StringComparison.Ordinal)
                    || childEntity.Name.StartsWith("StackBox", StringComparison.Ordinal)) {
                    physicsChildren.Add(CreatePhysicsOnlyEntity(childEntity));
                }
            }

            SceneEntityAsset physicsOnlyScenarioEntity = new SceneEntityAsset {
                Id = scenarioEntity.Id,
                Name = scenarioEntity.Name,
                IsStatic = scenarioEntity.IsStatic,
                LocalPosition = scenarioEntity.LocalPosition,
                LocalScale = scenarioEntity.LocalScale,
                LocalOrientation = scenarioEntity.LocalOrientation,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>(),
                PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                Children = physicsChildren.ToArray()
            };

            return new SceneAsset {
                Id = cookedSceneAsset.Id,
                RootEntities = new[] { physicsOnlyScenarioEntity },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Finds one scene entity by authored name within a serialized hierarchy.
        /// </summary>
        /// <param name="entities">Current hierarchy level to search.</param>
        /// <param name="name">Entity name to locate.</param>
        /// <returns>Matching scene entity when found.</returns>
        static SceneEntityAsset FindDirectionalLightEntity(SceneEntityAsset[] entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            SceneEntityAsset match = FindDirectionalLightEntityOrNull(entities);
            if (match != null) {
                return match;
            }

            throw new InvalidOperationException("A directional-light entity was not found in the cooked city stack-boxes hierarchy. Component inventory: " + string.Join(", ", CollectComponentTypeIds(entities)));
        }

        /// <summary>
        /// Finds one directional-light entity within the supplied hierarchy when present.
        /// </summary>
        /// <param name="entities">Current hierarchy level to search.</param>
        /// <returns>Matching directional-light entity, or null when none exists in the subtree.</returns>
        static SceneEntityAsset FindDirectionalLightEntityOrNull(SceneEntityAsset[] entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                }
                if (ContainsDirectionalLightComponent(entity.Components)) {
                    return entity;
                }

                SceneEntityAsset match = FindDirectionalLightEntityOrNull(entity.Children ?? Array.Empty<SceneEntityAsset>());
                if (match != null) {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets whether one serialized entity component list contains a directional-light record.
        /// </summary>
        /// <param name="components">Serialized component list to inspect.</param>
        /// <returns>True when a directional-light component record exists.</returns>
        static bool ContainsDirectionalLightComponent(SceneComponentAssetRecord[] components) {
            SceneComponentAssetRecord[] effectiveComponents = components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < effectiveComponents.Length; index++) {
                SceneComponentAssetRecord component = effectiveComponents[index];
                if (component == null || string.IsNullOrWhiteSpace(component.ComponentTypeId)) {
                    continue;
                }
                if (component.ComponentTypeId.IndexOf("DirectionalLightComponent", StringComparison.Ordinal) >= 0) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Collects the distinct serialized component type ids present in one hierarchy for diagnostics.
        /// </summary>
        /// <param name="entities">Hierarchy whose component ids should be collected.</param>
        /// <returns>Ordered distinct component ids.</returns>
        static string[] CollectComponentTypeIds(SceneEntityAsset[] entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            HashSet<string> componentTypeIds = new HashSet<string>(StringComparer.Ordinal);
            CollectComponentTypeIdsRecursive(entities, componentTypeIds);
            string[] values = componentTypeIds.ToArray();
            Array.Sort(values, StringComparer.Ordinal);
            return values;
        }

        /// <summary>
        /// Recursively adds serialized component type ids from one hierarchy into the supplied set.
        /// </summary>
        /// <param name="entities">Current hierarchy level to scan.</param>
        /// <param name="componentTypeIds">Distinct component id set receiving discovered values.</param>
        static void CollectComponentTypeIdsRecursive(SceneEntityAsset[] entities, HashSet<string> componentTypeIds) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }
            if (componentTypeIds == null) {
                throw new ArgumentNullException(nameof(componentTypeIds));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                }

                SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                    SceneComponentAssetRecord component = components[componentIndex];
                    if (component != null && !string.IsNullOrWhiteSpace(component.ComponentTypeId)) {
                        componentTypeIds.Add(component.ComponentTypeId);
                    }
                }

                CollectComponentTypeIdsRecursive(entity.Children ?? Array.Empty<SceneEntityAsset>(), componentTypeIds);
            }
        }

        /// <summary>
        /// Gets whether one quaternion matches the identity rotation within a small tolerance.
        /// </summary>
        /// <param name="value">Quaternion to inspect.</param>
        /// <returns>True when the quaternion is effectively identity.</returns>
        static bool IsIdentityQuaternion(float4 value) {
            return Math.Abs(value.X) < 0.0001f
                && Math.Abs(value.Y) < 0.0001f
                && Math.Abs(value.Z) < 0.0001f
                && Math.Abs(value.W - 1.0f) < 0.0001f;
        }

        /// <summary>
        /// Finds the authored scenario root entity from one cooked city scene.
        /// </summary>
        /// <param name="rootEntities">Cooked root entities to inspect.</param>
        /// <returns>Scenario root entity.</returns>
        static SceneEntityAsset FindScenarioEntity(SceneEntityAsset[] rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            for (int index = 0; index < rootEntities.Length; index++) {
                if (string.Equals(rootEntities[index].Name, "Scenario", StringComparison.Ordinal)) {
                    return rootEntities[index];
                }
            }

            throw new InvalidOperationException("The cooked city stack-boxes scene did not contain the expected 'Scenario' root entity.");
        }

        /// <summary>
        /// Clones one cooked entity while retaining only rigid-body and box-collider payload records required for physics simulation.
        /// </summary>
        /// <param name="sourceEntity">Cooked entity to reduce.</param>
        /// <returns>Physics-only entity clone.</returns>
        static SceneEntityAsset CreatePhysicsOnlyEntity(SceneEntityAsset sourceEntity) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }

            List<SceneComponentAssetRecord> keptComponents = new List<SceneComponentAssetRecord>();
            for (int index = 0; index < sourceEntity.Components.Length; index++) {
                SceneComponentAssetRecord component = sourceEntity.Components[index];
                if (string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal)
                    || string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal)) {
                    keptComponents.Add(new SceneComponentAssetRecord {
                        ComponentKey = component.ComponentKey,
                        ComponentIndex = component.ComponentIndex,
                        ComponentTypeId = component.ComponentTypeId,
                        Payload = component.Payload
                    });
                }
            }

            return new SceneEntityAsset {
                Id = sourceEntity.Id,
                Name = sourceEntity.Name,
                IsStatic = sourceEntity.IsStatic,
                LocalPosition = sourceEntity.LocalPosition,
                LocalScale = sourceEntity.LocalScale,
                LocalOrientation = sourceEntity.LocalOrientation,
                Components = keptComponents.ToArray(),
                PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>(),
                PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Collects every dynamic unit-sized box entity from one loaded scene hierarchy.
        /// </summary>
        /// <param name="rootEntities">Hierarchy roots to inspect.</param>
        /// <returns>Dynamic unit-box entities from the authored stack-box scene.</returns>
        static List<Entity> FindDynamicUnitBoxEntities(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            List<Entity> entities = new List<Entity>();
            for (int index = 0; index < rootEntities.Count; index++) {
                CollectDynamicUnitBoxEntities(rootEntities[index], entities);
            }

            return entities;
        }

        /// <summary>
        /// Collects every dynamic unit-sized box entity from one entity subtree.
        /// </summary>
        /// <param name="entity">Entity subtree root to inspect.</param>
        /// <param name="entities">Destination list receiving each matching entity.</param>
        static void CollectDynamicUnitBoxEntities(Entity entity, List<Entity> entities) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            if (IsDynamicUnitBoxEntity(entity)) {
                entities.Add(entity);
            }

            List<Entity> children = entity.Children;
            if (children == null) {
                return;
            }

            for (int index = 0; index < children.Count; index++) {
                CollectDynamicUnitBoxEntities(children[index], entities);
            }
        }

        /// <summary>
        /// Returns whether one entity is a dynamic rigid body with a unit-sized box collider.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when the entity matches the authored stack box signature.</returns>
        static bool IsDynamicUnitBoxEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            RigidBody3DComponent rigidBody = null;
            BoxCollider3DComponent boxCollider = null;
            List<Component> components = entity.Components;
            if (components == null) {
                return false;
            }

            for (int index = 0; index < components.Count; index++) {
                if (components[index] is RigidBody3DComponent currentRigidBody) {
                    rigidBody = currentRigidBody;
                    continue;
                }
                if (components[index] is BoxCollider3DComponent currentBoxCollider) {
                    boxCollider = currentBoxCollider;
                }
            }

            if (rigidBody == null || boxCollider == null) {
                return false;
            }
            if (rigidBody.BodyKind != BodyKind3D.Dynamic) {
                return false;
            }

            return Math.Abs(boxCollider.Size.X - 1f) < 0.001f
                && Math.Abs(boxCollider.Size.Y - 1f) < 0.001f
                && Math.Abs(boxCollider.Size.Z - 1f) < 0.001f;
        }

        /// <summary>
        /// Orders the supplied entities from lowest X position to highest X position.
        /// </summary>
        /// <param name="entities">Entities to reorder in place.</param>
        static void SortEntitiesByAscendingX(List<Entity> entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            for (int writeIndex = 0; writeIndex < entities.Count - 1; writeIndex++) {
                int lowestIndex = writeIndex;
                for (int readIndex = writeIndex + 1; readIndex < entities.Count; readIndex++) {
                    if (entities[readIndex].LocalPosition.X < entities[lowestIndex].LocalPosition.X) {
                        lowestIndex = readIndex;
                    }
                }

                if (lowestIndex == writeIndex) {
                    continue;
                }

                Entity swappedEntity = entities[writeIndex];
                entities[writeIndex] = entities[lowestIndex];
                entities[lowestIndex] = swappedEntity;
            }
        }
    }
}
