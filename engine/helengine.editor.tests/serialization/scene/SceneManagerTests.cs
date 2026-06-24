using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies runtime scene-manager loading, tracking, and unload notifications for built scenes.
    /// </summary>
    public sealed class SceneManagerTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the scene-manager tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated temporary content root.
        /// </summary>
        public SceneManagerTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-manager-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene-manager diagnostics expose safe empty strings before any transition occurs.
        /// </summary>
        [Fact]
        public void Initialize_whenCreated_initializesDiagnosticStringsToEmptyValues() {
            Core core = CreateCore(
                sceneCatalog: null,
                scenePathResolver: new TestSceneIdPathResolver(new Dictionary<string, string>(StringComparer.Ordinal) {
                    { "Scenes/AuthoredMenu.helen", "Scenes/AuthoredMenu.helen" }
                }));

            Assert.NotNull(core.SceneManager);
            Assert.Equal(string.Empty, core.SceneManager.LastTraceStage);
            Assert.Equal(string.Empty, core.SceneManager.LastTraceSceneId);
        }

        /// <summary>
        /// Ensures core bootstrap initializes the runtime scene manager when packaged scene metadata is injected.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogIsProvided_createsSceneManager() {
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")));

            Assert.NotNull(core.SceneManager);
            Assert.NotNull(core.RuntimeDiagnosticsService);
        }

        /// <summary>
        /// Ensures core bootstrap leaves the runtime scene manager unavailable when no packaged scene metadata is injected.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogIsNotProvided_leavesSceneManagerNull() {
            Core core = CreateCore();

            Assert.Null(core.SceneManager);
            Assert.NotNull(core.RuntimeDiagnosticsService);
        }

        /// <summary>
        /// Ensures core bootstrap initializes the runtime scene manager when an editor scene-id resolver is injected without a packaged runtime scene catalog.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogIsNotProvidedButScenePathResolverIsProvided_createsSceneManager() {
            Core core = CreateCore(
                sceneCatalog: null,
                scenePathResolver: new TestSceneIdPathResolver(new Dictionary<string, string>(StringComparer.Ordinal) {
                    { "Scenes/AuthoredMenu.helen", "Scenes/AuthoredMenu.helen" }
                }));

            Assert.NotNull(core.SceneManager);
            Assert.NotNull(core.RuntimeDiagnosticsService);
        }

        /// <summary>
        /// Ensures runtime diagnostics snapshots preserve provider memory counters and overlay loaded scene ids.
        /// </summary>
        [Fact]
        public void RuntimeDiagnosticsService_whenProviderIsSupplied_returnsProviderSnapshotAndLoadedSceneIds() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            RuntimeMemoryDiagnosticsSnapshot snapshot = new RuntimeMemoryDiagnosticsSnapshot {
                ResidentBytes = 123u,
                CommittedBytes = 456u,
                AvailablePhysicalBytes = 789u
            };
            Core core = CreateCore(
                CreateSceneCatalog(new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")),
                new FakeRuntimeDiagnosticsProvider(snapshot));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            RuntimeMemoryDiagnosticsSnapshot capturedSnapshot = core.RuntimeDiagnosticsService.CaptureSnapshot();

            Assert.Equal(123ul, capturedSnapshot.ResidentBytes);
            Assert.Equal(456ul, capturedSnapshot.CommittedBytes);
            Assert.Equal(789ul, capturedSnapshot.AvailablePhysicalBytes);
            Assert.Equal(new[] { "Scenes/Bootstrap.helen" }, capturedSnapshot.TrackedSceneIds);
        }

        /// <summary>
        /// Ensures runtime diagnostics snapshots remain available when no provider or scene manager has been configured.
        /// </summary>
        [Fact]
        public void RuntimeDiagnosticsService_whenProviderIsNotSupplied_returnsEmptyTrackedSceneIds() {
            Core core = CreateCore();

            RuntimeMemoryDiagnosticsSnapshot capturedSnapshot = core.RuntimeDiagnosticsService.CaptureSnapshot();

            Assert.NotNull(capturedSnapshot);
            Assert.Empty(capturedSnapshot.TrackedSceneIds);
        }

        /// <summary>
        /// Ensures authored scene ids can still load through the scene manager when an editor scene-id resolver is available instead of a packaged runtime scene catalog.
        /// </summary>
        [Fact]
        public void LoadScene_whenRuntimeSceneCatalogIsUnavailableButScenePathResolverCanResolveSceneId_loadsTheAuthoredSceneThroughSceneManager() {
            WriteSceneAsset("Scenes/AuthoredMenu.helen", 1u);
            TestSceneIdPathResolver scenePathResolver = new TestSceneIdPathResolver(new Dictionary<string, string>(StringComparer.Ordinal) {
                { "Scenes/AuthoredMenu.helen", "Scenes/AuthoredMenu.helen" }
            });
            Core core = CreateCore(sceneCatalog: null, scenePathResolver: scenePathResolver);

            core.SceneManager.LoadScene("Scenes/AuthoredMenu.helen", SceneLoadMode.Single);
            Assert.Empty(core.SceneManager.LoadedScenes);
            CommitFrame(core);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Assert.Equal("Scenes/AuthoredMenu.helen", loadedScene.SceneId);
            Assert.Equal("Scenes/AuthoredMenu.helen", loadedScene.CookedRelativePath);
            Assert.Equal("Scenes/AuthoredMenu.helen", scenePathResolver.LastResolvedSceneId);
            Assert.Equal(1, scenePathResolver.ResolveCallCount);
        }

        /// <summary>
        /// Ensures single-mode loads track one built scene and dispatch lifecycle events in order.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingle_tracksSceneAndRaisesLifecycleEvents() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")));
            List<string> raisedEvents = new List<string>();
            string loadedSceneId = string.Empty;
            string loadedCookedPath = string.Empty;
            IReadOnlyList<Entity> loadedRootEntities = Array.Empty<Entity>();

            core.SceneManager.SceneLoading += (_, eventArgs) => {
                raisedEvents.Add("loading:" + eventArgs.SceneId);
            };
            core.SceneManager.SceneLoaded += (_, eventArgs) => {
                raisedEvents.Add("loaded:" + eventArgs.SceneId);
                loadedSceneId = eventArgs.SceneId;
                loadedCookedPath = eventArgs.CookedRelativePath;
                loadedRootEntities = eventArgs.RootEntities;
            };

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            Assert.Empty(core.SceneManager.LoadedScenes);
            CommitFrame(core);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Assert.Equal("Scenes/Bootstrap.helen", loadedScene.SceneId);
            Assert.Equal("cooked/scenes/bootstrap.hasset", loadedScene.CookedRelativePath);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Equal(new[] { "loading:Scenes/Bootstrap.helen", "loaded:Scenes/Bootstrap.helen" }, raisedEvents);
            Assert.Equal("Scenes/Bootstrap.helen", loadedSceneId);
            Assert.Equal("cooked/scenes/bootstrap.hasset", loadedCookedPath);
            Assert.Single(loadedRootEntities);
        }

        /// <summary>
        /// Ensures additive loads preserve previously tracked scenes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsAdditive_preservesPreviouslyLoadedScenes() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 1u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            Assert.Empty(core.SceneManager.LoadedScenes);
            CommitFrame(core);
            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Additive);
            Assert.Single(core.SceneManager.LoadedScenes);
            CommitFrame(core);

            Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
        }

        /// <summary>
        /// Ensures single-scene requests stay pending until the frame-boundary draw commit completes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingle_defersActivationUntilAfterDrawCompletes() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

            Assert.Empty(core.SceneManager.LoadedScenes);
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));

            CommitFrame(core);

            Assert.Single(core.SceneManager.LoadedScenes);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
        }

        /// <summary>
        /// Ensures additive scene requests stay pending until the frame-boundary draw commit completes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsAdditive_defersActivationUntilAfterDrawCompletes() {
            WriteSceneAsset("cooked/scenes/bootstrap.hasset", 1u);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Additive);

            Assert.Single(core.SceneManager.LoadedScenes);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));

            CommitFrame(core);

            Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
        }

        /// <summary>
        /// Ensures single-mode scene transitions tear down the previous scene entities before loading the next scene.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingleAfterPreviousSceneWasLoaded_disposesPreviousSceneEntities() {
            WriteSceneAsset(
                "cooked/scenes/Bootstrap.hasset",
                1u,
                CreateCameraComponentRecord(0));
            WriteSceneAsset(
                "cooked/scenes/TestPlayableScene.hasset",
                1u,
                CreateCameraComponentRecord(1));
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            Entity previousRoot = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            CameraComponent previousCamera = Assert.IsType<CameraComponent>(Assert.Single(previousRoot.Components));
            Assert.Single(core.ObjectManager.Cameras);
            Assert.Single(core.ObjectManager.Entities);

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            CommitFrame(core);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Entity loadedRoot = Assert.Single(loadedScene.RootEntities);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedRoot.Components));
            Assert.Equal("Scenes/TestPlayableScene.helen", loadedScene.SceneId);
            Assert.Same(loadedCamera, Assert.Single(core.ObjectManager.Cameras));
            Assert.Same(loadedRoot, Assert.Single(core.ObjectManager.Entities));
            Assert.Throws<InvalidOperationException>(() => previousRoot.Components.Count);
            Assert.Null(previousCamera.Parent);
            Assert.DoesNotContain(previousRoot, core.ObjectManager.Entities);
            Assert.DoesNotContain(previousCamera, core.ObjectManager.Cameras);
        }

        /// <summary>
        /// Ensures single-mode scene transitions release font textures owned by the previous scene before the next scene loads.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingleAfterTextSceneWasLoaded_releasesPreviousSceneFontTextures() {
            WriteFontAsset("fonts/default.hefont", CreateFont());
            WriteSceneAsset(
                "cooked/scenes/Bootstrap.hasset",
                1u,
                CreateTextComponentRecord("fonts/default.hefont"));
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            TestRenderManager2D renderManager2D = new TestRenderManager2D();
            Core core = CreateCore(
                renderManager2D,
                CreateSceneCatalog(
                    new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                    new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            Entity previousRoot = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            TextComponent previousText = Assert.IsType<TextComponent>(
                Assert.Single(previousRoot.Components, component => component is TextComponent));
            FontAsset previousFont = previousText.Font;
            RuntimeTexture previousFontTexture = previousText.Font.Texture;
            TextureAsset previousSourceTexture = previousText.Font.SourceTextureAsset;
            Assert.Empty(renderManager2D.ReleasedTextures);
            Assert.Empty(renderManager2D.ReleasedFonts);
            int flushReleasedTexturesCallCountBeforeReload = renderManager2D.FlushReleasedTexturesCallCount;

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);
            Assert.Empty(renderManager2D.ReleasedTextures);
            Assert.Empty(renderManager2D.ReleasedFonts);
            Assert.Equal(flushReleasedTexturesCallCountBeforeReload, renderManager2D.FlushReleasedTexturesCallCount);
            CommitFrame(core);

            RuntimeTexture releasedTexture = Assert.Single(renderManager2D.ReleasedTextures);
            FontAsset releasedFont = Assert.Single(renderManager2D.ReleasedFonts);
            Assert.Same(previousFontTexture, releasedTexture);
            Assert.Same(previousFont, releasedFont);
            Assert.True(previousFont.IsDisposed);
            Assert.True(previousFontTexture.IsDisposed);
            Assert.NotNull(previousSourceTexture);
            Assert.Null(previousSourceTexture.Colors);
            Assert.Null(previousSourceTexture.PaletteColors);
            Assert.Equal(flushReleasedTexturesCallCountBeforeReload + 2, renderManager2D.FlushReleasedTexturesCallCount);
        }

        /// <summary>
        /// Ensures single-mode scene transitions release models and materials owned by the previous scene before the next scene loads.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingleAfterMeshSceneWasLoaded_releasesPreviousSceneModelAndMaterial() {
            WriteModelAsset("cooked/models/TestModel.hasset");
            WriteMaterialAsset("cooked/materials/TestMaterial.hasset", "ForwardStandardShader");
            WriteShaderAsset("cooked/shaders/ForwardStandardShader.dx11.hasset", "ForwardStandardShader");
            WriteShaderAsset("cooked/shaders/ForwardStandardShader.vulkan.hasset", "ForwardStandardShader");
            WriteSceneAsset(
                "cooked/scenes/Bootstrap.hasset",
                1u,
                CreateMeshComponentRecord("cooked/models/TestModel.hasset", "cooked/materials/TestMaterial.hasset"));
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            TestRenderManager3D renderManager3D = new TestRenderManager3D();
            Core core = CreateCore(renderManager3D, new TestRenderManager2D(), CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            Entity previousRoot = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            MeshComponent previousMesh = Assert.IsType<MeshComponent>(
                Assert.Single(previousRoot.Components, component => component is MeshComponent));
            RuntimeModel previousModel = Assert.IsAssignableFrom<RuntimeModel>(previousMesh.Model);
            ShaderRuntimeMaterial previousMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(previousMesh.Materials));
            Assert.Empty(renderManager3D.ReleasedModels);
            Assert.Empty(renderManager3D.ReleasedMaterials);
            int flushReleasedAssetsCallCountBeforeReload = renderManager3D.FlushReleasedAssetsCallCount;

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);
            Assert.Empty(renderManager3D.ReleasedModels);
            Assert.Empty(renderManager3D.ReleasedMaterials);
            Assert.Equal(flushReleasedAssetsCallCountBeforeReload, renderManager3D.FlushReleasedAssetsCallCount);
            CommitFrame(core);

            RuntimeModel releasedModel = Assert.Single(renderManager3D.ReleasedModels);
            RuntimeMaterial releasedMaterial = Assert.Single(renderManager3D.ReleasedMaterials);
            Assert.Same(previousModel, releasedModel);
            Assert.Same(previousMaterial, releasedMaterial);
            Assert.Null(releasedModel.Submeshes);
            Assert.Null(previousMaterial.Layout);
            Assert.Null(releasedMaterial.RenderState);
            Assert.Null(previousMaterial.Properties);
            Assert.Equal(flushReleasedAssetsCallCountBeforeReload + 1, renderManager3D.FlushReleasedAssetsCallCount);
        }

        /// <summary>
        /// Ensures single-mode scene transitions flush 2D texture releases that are queued while deferred 3D asset releases are being processed.
        /// </summary>
        [Fact]
        public void LoadScene_when3DFlushQueues2DTextureRelease_flushesReleasedTexturesAgainBeforeReloadCompletes() {
            WriteSceneAsset("cooked/scenes/bootstrap.hasset", 1u);
            WriteSceneAsset("cooked/scenes/testplayablescene.hasset", 1u);
            TestRenderManager3D renderManager3D = new TestRenderManager3D();
            TestRenderManager2D renderManager2D = new TestRenderManager2D();
            Core core = CreateCore(
                renderManager3D,
                renderManager2D,
                CreateSceneCatalog(
                    new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                    new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            TestRuntimeTexture deferredTexture = new TestRuntimeTexture {
                Width = 64,
                Height = 64
            };
            renderManager3D.TextureToReleaseDuringFlush = deferredTexture;
            int flushReleasedTexturesCallCountBeforeReload = renderManager2D.FlushReleasedTexturesCallCount;

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);
            CommitFrame(core);

            Assert.Same(deferredTexture, Assert.Single(renderManager2D.ReleasedTextures));
            Assert.Equal(flushReleasedTexturesCallCountBeforeReload + 2, renderManager2D.FlushReleasedTexturesCallCount);
            Assert.Null(renderManager3D.TextureToReleaseDuringFlush);
        }

        /// <summary>
        /// Ensures single-mode scene transitions tear down startup roots that were loaded directly before the runtime scene manager began tracking scenes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingleAndUntrackedStartupRootsExist_disposesTheUntrackedRoots() {
            WriteSceneAsset(
                "cooked/scenes/Bootstrap.hasset",
                1u,
                CreateCameraComponentRecord(0));
            WriteSceneAsset(
                "cooked/scenes/TestPlayableScene.hasset",
                1u,
                CreateCameraComponentRecord(1));
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));
            SceneAsset startupSceneAsset = core.ContentManager.Load<SceneAsset>("cooked/scenes/bootstrap.hasset", RuntimeContentProcessorIds.SceneAsset);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> startupRoots = sceneLoadService.Load(startupSceneAsset);
            Entity previousRoot = Assert.Single(startupRoots);
            CameraComponent previousCamera = Assert.IsType<CameraComponent>(Assert.Single(previousRoot.Components));
            Assert.Single(core.ObjectManager.Cameras);
            Assert.Single(core.ObjectManager.Entities);
            Assert.Empty(core.SceneManager.LoadedScenes);

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);
            Assert.Empty(core.SceneManager.LoadedScenes);
            CommitFrame(core);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Entity loadedRoot = Assert.Single(loadedScene.RootEntities);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedRoot.Components));
            Assert.Equal("Scenes/TestPlayableScene.helen", loadedScene.SceneId);
            Assert.Same(loadedCamera, Assert.Single(core.ObjectManager.Cameras));
            Assert.Same(loadedRoot, Assert.Single(core.ObjectManager.Entities));
            Assert.Throws<InvalidOperationException>(() => previousRoot.Components.Count);
            Assert.Null(previousCamera.Parent);
            Assert.DoesNotContain(previousRoot, core.ObjectManager.Entities);
            Assert.DoesNotContain(previousCamera, core.ObjectManager.Cameras);
        }

        /// <summary>
        /// Ensures scene transitions requested from inside an update component are deferred until the active update loop completes.
        /// </summary>
        [Fact]
        public void LoadScene_whenRequestedDuringUpdate_defersSceneDisposalUntilAfterTheUpdateMethodReturns() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 1u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);

            Entity bootstrapRoot = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            TestSceneLoadTriggerComponent triggerComponent = new TestSceneLoadTriggerComponent {
                TargetSceneId = "Scenes/TestPlayableScene.helen"
            };
            bootstrapRoot.AddComponent(triggerComponent);

            core.Update();

            Assert.True(triggerComponent.HasRequestedLoad);
            Assert.True(triggerComponent.WasStillAttachedAfterRequest);
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));

            core.Draw();

            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
        }

        /// <summary>
        /// Ensures one newly requested scene is committed before the first frame renders so startup cameras are visible immediately.
        /// </summary>
        [Fact]
        public void Draw_whenSceneLoadIsPending_commitsSceneBeforeRenderManagerDrawRuns() {
            WriteSceneAsset(
                "cooked/scenes/bootstrap.hasset",
                1u,
                CreateCameraComponentRecord(0));
            CameraCountRecordingRenderManager3D renderManager3D = new CameraCountRecordingRenderManager3D();
            Core core = CreateCore(
                renderManager3D,
                new TestRenderManager2D(),
                CreateSceneCatalog(new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

            core.Draw();

            Assert.Equal(1, renderManager3D.LastObservedCameraCount);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Single(core.ObjectManager.Cameras);
        }

        /// <summary>
        /// Ensures hosts can defer scene-operation commits until one explicit frame-boundary safe point after draw.
        /// </summary>
        [Fact]
        public void CompleteFrameBoundary_whenDrawTimeCommitIsDisabled_commitsPendingSceneOperationsAtExplicitSafePoint() {
            WriteSceneAsset(
                "cooked/scenes/bootstrap.hasset",
                1u,
                CreateCameraComponentRecord(0));
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath,
                SceneCatalog = CreateSceneCatalog(new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")),
                CommitPendingSceneOperationsDuringDraw = false
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

            core.Draw();

            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Empty(core.ObjectManager.Cameras);

            core.CompleteFrameBoundary();

            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Single(core.ObjectManager.Cameras);
        }

        /// <summary>
        /// Ensures unload notifications expose the tracked root entities and remove scene bookkeeping.
        /// </summary>
        [Fact]
        public void UnloadScene_whenSceneIsTracked_raisesUnloadEventsWithRootEntitiesAndRemovesTheRecord() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/bootstrap.hasset")));
            List<string> raisedEvents = new List<string>();
            IReadOnlyList<Entity> unloadingRootEntities = Array.Empty<Entity>();

            core.SceneManager.SceneUnloading += (_, eventArgs) => {
                raisedEvents.Add("unloading:" + eventArgs.SceneId);
                unloadingRootEntities = eventArgs.RootEntities;
            };
            core.SceneManager.SceneUnloaded += (_, eventArgs) => {
                raisedEvents.Add("unloaded:" + eventArgs.SceneId);
            };

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            CommitFrame(core);
            core.SceneManager.UnloadScene("Scenes/Bootstrap.helen");
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Single(core.SceneManager.LoadedScenes);
            CommitFrame(core);

            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Empty(core.SceneManager.LoadedScenes);
            Assert.Equal(new[] { "unloading:Scenes/Bootstrap.helen", "unloaded:Scenes/Bootstrap.helen" }, raisedEvents);
            Assert.Single(unloadingRootEntities);
        }

        /// <summary>
        /// Ensures single-mode loads preserve previously loaded scenes marked dont-unload.
        /// </summary>
        [Fact]
        public void LoadScene_WhenSingleLoadTargetsAnotherScene_PreservesPreviouslyLoadedDontUnloadScene() {
            WriteSceneAsset("cooked/scenes/Persistent.hasset", 1u, true);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Persistent.helen", "cooked/scenes/persistent.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/testplayablescene.hasset")));

            core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);
            CommitFrame(core);
            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Persistent.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            CommitFrame(core);

            Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Persistent.helen"));
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
        }

        /// <summary>
        /// Ensures explicit unload requests still unload scenes marked dont-unload.
        /// </summary>
        [Fact]
        public void UnloadScene_WhenSceneIsMarkedDontUnload_StillUnloadsWhenExplicitlyRequested() {
            WriteSceneAsset("cooked/scenes/Persistent.hasset", 1u, true);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Persistent.helen", "cooked/scenes/persistent.hasset")));

            core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);
            CommitFrame(core);
            core.SceneManager.UnloadScene("Scenes/Persistent.helen");
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Persistent.helen"));
            CommitFrame(core);

            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Persistent.helen"));
            Assert.Empty(core.SceneManager.LoadedScenes);
        }

        /// <summary>
        /// Ensures reloading an already loaded dont-unload scene still throws an already-loaded error.
        /// </summary>
        [Fact]
        public void LoadScene_WhenPersistentSceneIsAlreadyLoadedAndLoadModeIsSingle_ThrowsAlreadyLoaded() {
            WriteSceneAsset("cooked/scenes/Persistent.hasset", 1u, true);
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Persistent.helen", "cooked/scenes/persistent.hasset")));

            core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);
            CommitFrame(core);

            core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => core.Draw());
            Assert.Contains("already loaded", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes one packaged scene asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged scene path.</param>
        /// <param name="rootEntityId">Stable root entity identifier to persist.</param>
        void WriteSceneAsset(string relativePath, uint rootEntityId, params SceneComponentAssetRecord[] components) {
            WriteSceneAsset(relativePath, rootEntityId, false, components);
        }

        /// <summary>
        /// Writes one packaged scene asset into the temporary content root with the supplied dont-unload setting.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged scene path.</param>
        /// <param name="rootEntityId">Stable root entity identifier to persist.</param>
        /// <param name="dontUnload">True when the packaged scene should survive normal single-scene transitions.</param>
        /// <param name="components">Serialized root components to persist.</param>
        void WriteSceneAsset(string relativePath, uint rootEntityId, bool dontUnload, params SceneComponentAssetRecord[] components) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            SceneAsset sceneAsset = new SceneAsset {
                Id = relativePath,
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile(),
                    DontUnload = dontUnload
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = rootEntityId,
                        Name = "Entity" + rootEntityId.ToString(),
                        Components = components ?? Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Creates one runtime scene catalog for the supplied entries.
        /// </summary>
        /// <param name="entries">Runtime scene entries to expose to the core bootstrap path.</param>
        /// <returns>Runtime scene catalog instance.</returns>
        RuntimeSceneCatalog CreateSceneCatalog(params RuntimeSceneCatalogEntry[] entries) {
            return new RuntimeSceneCatalog(entries);
        }

        /// <summary>
        /// Creates one initialized core rooted at the temporary content path.
        /// </summary>
        /// <param name="sceneCatalog">Optional runtime scene catalog to inject before initialization.</param>
        /// <returns>Initialized core instance for runtime scene-manager tests.</returns>
        Core CreateCore(
            RuntimeSceneCatalog sceneCatalog = null,
            FakeRuntimeDiagnosticsProvider runtimeDiagnosticsProvider = null,
            ISceneIdPathResolver scenePathResolver = null) {
            return CreateCore(new TestRenderManager2D(), sceneCatalog, runtimeDiagnosticsProvider, scenePathResolver);
        }

        /// <summary>
        /// Creates one initialized core rooted at the temporary content path with the supplied 2D render manager.
        /// </summary>
        /// <param name="renderManager2D">2D render manager used by the initialized core.</param>
        /// <param name="sceneCatalog">Optional runtime scene catalog to inject before initialization.</param>
        /// <returns>Initialized core instance for runtime scene-manager tests.</returns>
        Core CreateCore(
            RenderManager2D renderManager2D,
            RuntimeSceneCatalog sceneCatalog = null,
            FakeRuntimeDiagnosticsProvider runtimeDiagnosticsProvider = null,
            ISceneIdPathResolver scenePathResolver = null) {
            return CreateCore(new TestRenderManager3D(), renderManager2D, sceneCatalog, runtimeDiagnosticsProvider, scenePathResolver);
        }

        /// <summary>
        /// Creates one initialized core rooted at the temporary content path with the supplied render managers.
        /// </summary>
        /// <param name="renderManager3D">3D render manager used by the initialized core.</param>
        /// <param name="renderManager2D">2D render manager used by the initialized core.</param>
        /// <param name="sceneCatalog">Optional runtime scene catalog to inject before initialization.</param>
        /// <returns>Initialized core instance for runtime scene-manager tests.</returns>
        Core CreateCore(
            RenderManager3D renderManager3D,
            RenderManager2D renderManager2D,
            RuntimeSceneCatalog sceneCatalog = null,
            FakeRuntimeDiagnosticsProvider runtimeDiagnosticsProvider = null,
            ISceneIdPathResolver scenePathResolver = null) {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath,
                SceneCatalog = sceneCatalog,
                RuntimeDiagnosticsProvider = runtimeDiagnosticsProvider,
                ScenePathResolver = scenePathResolver
            });
            core.Initialize(renderManager3D, renderManager2D, new TestInputBackend(), new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Advances one frame-boundary draw so queued scene operations commit under the shared runtime contract.
        /// </summary>
        /// <param name="core">Initialized core whose pending scene operations should commit.</param>
        void CommitFrame(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }

            core.Draw();
        }

        /// <summary>
        /// Creates one serialized camera component record for packaged scene-manager tests.
        /// </summary>
        /// <param name="drawOrder">Camera draw order to encode in the payload.</param>
        /// <returns>Serialized camera component record.</returns>
        SceneComponentAssetRecord CreateCameraComponentRecord(byte drawOrder) {
            CameraComponent cameraComponent = new CameraComponent {
                CameraDrawOrder = drawOrder,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(0f, 0f, 1f, 1f),
                NearPlaneDistance = 0.1f,
                FarPlaneDistance = 1000f,
                ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 1f), true, 1f, true, 0),
                RenderSettings = new CameraRenderSettings {
                    DepthPrepassMode = DepthPrepassMode.Disabled,
                    ShadowDistance = 40f,
                    PostProcessTier = PostProcessTier.Disabled
                }
            };

            return CreateRuntimeAutomaticComponentRecord(cameraComponent, 0, null);
        }

        /// <summary>
        /// Writes one packaged font asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged font path.</param>
        /// <param name="font">Packaged font asset to persist.</param>
        void WriteFontAsset(string relativePath, FontAsset font) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, font);
        }

        /// <summary>
        /// Creates one packaged font asset used by runtime scene-manager tests.
        /// </summary>
        /// <returns>Packaged font asset with a single white atlas texel.</returns>
        FontAsset CreateFont() {
            TextureAsset sourceTexture = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 },
                PaletteColors = new byte[] { 0, 0, 0, 255 }
            };

            FontAsset font = new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1) {
                SourceTextureAsset = sourceTexture
            };
            return font;
        }

        /// <summary>
        /// Creates one serialized text component record that references the supplied packaged font path.
        /// </summary>
        /// <param name="fontRelativePath">Content-relative packaged font path used by the text component.</param>
        /// <returns>Serialized text component record.</returns>
        SceneComponentAssetRecord CreateTextComponentRecord(string fontRelativePath) {
            if (string.IsNullOrWhiteSpace(fontRelativePath)) {
                throw new ArgumentException("Font path must be provided.", nameof(fontRelativePath));
            }

            TextComponent component = new TextComponent {
                Font = CreateFont(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                FontScale = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = false
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                AutomaticComponentAssetReferenceSupport.BuildReferenceName(nameof(TextComponent.Font)),
                CreateFileReference(fontRelativePath));
            return CreateRuntimeAutomaticComponentRecord(component, 0, saveState);
        }

        /// <summary>
        /// Serializes one automatic component through the packaged runtime payload format used by player scene builds.
        /// </summary>
        /// <param name="component">Component instance to serialize.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <param name="saveState">Editor-time asset-reference state associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        SceneComponentAssetRecord CreateRuntimeAutomaticComponentRecord(
            Component component,
            int componentIndex,
            EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(component.GetType());
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, saveState);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(component.GetType()),
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized mesh component record that references the supplied packaged model and material paths.
        /// </summary>
        /// <param name="modelRelativePath">Content-relative packaged model path used by the mesh component.</param>
        /// <param name="materialRelativePath">Content-relative packaged material path used by the mesh component.</param>
        /// <returns>Serialized mesh component record.</returns>
        SceneComponentAssetRecord CreateMeshComponentRecord(string modelRelativePath, string materialRelativePath) {
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new[] { new TestRuntimeMaterial() },
                RenderOrder3D = 9
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(MeshComponent.Model), CreateFileReference(modelRelativePath));
            saveState.SetAssetReference("Materials[0]", CreateFileReference(materialRelativePath));
            return CreateRuntimeAutomaticComponentRecord(meshComponent, 0, saveState);
        }

        /// <summary>
        /// Creates one packaged file-backed scene asset reference.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged asset path.</param>
        /// <returns>File-backed scene asset reference.</returns>
        SceneAssetReference CreateFileReference(string relativePath) {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.FileSystem,
                relativePath,
                string.Empty,
                string.Empty);
        }

        /// <summary>
        /// Writes one packaged model asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged model path.</param>
        void WriteModelAsset(string relativePath) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ModelAsset modelAsset = new ModelAsset {
                Id = "TestModel",
                Positions = new[] {
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(0f, 1f, 0f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2 },
                BoundsMin = new float3(0f, 0f, 0f),
                BoundsMax = new float3(1f, 1f, 0f),
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "Default",
                        IndexStart = 0,
                        IndexCount = 3
                    }
                }
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, modelAsset);
        }

        /// <summary>
        /// Writes one packaged material asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged material path.</param>
        /// <param name="shaderAssetId">Shader asset identifier referenced by the packaged material.</param>
        void WriteMaterialAsset(string relativePath, string shaderAssetId) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                Id = "TestMaterial",
                ShaderAssetId = shaderAssetId,
                VertexProgram = "VS",
                PixelProgram = "PS",
                Variant = "Default"
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Writes one packaged shader asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged shader path.</param>
        /// <param name="shaderAssetId">Stable shader asset identifier stored in the package.</param>
        void WriteShaderAsset(string relativePath, string shaderAssetId) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = shaderAssetId,
                Name = shaderAssetId,
                TargetName = "directx11",
                Programs = new[] {
                    new ShaderProgramAsset {
                        Name = "VS",
                        Stage = ShaderStage.Vertex,
                        EntryPoint = "VS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = Array.Empty<ShaderVariantAsset>()
                    },
                    new ShaderProgramAsset {
                        Name = "PS",
                        Stage = ShaderStage.Pixel,
                        EntryPoint = "PS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = Array.Empty<ShaderVariantAsset>()
                    }
                },
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, shaderAsset);
        }
    }
}
