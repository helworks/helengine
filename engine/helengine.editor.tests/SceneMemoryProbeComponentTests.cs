using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies fixed-step scene memory probe execution, scene transitions, looping, and logging.
    /// </summary>
    public sealed class SceneMemoryProbeComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the scene memory probe tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated temporary content root for the current test instance.
        /// </summary>
        public SceneMemoryProbeComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-memory-probe-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Deletes the temporary content root created for the current test instance.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures wait steps do not emit probe checkpoints until the authored duration has fully elapsed.
        /// </summary>
        [Fact]
        public void Update_WhenCurrentStepIsWait_DoesNotAdvanceUntilDurationElapses() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            TestClockDrivenCore core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")));
            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "wait-probe",
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.Wait,
                        DurationSeconds = 5d,
                        Label = "idle"
                    }
                }
            };
            Entity rootEntity = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            List<LogEntry> loggedMessages = new List<LogEntry>();
            Logger.MessageLogged += loggedMessages.Add;

            try {
                rootEntity.AddComponent(component);

                core.Update(0d);
                core.Update(2d);
                Assert.DoesNotContain(loggedMessages, entry => entry.Message.Contains("[SceneMemoryProbe]", StringComparison.Ordinal));

                core.Update(3d);

                LogEntry measurement = Assert.Single(loggedMessages, entry => entry.Message.Contains("[SceneMemoryProbe]", StringComparison.Ordinal));
                Assert.Contains("label=idle", measurement.Message, StringComparison.Ordinal);
            } finally {
                Logger.MessageLogged -= loggedMessages.Add;
            }
        }

        /// <summary>
        /// Ensures single-load probe steps request exactly one single-scene transition and unload the previous scene.
        /// </summary>
        [Fact]
        public void Update_WhenCurrentStepLoadsSingleScene_LoadsRequestedSceneOnce() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            TestClockDrivenCore core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));
            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "single-load",
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.LoadSceneSingle,
                        SceneId = "Scenes/TestPlayableScene.helen",
                        Label = "load-target"
                    }
                }
            };
            int loadedEventCount = 0;
            core.SceneManager.SceneLoaded += (_, eventArgs) => {
                if (string.Equals(eventArgs.SceneId, "Scenes/TestPlayableScene.helen", StringComparison.Ordinal)) {
                    loadedEventCount++;
                }
            };

            Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0].AddComponent(component);

            core.Update(0d);
            core.Update(0d);
            core.Update(0d);

            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Equal(1, loadedEventCount);
        }

        /// <summary>
        /// Ensures additive-load probe steps preserve the previously loaded scene while loading the requested scene.
        /// </summary>
        [Fact]
        public void Update_WhenCurrentStepLoadsAdditiveScene_LoadsRequestedSceneAdditively() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            TestClockDrivenCore core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));
            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "additive-load",
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.LoadSceneAdditive,
                        SceneId = "Scenes/TestPlayableScene.helen",
                        Label = "load-target"
                    }
                }
            };

            Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0].AddComponent(component);

            core.Update(0d);
            core.Update(0d);

            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
        }

        /// <summary>
        /// Ensures unload probe steps explicitly unload the requested scene and leave unrelated scenes intact.
        /// </summary>
        [Fact]
        public void Update_WhenCurrentStepUnloadsScene_UnloadsRequestedSceneOnce() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u, true);
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
            TestClockDrivenCore core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));
            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Additive);
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "unload",
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.UnloadScene,
                        SceneId = "Scenes/TestPlayableScene.helen",
                        Label = "unload-target"
                    }
                }
            };
            int unloadedEventCount = 0;
            core.SceneManager.SceneUnloaded += (_, eventArgs) => {
                if (string.Equals(eventArgs.SceneId, "Scenes/TestPlayableScene.helen", StringComparison.Ordinal)) {
                    unloadedEventCount++;
                }
            };

            Assert.Single(core.SceneManager.LoadedScenes, scene => scene.SceneId == "Scenes/Bootstrap.helen").RootEntities[0].AddComponent(component);

            core.Update(0d);
            core.Update(0d);
            core.Update(0d);

            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            Assert.Equal(1, unloadedEventCount);
        }

        /// <summary>
        /// Ensures looping probes restart from the first step and emit checkpoints for subsequent cycles.
        /// </summary>
        [Fact]
        public void Update_WhenFinalStepCompletesAndLoopIsEnabled_RestartsFromFirstStep() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            TestClockDrivenCore core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")));
            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "looping",
                Loop = true,
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.Wait,
                        DurationSeconds = 0d,
                        Label = "tick"
                    }
                }
            };
            List<LogEntry> loggedMessages = new List<LogEntry>();
            Logger.MessageLogged += loggedMessages.Add;

            try {
                Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0].AddComponent(component);

                core.Update(0d);
                core.Update(0d);
                core.Update(0d);

                IReadOnlyList<LogEntry> probeLogs = loggedMessages.Where(entry => entry.Message.Contains("[SceneMemoryProbe]", StringComparison.Ordinal)).ToArray();
                Assert.Equal(2, probeLogs.Count);
                Assert.Contains("cycle=0", probeLogs[0].Message, StringComparison.Ordinal);
                Assert.Contains("cycle=1", probeLogs[1].Message, StringComparison.Ordinal);
            } finally {
                Logger.MessageLogged -= loggedMessages.Add;
            }
        }

        /// <summary>
        /// Ensures probe checkpoints log one stable compact line and use lightweight scalar counter capture instead of full snapshot capture.
        /// </summary>
        [Fact]
        public void Update_WhenProbeRuns_LogsStableSceneMemoryProbeLines() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
            RuntimeMemoryDiagnosticsSnapshot snapshot = new RuntimeMemoryDiagnosticsSnapshot {
                ResidentBytes = 4096u,
                CommittedBytes = 8192u
            };
            FakeRuntimeDiagnosticsProvider diagnosticsProvider = new FakeRuntimeDiagnosticsProvider(snapshot);
            TestRenderManager3D renderManager3D = new TestRenderManager3D();
            renderManager3D.QueueDrawCallCounts(new[] { 7 });
            TestClockDrivenCore core = CreateCore(
                renderManager3D,
                new TestRenderManager2D(),
                CreateSceneCatalog(new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")),
                diagnosticsProvider);
            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            core.Draw();
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "menu-soak",
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.Wait,
                        DurationSeconds = 1d,
                        Label = "idle-menu"
                    }
                }
            };
            List<LogEntry> loggedMessages = new List<LogEntry>();
            Logger.MessageLogged += loggedMessages.Add;

            try {
                Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0].AddComponent(component);

                core.Update(0d);
                core.Update(1d);

                LogEntry measurement = Assert.Single(loggedMessages, entry => entry.Message.Contains("[SceneMemoryProbe]", StringComparison.Ordinal));
                Assert.Equal(1, diagnosticsProvider.MemoryCounterCaptureCount);
                Assert.Equal(0, diagnosticsProvider.SnapshotCaptureCount);
                Assert.Contains("probe=menu-soak", measurement.Message, StringComparison.Ordinal);
                Assert.Contains("label=idle-menu", measurement.Message, StringComparison.Ordinal);
                Assert.Contains("resident_bytes=4096", measurement.Message, StringComparison.Ordinal);
                Assert.Contains("committed_bytes=8192", measurement.Message, StringComparison.Ordinal);
                Assert.Contains("scenes=Scenes/Bootstrap.helen", measurement.Message, StringComparison.Ordinal);
                Assert.Contains("draw_calls=7", measurement.Message, StringComparison.Ordinal);
            } finally {
                Logger.MessageLogged -= loggedMessages.Add;
            }
        }

        /// <summary>
        /// Writes one packaged scene asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged scene path.</param>
        /// <param name="rootEntityId">Stable root entity identifier to persist.</param>
        /// <param name="components">Serialized root components to persist.</param>
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
        /// Creates one initialized deterministic core rooted at the temporary content path.
        /// </summary>
        /// <param name="sceneCatalog">Optional runtime scene catalog to inject before initialization.</param>
        /// <param name="runtimeDiagnosticsProvider">Optional runtime diagnostics provider used by the core under test.</param>
        /// <returns>Initialized deterministic core instance for scene memory probe tests.</returns>
        TestClockDrivenCore CreateCore(
            RuntimeSceneCatalog sceneCatalog = null,
            FakeRuntimeDiagnosticsProvider runtimeDiagnosticsProvider = null) {
            return CreateCore(new TestRenderManager3D(), new TestRenderManager2D(), sceneCatalog, runtimeDiagnosticsProvider);
        }

        /// <summary>
        /// Creates one initialized deterministic core rooted at the temporary content path with the supplied render managers.
        /// </summary>
        /// <param name="renderManager3D">3D render manager used by the initialized core.</param>
        /// <param name="renderManager2D">2D render manager used by the initialized core.</param>
        /// <param name="sceneCatalog">Optional runtime scene catalog to inject before initialization.</param>
        /// <param name="runtimeDiagnosticsProvider">Optional runtime diagnostics provider used by the core under test.</param>
        /// <returns>Initialized deterministic core instance for scene memory probe tests.</returns>
        TestClockDrivenCore CreateCore(
            RenderManager3D renderManager3D,
            RenderManager2D renderManager2D,
            RuntimeSceneCatalog sceneCatalog = null,
            FakeRuntimeDiagnosticsProvider runtimeDiagnosticsProvider = null) {
            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath),
                SceneCatalog = sceneCatalog,
                RuntimeDiagnosticsProvider = runtimeDiagnosticsProvider
            });
            core.Initialize(renderManager3D, renderManager2D, new TestInputBackend(), new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}

