using helengine.editor.tests.testing;
using helengine.files;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies runtime scene loading emits a timing log for packaged asset materialization.
    /// </summary>
    public class RuntimeSceneLoadServiceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime scene-load test harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the runtime services required by the scene-load tests.
        /// </summary>
        public RuntimeSceneLoadServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-scene-load-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
            Core.Instance.DefaultFontAsset = CreateFont();
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures packaged runtime scene loading writes a start log and a timing log around materialization.
        /// </summary>
        [Fact]
        public void Load_WhenSceneIsMaterialized_WritesStartAndTimingLogs() {
            List<LogEntry> loggedMessages = new List<LogEntry>();
            Action<LogEntry> handleMessageLogged = loggedMessages.Add;

            Logger.MessageLogged += handleMessageLogged;
            try {
                RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                    Core.Instance.ContentManager,
                    TempRootPath,
                    ShaderCompileTarget.DirectX11);
                RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
                SceneAsset sceneAsset = new SceneAsset {
                    RootEntities = new[] {
                        new SceneEntityAsset {
                            Id = "root-entity",
                            Name = "Root"
                        }
                    }
                };

                IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);

                Assert.Single(loadedRoots);
                Assert.Contains(loggedMessages, entry => entry.Level == LogLevel.Info && entry.Message == "Loading packaged scene assets.");
                Assert.Contains(loggedMessages, entry => entry.Level == LogLevel.Info && entry.Message.StartsWith("Loaded packaged scene assets in ", StringComparison.Ordinal));
            } finally {
                Logger.MessageLogged -= handleMessageLogged;
            }
        }

        /// <summary>
        /// Ensures packaged runtime scene loading materializes FPS overlay components on the player side.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsFpsOverlay_MaterializesTheComponent() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            WriteFontAsset("fonts/default.hefont", CreateFont());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.FPSComponent",
                                ComponentIndex = 0,
                                Payload = WriteFpsComponentPayload()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            FPSComponent fpsComponent = Assert.IsType<FPSComponent>(Assert.Single(loadedRoot.Components, component => component is FPSComponent));

            Assert.Equal(0.5d, fpsComponent.RefreshIntervalSeconds);
            Assert.Equal(new int2(8, 6), fpsComponent.Padding);
            Assert.Equal((byte)250, fpsComponent.RenderOrder2D);
            Assert.NotNull(fpsComponent.Font);
            Assert.Equal(16f, fpsComponent.Font.LineHeight);
        }

        /// <summary>
        /// Ensures packaged runtime scene loading materializes mesh components through the registry.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsMeshComponent_MaterializesTheComponent() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(loadedRoot.Components, component => component is MeshComponent));

            Assert.Equal((byte)9, meshComponent.RenderOrder3D);
            Assert.Null(meshComponent.Model);
            Assert.Null(meshComponent.Material);
        }

        /// <summary>
        /// Ensures packaged runtime scene loading materializes camera components through the registry.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsCameraComponent_MaterializesTheComponent() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.CameraComponent",
                                ComponentIndex = 0,
                                Payload = WriteCameraComponentPayload()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(Assert.Single(loadedRoot.Components, component => component is CameraComponent));

            Assert.Equal((byte)17, cameraComponent.CameraDrawOrder);
            Assert.Equal((ushort)EditorLayerMasks.SceneObjects, cameraComponent.LayerMask);
            Assert.Equal(new float4(12f, 24f, 640f, 360f), cameraComponent.Viewport);
            Assert.True(cameraComponent.ClearSettings.ClearColorEnabled);
            Assert.Equal(new float4(0.25f, 0.5f, 0.75f, 1f), cameraComponent.ClearSettings.ClearColor);
        }

        /// <summary>
        /// Ensures unknown component records fail with a clear registry lookup error.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsUnknownComponent_Throws() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.DoesNotExist",
                                ComponentIndex = 0,
                                Payload = Array.Empty<byte>()
                            }
                        }
                    }
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(sceneAsset));
            Assert.Contains("helengine.DoesNotExist", exception.Message);
        }

        /// <summary>
        /// Ensures packaged runtime scene loading resolves text components that were authored against source font files and cooked into packaged `.hefont` outputs.
        /// </summary>
        [Fact]
        public void Load_WhenPackagedSceneUsesSourceFontReference_ResolvesCookedFontAsset() {
            string projectRootPath = Path.Combine(TempRootPath, "source-font-project");
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            string buildRootPath = Path.Combine(TempRootPath, "source-font-build");
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(buildRootPath);

            WriteSourceFont(projectRootPath, "Fonts/DemoDiscTitle.ttf");

            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TextScene.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            SceneAsset authoredSceneAsset = new SceneAsset {
                Id = "Scenes/TextScene.helen",
                AssetReferences = new[] {
                    CreateFileFontReference("Fonts/DemoDiscTitle.ttf")
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.TextComponent",
                                ComponentIndex = 0,
                                Payload = WriteTextComponentPayload(CreateFileFontReference("Fonts/DemoDiscTitle.ttf"))
                            }
                        }
                    }
                }
            };
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TextScene.helen" }, buildRootPath);

            string packagedScenePath = Path.Combine(
                buildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset sceneAsset;
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            TextComponent textComponent = Assert.IsType<TextComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is TextComponent));

            Assert.NotNull(textComponent.Font);
            Assert.Equal("ImportedTestFont", textComponent.Font.FontInfo.Name);
            Assert.True(File.Exists(Path.Combine(buildRootPath, "cooked", "Fonts", "DemoDiscTitle.hefont")));
        }

        /// <summary>
        /// Ensures packaged runtime scene loading materializes baked demo menu metadata and hierarchy through the default runtime registry.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsBakedDemoMenu_MaterializesTheComponent() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-project");
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            string buildRootPath = Path.Combine(TempRootPath, "menu-build");
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(buildRootPath);

            string titleFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscTitle.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(titleFontPath));
            File.WriteAllBytes(titleFontPath, new byte[] { 1, 2, 3, 4 });

            string bodyFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(bodyFontPath));
            File.WriteAllBytes(bodyFontPath, new byte[] { 5, 6, 7, 8 });

            SceneAsset authoredSceneAsset = BuildDemoMenuSceneAsset();
            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen" }, buildRootPath);

            SceneAsset sceneAsset;
            string packagedScenePath = Path.Combine(
                buildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Assert.Equal(2, loadedRoots.Count);
            Entity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is MenuComponent));
            MenuComponent menuHostComponent = Assert.IsType<MenuComponent>(Assert.Single(loadedRoot.Components, component => component is MenuComponent));

            Assert.Equal(typeof(TestMenuDefinitionProvider).AssemblyQualifiedName, menuHostComponent.ProviderTypeName);
            Assert.False(menuHostComponent.IsInitialized);
            Assert.Single(loadedRoot.Children);
            Assert.NotEmpty(loadedRoot.Children[0].Children);
        }

        /// <summary>
        /// Ensures baked menu initialization hides inactive panels so only the selected startup panel remains visible.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsBakedDemoMenu_InitializesOnlyTheInitialPanelAsEnabled() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-panel-visibility-project");
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            string buildRootPath = Path.Combine(TempRootPath, "menu-panel-visibility-build");
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(buildRootPath);

            string titleFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscTitle.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(titleFontPath));
            File.WriteAllBytes(titleFontPath, new byte[] { 1, 2, 3, 4 });

            string bodyFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(bodyFontPath));
            File.WriteAllBytes(bodyFontPath, new byte[] { 5, 6, 7, 8 });

            SceneAsset authoredSceneAsset = BuildDemoMenuSceneAsset();
            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen" }, buildRootPath);

            SceneAsset sceneAsset;
            string packagedScenePath = Path.Combine(
                buildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is MenuComponent));
            MenuComponent menuHostComponent = Assert.IsType<MenuComponent>(Assert.Single(loadedRoot.Components, component => component is MenuComponent));
            menuHostComponent.Update();

            List<Entity> panelEntities = new List<Entity>();
            CollectEntitiesWithComponent<MenuPanelComponent>(loadedRoot, panelEntities);

            Entity mainPanel = Assert.Single(panelEntities, entity => entity.Components.OfType<MenuPanelComponent>().Any(component => component.PanelId == "main"));
            Entity optionsPanel = Assert.Single(panelEntities, entity => entity.Components.OfType<MenuPanelComponent>().Any(component => component.PanelId == "options"));
            MenuPanelComponent activePanelComponent = Assert.IsType<MenuPanelComponent>(Assert.Single(mainPanel.Components, component => component is MenuPanelComponent));

            Assert.True(menuHostComponent.IsInitialized);
            Assert.Equal("main", menuHostComponent.ActivePanelId);
            Assert.Equal(activePanelComponent.PanelId, menuHostComponent.ActivePanelId);
            Assert.True(mainPanel.Enabled);
            Assert.False(optionsPanel.Enabled);
        }

        /// <summary>
        /// Ensures the packaged runtime menu responds to keyboard confirmation in the player-style input path.
        /// </summary>
        [Fact]
        public void Load_WhenMenuReceivesKeyboardConfirm_OpensTheSelectedPanel() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-keyboard-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-keyboard-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Enter));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("options", menuHostComponent.ActivePanelId);
        }

        /// <summary>
        /// Ensures the packaged runtime menu responds to mouse activation over a visible menu row.
        /// </summary>
        [Fact]
        public void Load_WhenMenuItemIsClickedWithTheMouse_OpensTheTargetPanel() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-mouse-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-mouse-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);
            MouseState releasedState = CreateMouseStateInsideMenuItem(menuHostComponent, "open-options", ButtonState.Released);
            MouseState pressedState = CreateMouseStateInsideMenuItem(menuHostComponent, "open-options", ButtonState.Pressed);

            input.SetMouseState(releasedState);
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetMouseState(pressedState);
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetMouseState(releasedState);
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("options", menuHostComponent.ActivePanelId);
        }

        /// <summary>
        /// Ensures runtime menu scene actions route through the runtime scene manager and register the loaded scene.
        /// </summary>
        [Fact]
        public void Load_WhenRuntimeMenuActionLoadsScene_routesThroughSceneManager() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-runtime-scene-manager-project");
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            string buildRootPath = Path.Combine(TempRootPath, "menu-runtime-scene-manager-build");
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(buildRootPath);

            string titleFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscTitle.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(titleFontPath));
            File.WriteAllBytes(titleFontPath, new byte[] { 1, 2, 3, 4 });

            string bodyFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(bodyFontPath));
            File.WriteAllBytes(bodyFontPath, new byte[] { 5, 6, 7, 8 });

            string playableScenePath = Path.Combine(assetsRootPath, "Scenes", "TestPlayableScene.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(playableScenePath));
            using (FileStream playableSceneStream = new FileStream(playableScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                EditorAssetBinarySerializer.Serialize(
                    playableSceneStream,
                    new SceneAsset {
                        Id = "Scenes/TestPlayableScene.helen",
                        RootEntities = new[] {
                            new SceneEntityAsset {
                                Id = "playable-root",
                                Name = "PlayableRoot",
                                Components = Array.Empty<SceneComponentAssetRecord>(),
                                Children = Array.Empty<SceneEntityAsset>()
                            }
                        }
                    });
            }

            SceneAsset authoredSceneAsset = BuildDemoMenuSceneAsset();
            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen", "Scenes/TestPlayableScene.helen" }, buildRootPath);
            WriteRuntimeSceneCatalog(
                buildRootPath,
                new RuntimeSceneCatalogEntry("Scenes/TestMenu.helen", "cooked/scenes/main.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene.hasset"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
            Core.Instance.DefaultFontAsset = CreateFont();

            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Enter));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Enter));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.NotNull(Core.Instance.SceneManager);
            Assert.True(Core.Instance.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
        }

        /// <summary>
        /// Ensures packaged runtime scene loading materializes eligible scripted components through the ordinal automatic fallback path.
        /// </summary>
        [Fact]
        public void Load_WhenPackagedSceneContainsEligibleScriptComponent_MaterializesTheComponent() {
            string projectRootPath = Path.Combine(TempRootPath, "scripted-project");
            string scenePath = Path.Combine(projectRootPath, "assets", "Scenes", "Scripted.helen");
            string buildRootPath = Path.Combine(TempRootPath, "scripted-build");
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets", "Scenes"));
            Directory.CreateDirectory(buildRootPath);

            EditorEntity entity = CreateUserEntity("Scripted");
            entity.AddComponent(new TestScriptSerializableComponent {
                DisplayName = "Packaged Widget",
                Visible = true,
                SortOrder = 14
            });

            SceneSaveService saveService = new SceneSaveService(projectRootPath, new ComponentPersistenceRegistry());
            saveService.Save(scenePath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                CreateFont());
            packager.Package(new[] { "Scenes/Scripted.helen" }, buildRootPath);

            SceneAsset sceneAsset;
            string packagedScenePath = Path.Combine(
                buildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            TestScriptSerializableComponent component = Assert.IsType<TestScriptSerializableComponent>(
                Assert.Single(loadedRoot.Components, loadedComponent => loadedComponent is TestScriptSerializableComponent));

            Assert.Equal("Packaged Widget", component.DisplayName);
            Assert.True(component.Visible);
            Assert.Equal(14, component.SortOrder);
        }

        /// <summary>
        /// Ensures packaged runtime scene loading materializes the authored light component families through the default runtime registry.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsLightComponents_MaterializesAllSupportedLightFamilies() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "directional-light",
                        Name = "DirectionalLight",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.DirectionalLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteDirectionalLightComponentPayload()
                            }
                        }
                    },
                    new SceneEntityAsset {
                        Id = "point-light",
                        Name = "PointLight",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.PointLightComponent",
                                ComponentIndex = 0,
                                Payload = WritePointLightComponentPayload()
                            }
                        }
                    },
                    new SceneEntityAsset {
                        Id = "spot-light",
                        Name = "SpotLight",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.SpotLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteSpotLightComponentPayload()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Assert.Equal(3, loadedRoots.Count);

            DirectionalLightComponent directionalLight = Assert.IsType<DirectionalLightComponent>(Assert.Single(loadedRoots[0].Components, component => component is DirectionalLightComponent));
            PointLightComponent pointLight = Assert.IsType<PointLightComponent>(Assert.Single(loadedRoots[1].Components, component => component is PointLightComponent));
            SpotLightComponent spotLight = Assert.IsType<SpotLightComponent>(Assert.Single(loadedRoots[2].Components, component => component is SpotLightComponent));

            Assert.Equal(new float4(0.3f, 0.4f, 0.5f, 1f), directionalLight.Color);
            Assert.Equal(3.0f, directionalLight.Intensity);
            Assert.True(directionalLight.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Forced, directionalLight.ShadowMapMode);
            Assert.Equal(0.7f, directionalLight.ShadowStrength);
            Assert.Equal(64f, directionalLight.ShadowDistance);

            Assert.Equal(new float4(1f, 0.8f, 0.6f, 1f), pointLight.Color);
            Assert.Equal(4.0f, pointLight.Intensity);
            Assert.True(pointLight.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Auto, pointLight.ShadowMapMode);
            Assert.Equal(0.85f, pointLight.ShadowStrength);
            Assert.Equal(18f, pointLight.Range);

            Assert.Equal(new float4(0.8f, 0.9f, 1f, 1f), spotLight.Color);
            Assert.Equal(2.5f, spotLight.Intensity);
            Assert.False(spotLight.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Disabled, spotLight.ShadowMapMode);
            Assert.Equal(0.45f, spotLight.ShadowStrength);
            Assert.Equal(24f, spotLight.Range);
            Assert.Equal(20f, spotLight.InnerConeAngleDegrees);
            Assert.Equal(36f, spotLight.OuterConeAngleDegrees);
        }

        /// <summary>
        /// Creates a small font asset for the FPS overlay constructor.
        /// </summary>
        /// <returns>Font asset with basic metrics for the test harness.</returns>
        FontAsset CreateFont() {
            TextureAsset sourceTexture = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
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
        /// Writes one serialized FPS component payload.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            WriteFontReference(writer);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(0.5d));
            writer.WriteInt2(new int2(8, 6));
            writer.WriteByte(250);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes a packaged font asset used by the runtime FPS overlay test.
        /// </summary>
        /// <param name="relativePath">Packaged path to write.</param>
        /// <param name="font">Font asset to serialize.</param>
        void WriteFontAsset(string relativePath, FontAsset font) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, font);
        }

        /// <summary>
        /// Writes the runtime font reference used by the FPS overlay test.
        /// </summary>
        /// <param name="writer">Writer receiving the reference payload.</param>
        void WriteFontReference(EngineBinaryWriter writer) {
            writer.WriteByte(1);
            writer.WriteInt32((int)SceneAssetReferenceSourceKind.FileSystem);
            writer.WriteString("fonts/default.hefont");
            writer.WriteString(string.Empty);
            writer.WriteString(string.Empty);
        }

        /// <summary>
        /// Creates one file-backed font reference for authored or packaged scene payloads.
        /// </summary>
        /// <param name="relativePath">Relative font path to encode.</param>
        /// <returns>File-backed scene reference.</returns>
        SceneAssetReference CreateFileFontReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Writes one raw source font file into a test project assets folder.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the `assets` folder.</param>
        /// <param name="relativePath">Project-relative source font path.</param>
        void WriteSourceFont(string projectRootPath, string relativePath) {
            string fullPath = Path.Combine(projectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, new byte[] { 1, 2, 3, 4 });
        }

        /// <summary>
        /// Writes one serialized text component payload using the supplied font asset reference.
        /// </summary>
        /// <param name="fontReference">Font reference to persist for the text component.</param>
        /// <returns>Serialized text component payload.</returns>
        byte[] WriteTextComponentPayload(SceneAssetReference fontReference) {
            TextComponentPersistenceDescriptor descriptor = new TextComponentPersistenceDescriptor();
            TextComponent textComponent = new TextComponent {
                Font = CreateFont(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(textComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized mesh component payload with no external asset references.
        /// </summary>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteByte(9);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized camera component payload.
        /// </summary>
        /// <returns>Serialized camera component payload.</returns>
        byte[] WriteCameraComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(17);
            writer.WriteUInt16(EditorLayerMasks.SceneObjects);
            writer.WriteSingle(12f);
            writer.WriteSingle(24f);
            writer.WriteSingle(640f);
            writer.WriteSingle(360f);
            writer.WriteByte(1);
            writer.WriteSingle(0.25f);
            writer.WriteSingle(0.5f);
            writer.WriteSingle(0.75f);
            writer.WriteSingle(1f);
            writer.WriteByte(1);
            writer.WriteSingle(0.42f);
            writer.WriteByte(1);
            writer.WriteByte(9);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds one baked demo menu scene asset for runtime materialization tests.
        /// </summary>
        /// <returns>Baked demo menu scene asset.</returns>
        SceneAsset BuildDemoMenuSceneAsset() {
            DemoMenuSceneAssetFactory factory = new DemoMenuSceneAssetFactory();
            return factory.BuildSceneAsset(
                "Scenes/TestMenu.helen",
                typeof(TestMenuDefinitionProvider).AssemblyQualifiedName,
                new MenuDefinition(
                    "Demo",
                    "Runtime",
                    "main",
                    "Fonts/DemoDiscTitle.ttf",
                    "Fonts/DemoDiscBody.ttf",
                    new byte4(10, 10, 20, 255),
                    new byte4(30, 30, 50, 255),
                    new byte4(60, 60, 90, 255),
                    new byte4(120, 120, 255, 255),
                    new byte4(80, 180, 200, 255),
                    new byte4(255, 255, 255, 255),
                    new byte4(210, 210, 220, 255),
                    new[] {
                    new MenuPanelDefinition(
                            "main",
                            "Main Menu",
                            "Runtime test panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("open-options", "Options", "Opens the options panel.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "options")),
                                new MenuItemDefinition("back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            }),
                        new MenuPanelDefinition(
                            "options",
                            "Options",
                            "Secondary runtime test panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("load-scene", "Select Scene", "Loads a scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "Scenes/TestPlayableScene.helen")),
                                new MenuItemDefinition("options-back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            })
                    }));
        }

        /// <summary>
        /// Packages the baked demo menu scene into one temporary build root.
        /// </summary>
        /// <param name="projectRootPath">Temporary project root that should receive authored content.</param>
        /// <param name="buildFolderName">Unique build folder name under the temporary root.</param>
        /// <returns>Packaged build output root.</returns>
        string PackageDemoMenuScene(string projectRootPath, string buildFolderName) {
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            string buildRootPath = Path.Combine(TempRootPath, buildFolderName);
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(buildRootPath);

            string titleFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscTitle.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(titleFontPath));
            File.WriteAllBytes(titleFontPath, new byte[] { 1, 2, 3, 4 });

            string bodyFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(bodyFontPath));
            File.WriteAllBytes(bodyFontPath, new byte[] { 5, 6, 7, 8 });

            SceneAsset authoredSceneAsset = BuildDemoMenuSceneAsset();
            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen" }, buildRootPath);
            return buildRootPath;
        }

        /// <summary>
        /// Writes one runtime scene catalog file into the supplied packaged build output root.
        /// </summary>
        /// <param name="buildRootPath">Packaged build output root that should receive the runtime catalog.</param>
        /// <param name="entries">Runtime scene catalog entries to persist.</param>
        void WriteRuntimeSceneCatalog(string buildRootPath, params RuntimeSceneCatalogEntry[] entries) {
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            string catalogPath = Path.Combine(buildRootPath, "runtime-scene-catalog.json");
            using StreamWriter writer = new StreamWriter(catalogPath, false, System.Text.Encoding.UTF8);
            writer.WriteLine("{");
            writer.WriteLine("  \"Entries\": [");
            for (int index = 0; index < entries.Length; index++) {
                RuntimeSceneCatalogEntry entry = entries[index];
                writer.WriteLine("    {");
                writer.WriteLine("      \"SceneId\": \"" + entry.SceneId + "\",");
                writer.WriteLine("      \"CookedRelativePath\": \"" + entry.CookedRelativePath + "\"");
                writer.Write("    }");
                if (index < entries.Length - 1) {
                    writer.Write(",");
                }

                writer.WriteLine();
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        /// <summary>
        /// Loads the packaged demo menu scene and returns its baked runtime host component.
        /// </summary>
        /// <param name="buildRootPath">Packaged build output root.</param>
        /// <returns>Loaded runtime menu host component.</returns>
        MenuComponent LoadPackagedMenu(string buildRootPath) {
            SceneAsset sceneAsset;
            string packagedScenePath = Path.Combine(
                buildRootPath,
                EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is MenuComponent));
            return Assert.IsType<MenuComponent>(Assert.Single(loadedRoot.Components, component => component is MenuComponent));
        }

        /// <summary>
        /// Finds one baked runtime item by stable item id.
        /// </summary>
        /// <param name="menuHostComponent">Loaded menu host component to inspect.</param>
        /// <param name="itemId">Stable item id to resolve.</param>
        /// <returns>Runtime item whose metadata matches the requested id.</returns>
        Entity FindMenuItemEntity(MenuComponent menuHostComponent, string itemId) {
            if (menuHostComponent == null) {
                throw new ArgumentNullException(nameof(menuHostComponent));
            }
            if (string.IsNullOrWhiteSpace(itemId)) {
                throw new ArgumentException("Menu item id must be provided.", nameof(itemId));
            }

            List<Entity> itemEntities = new List<Entity>();
            CollectEntitiesWithComponent<MenuItemComponent>(menuHostComponent.Parent, itemEntities);
            for (int itemIndex = 0; itemIndex < itemEntities.Count; itemIndex++) {
                Entity itemEntity = itemEntities[itemIndex];
                MenuItemComponent itemComponent = Assert.IsType<MenuItemComponent>(Assert.Single(itemEntity.Components, component => component is MenuItemComponent));
                if (!string.Equals(itemComponent.ItemId, itemId, StringComparison.Ordinal)) {
                    continue;
                }

                return itemEntity;
            }

            throw new InvalidOperationException($"Could not find baked menu item '{itemId}'.");
        }

        /// <summary>
        /// Creates one mouse state positioned inside the supplied menu row.
        /// </summary>
        /// <param name="runtimeItem">Runtime item whose bounds should receive the pointer.</param>
        /// <param name="leftButtonState">Left mouse button state to emit.</param>
        /// <returns>Mouse state centered inside the menu row.</returns>
        MouseState CreateMouseStateInsideMenuItem(MenuComponent menuHostComponent, string itemId, ButtonState leftButtonState) {
            Entity itemEntity = FindMenuItemEntity(menuHostComponent, itemId);
            RoundedRectComponent background = Assert.IsType<RoundedRectComponent>(Assert.Single(itemEntity.Components, component => component is RoundedRectComponent));
            int pointerX = (int)itemEntity.Position.X + (background.Size.X / 2);
            int pointerY = (int)itemEntity.Position.Y + (background.Size.Y / 2);
            return new MouseState(pointerX, pointerY, 0, leftButtonState, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        }

        /// <summary>
        /// Recursively collects entities that contain the supplied component type.
        /// </summary>
        /// <typeparam name="TComponent">Component type that marks a matching entity.</typeparam>
        /// <param name="entity">Root entity to inspect.</param>
        /// <param name="entities">Destination list receiving matching entities.</param>
        void CollectEntitiesWithComponent<TComponent>(Entity entity, List<Entity> entities) where TComponent : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            if (entity.Components != null && entity.Components.Any(component => component is TComponent)) {
                entities.Add(entity);
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                CollectEntitiesWithComponent<TComponent>(entity.Children[childIndex], entities);
            }
        }

        /// <summary>
        /// Writes one serialized directional light component payload.
        /// </summary>
        /// <returns>Serialized directional light component payload.</returns>
        byte[] WriteDirectionalLightComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WriteDirectionalLight(writer, new DirectionalLightComponent {
                Color = new float4(0.3f, 0.4f, 0.5f, 1f),
                Intensity = 3.0f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.7f,
                ShadowDistance = 64f
            });
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized point light component payload.
        /// </summary>
        /// <returns>Serialized point light component payload.</returns>
        byte[] WritePointLightComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WritePointLight(writer, new PointLightComponent {
                Color = new float4(1f, 0.8f, 0.6f, 1f),
                Intensity = 4.0f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Auto,
                ShadowStrength = 0.85f,
                Range = 18f
            });
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized spot light component payload.
        /// </summary>
        /// <returns>Serialized spot light component payload.</returns>
        byte[] WriteSpotLightComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WriteSpotLight(writer, new SpotLightComponent {
                Color = new float4(0.8f, 0.9f, 1f, 1f),
                Intensity = 2.5f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.45f,
                Range = 24f,
                InnerConeAngleDegrees = 20f,
                OuterConeAngleDegrees = 36f
            });
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one empty packaged scene asset referenced by the menu-host test provider.
        /// </summary>
        /// <param name="relativePath">Project-relative packaged scene path.</param>
        void WriteSceneAsset(string relativePath) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Creates one editor-authored scene entity configured for packaging tests.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <returns>Configured editor scene entity.</returns>
        EditorEntity CreateUserEntity(string name) {
            return new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
        }
    }
}

