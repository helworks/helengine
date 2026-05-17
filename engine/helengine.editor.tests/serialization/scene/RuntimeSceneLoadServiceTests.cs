using helengine.editor.tests.testing;
using helengine.files;
using helengine.ui;
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
        /// Next numeric scene entity id assigned to manually-authored editor entities in tests that run without an editor core.
        /// </summary>
        uint NextEditorEntityId = 1u;

        /// <summary>
        /// Initializes the runtime services required by the scene-load tests.
        /// </summary>
        public RuntimeSceneLoadServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-scene-load-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            EditorCore core = new EditorCore(new Project {
                Name = "Runtime Scene Load",
                Path = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.SetDefaultFontAssetForEditor(CreateFont());
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
        /// Ensures runtime scene loading restores the serialized static flag onto live entities.
        /// </summary>
        [Fact]
        public void Load_WhenSceneEntityStaticFlagsDiffer_RestoresStaticFlags() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "StaticRoot",
                        IsStatic = true,
                        Children = new[] {
                            new SceneEntityAsset {
                                Id = 2u,
                                Name = "DynamicChild",
                                IsStatic = false,
                                Children = Array.Empty<SceneEntityAsset>()
                            }
                        }
                    }
                }
            };

            Entity loadedRoot = Assert.Single(loadService.Load(sceneAsset));
            Assert.True(loadedRoot.Static);

            Entity loadedChild = Assert.Single(loadedRoot.Children);
            Assert.False(loadedChild.Static);
        }

        /// <summary>
        /// Resolves the packaged scene file path for one authored scene inside the supplied build output root.
        /// </summary>
        /// <param name="buildRootPath">Build output root that contains packaged scene assets.</param>
        /// <param name="sceneId">Authored scene id whose packaged output should be resolved.</param>
        /// <returns>Absolute packaged scene file path for the authored scene.</returns>
        static string GetPackagedScenePath(string buildRootPath, string sceneId) {
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return Path.Combine(buildRootPath, GetPackagedSceneRelativePath(sceneId).Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Resolves the packaged scene relative path for one authored scene.
        /// </summary>
        /// <param name="sceneId">Authored scene id whose packaged relative path should be resolved.</param>
        /// <returns>Packaged scene relative path that matches the canonical authored name.</returns>
        static string GetPackagedSceneRelativePath(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return PackagedScenePathResolver.BuildRelativePath(sceneId, 0);
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
                            Id = 1u,
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
                        Id = 1u,
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
        /// Ensures packaged runtime scene loading accepts FPS overlays whose payload omits the packaged font reference.
        /// </summary>
        [Fact]
        public void Load_WhenFpsPayloadOmitsFontReference_LoadsComponentWithNullFont() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.FPSComponent",
                                ComponentIndex = 0,
                                Payload = WriteFpsComponentPayloadWithoutFontReference()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            FPSComponent fpsComponent = Assert.IsType<FPSComponent>(Assert.Single(loadedRoot.Components, component => component is FPSComponent));

            Assert.Null(fpsComponent.Font);
            Assert.Equal(0.5d, fpsComponent.RefreshIntervalSeconds);
            Assert.Equal(new int2(8, 6), fpsComponent.Padding);
            Assert.Equal((byte)250, fpsComponent.RenderOrder2D);
        }

        /// <summary>
        /// Ensures older packaged FPS payload versions are rejected during runtime scene loading.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsOlderVersionFpsOverlay_ThrowsUnsupportedPayloadVersion() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.FPSComponent",
                                ComponentIndex = 0,
                                Payload = WriteOlderVersionFpsComponentPayload()
                            }
                        }
                    }
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(sceneAsset));
            Assert.Contains("Unsupported FPS component payload version", exception.Message);
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
                        Id = 1u,
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
        /// Ensures packaged runtime scene loading materializes mesh components that carry multiple material slots in payload version 2.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsVersion2MeshComponentWithMultipleMaterialSlots_MaterializesEverySlot() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayloadVersion2WithEmptyMaterialSlots()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(loadedRoot.Components, component => component is MeshComponent));

            Assert.Equal((byte)21, meshComponent.RenderOrder3D);
            Assert.Null(meshComponent.Model);
            Assert.Null(meshComponent.Material);
            Assert.Equal(2, meshComponent.Materials.Length);
            Assert.Null(meshComponent.Materials[0]);
            Assert.Null(meshComponent.Materials[1]);
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
                        Id = 1u,
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
                        Id = 1u,
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
        /// Ensures the default runtime component registry materializes the built-in platform-info overlay binder component.
        /// </summary>
        [Fact]
        public void RuntimeComponentRegistry_WhenUsingDefaultRegistry_MaterializesPlatformInfoTextComponent() {
            RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = PlatformInfoTextComponent.SerializedComponentTypeId,
                ComponentIndex = 0,
                Payload = WritePlatformInfoTextComponentPayload()
            };

            Assert.True(registry.TryGet(PlatformInfoTextComponent.SerializedComponentTypeId, out IRuntimeComponentDeserializer deserializer));

            PlatformInfoTextComponent component = Assert.IsType<PlatformInfoTextComponent>(deserializer.Deserialize(record, null));
            Assert.NotNull(component);
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
                        Id = 1u,
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
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TextScene.helen" }, buildRootPath);

            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/TextScene.helen");
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
        /// Ensures packaged runtime scene loading reuses one cooked font asset instance when multiple text components reference the same packaged font.
        /// </summary>
        [Fact]
        public void Load_WhenMultipleTextComponentsShareOnePackagedFont_ReusesTheSameFontAssetAndAtlas() {
            string projectRootPath = Path.Combine(TempRootPath, "shared-font-project");
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            string buildRootPath = Path.Combine(TempRootPath, "shared-font-build");
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(buildRootPath);

            WriteSourceFont(projectRootPath, "Fonts/DemoDiscBody.ttf");

            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "SharedFontScene.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            SceneAssetReference sharedFontReference = CreateFileFontReference("Fonts/DemoDiscBody.ttf");
            SceneAsset authoredSceneAsset = new SceneAsset {
                Id = "Scenes/SharedFontScene.helen",
                AssetReferences = new[] {
                    sharedFontReference
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "RootA",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.TextComponent",
                                ComponentIndex = 0,
                                Payload = WriteTextComponentPayload(sharedFontReference)
                            }
                        }
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "RootB",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "Helengine.TextComponent",
                                ComponentIndex = 0,
                                Payload = WriteTextComponentPayload(sharedFontReference)
                            }
                        }
                    }
                }
            };
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/SharedFontScene.helen" }, buildRootPath);

            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/SharedFontScene.helen");
            SceneAsset sceneAsset;
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            TestRenderManager2D renderManager2D = Assert.IsType<TestRenderManager2D>(Core.Instance.RenderManager2D);
            int buildTextureFromRawCallCountBeforeLoad = renderManager2D.BuildTextureFromRawCallCount;
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            RuntimeSceneLoadResult loadResult = loadService.LoadTracked(sceneAsset);
            IReadOnlyList<Entity> loadedRoots = loadResult.RootEntities;
            TextComponent firstTextComponent = Assert.IsType<TextComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is TextComponent));
            TextComponent secondTextComponent = Assert.IsType<TextComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is TextComponent));

            Assert.Same(firstTextComponent.Font, secondTextComponent.Font);
            Assert.Same(firstTextComponent.Font.Texture, secondTextComponent.Font.Texture);
            Assert.Equal(buildTextureFromRawCallCountBeforeLoad + 1, renderManager2D.BuildTextureFromRawCallCount);
            Assert.Single(loadResult.OwnedAssets.OwnedFonts);
            Assert.Single(loadResult.OwnedAssets.OwnedTextures);
        }

        /// <summary>
        /// Ensures packaged demo menus preserve decorative overlay sprites, their cooked texture references, and their bottom-right anchor.
        /// </summary>
        [Fact]
        public void Load_WhenPackagedDemoMenuUsesDecorativeOverlayImage_MaterializesSpriteWithCookedTexture() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-overlay-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-overlay-build", includeOverlayImage: true);

            SceneAsset sceneAsset;
            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/TestMenu.helen");
            using (FileStream packagedSceneStream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            }

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                buildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            List<Entity> spriteEntities = new List<Entity>();
            for (int rootIndex = 0; rootIndex < loadedRoots.Count; rootIndex++) {
                CollectEntitiesWithComponent<SpriteComponent>(loadedRoots[rootIndex], spriteEntities);
            }

            Entity overlayEntity = Assert.Single(spriteEntities);
            SpriteComponent spriteComponent = Assert.IsType<SpriteComponent>(Assert.Single(overlayEntity.Components, component => component is SpriteComponent));
            AnchorComponent anchorComponent = Assert.IsType<AnchorComponent>(Assert.Single(overlayEntity.Components, component => component is AnchorComponent));
            string importedTextureRootPath = Path.Combine(buildRootPath, "cooked", "imported");

            Assert.NotNull(spriteComponent.Texture);
            Assert.Equal(new int2(220, 220), spriteComponent.Size);
            Assert.Equal((byte)(AnchorComponent.RightAnchorFlag | AnchorComponent.BottomAnchorFlag), anchorComponent.AnchorFlags);
            Assert.Equal(new float4(0f, 44f, 0f, 36f), anchorComponent.AnchorDistances);
            Assert.NotEmpty(Directory.GetFiles(importedTextureRootPath, "*", SearchOption.AllDirectories));
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

            SceneAsset authoredSceneAsset = BuildMinimalSceneLoadingMenuSceneAsset();
            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen" }, buildRootPath);

            SceneAsset sceneAsset;
            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/TestMenu.helen");
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
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen" }, buildRootPath);

            SceneAsset sceneAsset;
            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/TestMenu.helen");
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
        /// Ensures the packaged runtime menu responds to primary-gamepad navigation and confirm input using the shared gamepad button contract.
        /// </summary>
        [Fact]
        public void Load_WhenMenuReceivesGamepadNavigationAndConfirm_OpensTheSelectedPanel() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-gamepad-confirm-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-gamepad-confirm-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

            input.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetGamepadStates(new[] { CreateConnectedGamepadState(InputGamepadButton.South) });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("options", menuHostComponent.ActivePanelId);
        }

        /// <summary>
        /// Ensures the packaged runtime menu responds to primary-gamepad directional navigation and back actions.
        /// </summary>
        [Fact]
        public void Load_WhenMenuReceivesGamepadNavigationAndBack_UpdatesSelectionAndReturnsToPreviousPanel() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-gamepad-back-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-gamepad-back-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

            input.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetGamepadStates(new[] { CreateConnectedGamepadState(InputGamepadButton.DPadDown) });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("open-scene-select", menuHostComponent.SelectedItemId);

            input.SetGamepadStates(new[] { CreateConnectedGamepadState(InputGamepadButton.South) });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("scene-select", menuHostComponent.ActivePanelId);

            input.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetGamepadStates(new[] { CreateConnectedGamepadState(InputGamepadButton.East) });
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("main", menuHostComponent.ActivePanelId);
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
        /// Ensures keyboard navigation keeps the selected scene row inside the visible scroll window.
        /// </summary>
        [Fact]
        public void Load_WhenMenuSelectionMovesPastVisibleSceneRows_ScrollsTheItemsRoot() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-selection-scroll-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-selection-scroll-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);
            ScrollComponent scrollComponent = FindPanelScrollComponent(menuHostComponent, "main");

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal(1, scrollComponent.ScrollOffset);
            Assert.Equal(new float3(0f, -(DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing), 0f), scrollComponent.Parent.LocalPosition);
            Assert.Equal("scene-alpha", menuHostComponent.SelectedItemId);
        }

        /// <summary>
        /// Ensures stationary mouse hover does not override keyboard-driven menu selection and scrolling.
        /// </summary>
        [Fact]
        public void Load_WhenMouseIsStationaryOverSceneList_KeyboardSelectionStillScrollsTheItemsRoot() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-selection-scroll-stationary-mouse-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-selection-scroll-stationary-mouse-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);
            ScrollComponent scrollComponent = FindPanelScrollComponent(menuHostComponent, "main");

            input.SetMouseState(CreateMouseStateInsideMenuItem(menuHostComponent, "open-options", ButtonState.Released));
            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetMouseState(CreateMouseStateInsideMenuItem(menuHostComponent, "open-options", ButtonState.Released));
            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetMouseState(CreateMouseStateInsideMenuItem(menuHostComponent, "open-options", ButtonState.Released));
            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetMouseState(CreateMouseStateInsideMenuItem(menuHostComponent, "open-options", ButtonState.Released));
            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal(1, scrollComponent.ScrollOffset);
            Assert.Equal(new float3(0f, -(DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing), 0f), scrollComponent.Parent.LocalPosition);
            Assert.Equal("scene-alpha", menuHostComponent.SelectedItemId);
        }

        /// <summary>
        /// Ensures mouse-wheel input inside the scene-list viewport advances the reusable scroll component and translates the baked item root.
        /// </summary>
        [Fact]
        public void Load_WhenSceneListViewportReceivesMouseWheel_ScrollsTheItemsRoot() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-wheel-scroll-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-wheel-scroll-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);
            ScrollComponent scrollComponent = FindPanelScrollComponent(menuHostComponent, "main");

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", 0, ButtonState.Released));
            Core.Instance.Update();

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", -120, ButtonState.Released));
            Core.Instance.Update();

            Assert.Equal(1, scrollComponent.ScrollOffset);
            Assert.Equal(new float3(0f, -(DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing), 0f), scrollComponent.Parent.LocalPosition);
        }

        /// <summary>
        /// Ensures the baked menu fit component scales the main panel and its scroll step when the live window is 640x480.
        /// </summary>
        [Fact]
        public void Load_WhenMenuRunsAt640x480_ScalesPanelLayoutAndScrollStep() {
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager.OnWindowResize(IntPtr.Zero, 640, 480);

            string projectRootPath = Path.Combine(TempRootPath, "menu-640x480-fit-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-640x480-fit-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            Core.Instance.Update();
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);
            Entity mainPanelEntity = FindPanelEntity(menuHostComponent, "main");
            ScrollComponent scrollComponent = FindPanelScrollComponent(menuHostComponent, "main");

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", 0, ButtonState.Released));
            Core.Instance.Update();

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", -120, ButtonState.Released));
            Core.Instance.Update();

            Assert.Equal(new float3(44f, 95f, 0f), mainPanelEntity.LocalPosition);
            Assert.Equal(new int2(210, 55), scrollComponent.Size);
            Assert.Equal(1, scrollComponent.ScrollOffset);
            Assert.Equal(new float3(0f, -31f, 0f), scrollComponent.Parent.LocalPosition);
        }

        /// <summary>
        /// Ensures repeated wheel input keeps using the fixed scene-list viewport after the fitted item root starts moving at 640x480.
        /// </summary>
        [Fact]
        public void Load_WhenMenuRunsAt640x480_AllowsRepeatedWheelScrollInsideFixedViewport() {
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager.OnWindowResize(IntPtr.Zero, 640, 480);

            string projectRootPath = Path.Combine(TempRootPath, "menu-640x480-repeat-wheel-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-640x480-repeat-wheel-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            Core.Instance.Update();
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);
            ScrollComponent scrollComponent = FindPanelScrollComponent(menuHostComponent, "main");

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", 0, ButtonState.Released));
            Core.Instance.Update();

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", -120, ButtonState.Released));
            Core.Instance.Update();

            input.SetMouseState(CreateMouseStateInsidePanelViewport(menuHostComponent, "main", -240, ButtonState.Released));
            Core.Instance.Update();

            Assert.Equal(2, scrollComponent.ScrollOffset);
            Assert.Equal(new float3(0f, -62f, 0f), scrollComponent.Parent.LocalPosition);
        }

        /// <summary>
        /// Ensures keyboard navigation scrolls a secondary scene-select panel opened from the main menu.
        /// </summary>
        [Fact]
        public void Load_WhenKeyboardNavigatesOpenedSceneSelectPanel_ScrollsThatPanelItemsRoot() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-secondary-panel-selection-scroll-project");
            string buildRootPath = PackageDemoMenuScene(projectRootPath, "menu-secondary-panel-selection-scroll-build");
            MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
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

            ScrollComponent scrollComponent = FindPanelScrollComponent(menuHostComponent, "scene-select");

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Down));
            input.EarlyUpdate();
            menuHostComponent.Update();
            input.Update();

            Assert.Equal("scene-select", menuHostComponent.ActivePanelId);
            Assert.Equal(1, scrollComponent.ScrollOffset);
            Assert.Equal(new float3(0f, -(DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing), 0f), scrollComponent.Parent.LocalPosition);
            Assert.Equal("scene-epsilon", menuHostComponent.SelectedItemId);
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
                global::helengine.files.EditorAssetBinarySerializer.Serialize(
                    playableSceneStream,
                    new SceneAsset {
                        Id = "TestPlayableScene",
                        RootEntities = new[] {
                            new SceneEntityAsset {
                                Id = 1u,
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
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen", "Scenes/TestPlayableScene.helen" }, buildRootPath);
            RuntimeSceneCatalog sceneCatalog = new RuntimeSceneCatalog(new[] {
                new RuntimeSceneCatalogEntry("TestMenu", GetPackagedSceneRelativePath("Scenes/TestMenu.helen")),
                new RuntimeSceneCatalogEntry("TestPlayableScene", "cooked/scenes/TestPlayableScene.hasset")
            });

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath,
                SceneCatalog = sceneCatalog
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));

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
            Assert.True(Core.Instance.SceneManager.IsSceneLoaded("TestPlayableScene"));
        }

        /// <summary>
        /// Ensures editor menu scene actions resolve stable scene ids through the configured scene-id path resolver.
        /// </summary>
        [Fact]
        public void Load_WhenEditorMenuActionLoadsScene_resolvesThroughSceneIdPathResolver() {
            string projectRootPath = Path.Combine(TempRootPath, "menu-editor-scene-resolver-project");
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            Directory.CreateDirectory(Path.Combine(assetsRootPath, "Fonts"));
            Directory.CreateDirectory(Path.Combine(assetsRootPath, "Scenes"));

            File.WriteAllBytes(Path.Combine(assetsRootPath, "Fonts", "DemoDiscTitle.ttf"), new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(Path.Combine(assetsRootPath, "Fonts", "DemoDiscBody.ttf"), new byte[] { 5, 6, 7, 8 });

            string playableScenePath = Path.Combine(assetsRootPath, "Scenes", "TestPlayableScene.helen");
            using (FileStream playableSceneStream = new FileStream(playableScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                global::helengine.files.EditorAssetBinarySerializer.Serialize(
                    playableSceneStream,
                    new SceneAsset {
                        Id = "TestPlayableScene",
                        RootEntities = new[] {
                            new SceneEntityAsset {
                                Id = 1u,
                                Name = "PlayableRoot",
                                Components = Array.Empty<SceneComponentAssetRecord>(),
                                Children = Array.Empty<SceneEntityAsset>()
                            }
                        }
                    });
            }

            TestSceneIdPathResolver scenePathResolver = new TestSceneIdPathResolver(new Dictionary<string, string> {
                ["TestPlayableScene"] = "Scenes/TestPlayableScene.helen"
            });
            EditorCore core = new EditorCore(new Project {
                Name = "Runtime Scene Load Editor Resolver",
                Path = assetsRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = assetsRootPath,
                ScenePathResolver = scenePathResolver
            });
            core.SetDefaultFontAssetForEditor(CreateFont());

            TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
            referenceResolver.RegisterFont(CreateFileFontReference("Fonts/DemoDiscTitle.ttf"), CreateFont());
            referenceResolver.RegisterFont(CreateFileFontReference("Fonts/DemoDiscBody.ttf"), CreateFont());

            MenuComponent menuHostComponent;
            SceneAsset menuSceneAsset = BuildMinimalSceneLoadingMenuSceneAsset();
            using (FileStream menuSceneStream = new FileStream(Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen"), FileMode.Create, FileAccess.Write, FileShare.None)) {
                global::helengine.files.EditorAssetBinarySerializer.Serialize(menuSceneStream, menuSceneAsset);
            }

            SceneLoadService sceneLoadService = new SceneLoadService(CreateDemoMenuPersistenceRegistry(), referenceResolver);
            IReadOnlyList<EditorEntity> loadedRoots = sceneLoadService.Load(menuSceneAsset);
            EditorEntity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is MenuComponent));
            menuHostComponent = Assert.IsType<MenuComponent>(Assert.Single(loadedRoot.Components, component => component is MenuComponent));

            TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

            EnterEditorAndRun(() => {
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
            });

            Assert.Equal("TestPlayableScene", scenePathResolver.LastResolvedSceneId);
            Assert.Equal(1, scenePathResolver.ResolveCallCount);
            Assert.NotNull(Core.Instance.SceneManager);
            Assert.True(Core.Instance.SceneManager.IsSceneLoaded("TestPlayableScene"));
            Assert.Contains("TestPlayableScene", Core.Instance.SceneManager.GetLoadedSceneIds(), StringComparer.Ordinal);
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
            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/Scripted.helen");
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
                        Id = 1u,
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
                        Id = 2u,
                        Name = "AmbientLight",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.AmbientLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteAmbientLightComponentPayload()
                            }
                        }
                    },
                    new SceneEntityAsset {
                        Id = 3u,
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
                        Id = 4u,
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
            Assert.Equal(4, loadedRoots.Count);

            DirectionalLightComponent directionalLight = Assert.IsType<DirectionalLightComponent>(Assert.Single(loadedRoots[0].Components, component => component is DirectionalLightComponent));
            AmbientLightComponent ambientLight = Assert.IsType<AmbientLightComponent>(Assert.Single(loadedRoots[1].Components, component => component is AmbientLightComponent));
            PointLightComponent pointLight = Assert.IsType<PointLightComponent>(Assert.Single(loadedRoots[2].Components, component => component is PointLightComponent));
            SpotLightComponent spotLight = Assert.IsType<SpotLightComponent>(Assert.Single(loadedRoots[3].Components, component => component is SpotLightComponent));

            Assert.Equal(new float4(0.3f, 0.4f, 0.5f, 1f), directionalLight.Color);
            Assert.Equal(3.0f, directionalLight.Intensity);
            Assert.True(directionalLight.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Forced, directionalLight.ShadowMapMode);
            Assert.Equal(0.7f, directionalLight.ShadowStrength);
            Assert.Equal(64f, directionalLight.ShadowDistance);

            Assert.Equal(new float4(0.2f, 0.25f, 0.3f, 1f), ambientLight.Color);
            Assert.Equal(1.5f, ambientLight.Intensity);
            Assert.False(ambientLight.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Disabled, ambientLight.ShadowMapMode);
            Assert.Equal(0.2f, ambientLight.ShadowStrength);

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
        /// Ensures older packaged light payload version 1 is rejected during runtime scene loading.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsLegacyLightComponentPayloadVersion1_ThrowsUnsupportedPayloadVersion() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            byte[] olderVersionPayload;
            using (MemoryStream stream = new MemoryStream()) {
                using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
                writer.WriteByte(1);
                writer.WriteSingle(0.8f);
                writer.WriteSingle(0.9f);
                writer.WriteSingle(1f);
                writer.WriteSingle(1f);
                writer.WriteSingle(2.5f);
                writer.WriteByte(0);
                writer.WriteByte((byte)ShadowMapMode.Disabled);
                writer.WriteSingle(0.45f);
                writer.WriteSingle(24f);
                writer.WriteSingle(20f);
                writer.WriteSingle(36f);
                olderVersionPayload = stream.ToArray();
            }

            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "SpotLight",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.SpotLightComponent",
                                ComponentIndex = 0,
                                Payload = olderVersionPayload
                            }
                        }
                    }
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(sceneAsset));
            Assert.Contains("Unsupported spot light payload version", exception.Message);
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
        /// Writes one serialized FPS component payload that omits the packaged font reference.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayloadWithoutFontReference() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteByte(0);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(0.5d));
            writer.WriteInt2(new int2(8, 6));
            writer.WriteByte(250);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one older serialized FPS component payload that omits the packaged font reference.
        /// </summary>
        /// <returns>Serialized older-version FPS component payload.</returns>
        byte[] WriteOlderVersionFpsComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
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
            writer.WriteByte(MeshComponentScenePayloadSerializer.CurrentVersion);
            writer.WriteByte(0);
            writer.WriteInt32(0);
            writer.WriteByte(9);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one version-2 serialized mesh component payload with two empty material slots.
        /// </summary>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayloadVersion2WithEmptyMaterialSlots() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteByte(0);
            writer.WriteInt32(2);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteByte(21);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized camera component payload.
        /// </summary>
        /// <returns>Serialized camera component payload.</returns>
        byte[] WriteCameraComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(3);
            writer.WriteByte(17);
            writer.WriteUInt16(EditorLayerMasks.SceneObjects);
            writer.WriteSingle(12f);
            writer.WriteSingle(24f);
            writer.WriteSingle(640f);
            writer.WriteSingle(360f);
            writer.WriteSingle(0.42f);
            writer.WriteSingle(128f);
            writer.WriteByte(1);
            writer.WriteSingle(0.25f);
            writer.WriteSingle(0.5f);
            writer.WriteSingle(0.75f);
            writer.WriteSingle(1f);
            writer.WriteByte(1);
            writer.WriteSingle(0.42f);
            writer.WriteByte(1);
            writer.WriteByte(9);
            writer.WriteByte((byte)DepthPrepassMode.Always);
            writer.WriteSingle(128f);
            writer.WriteByte((byte)PostProcessTier.High);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized platform-info overlay binder payload.
        /// </summary>
        /// <returns>Serialized built-in platform-info overlay binder payload.</returns>
        byte[] WritePlatformInfoTextComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(0);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds one baked demo menu scene asset for runtime materialization tests.
        /// </summary>
        /// <returns>Baked demo menu scene asset.</returns>
        SceneAsset BuildDemoMenuSceneAsset(bool includeOverlayImage = false) {
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
                            2,
                            new[] {
                                new MenuItemDefinition("open-options", "Options", "Opens the options panel.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "options")),
                                new MenuItemDefinition("open-scene-select", "Select Scene", "Opens the scene selection panel.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "scene-select")),
                                new MenuItemDefinition("scene-alpha", "Scene Alpha", "Preview alpha district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-beta", "Scene Beta", "Preview beta district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-gamma", "Scene Gamma", "Preview gamma district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            }),
                        new MenuPanelDefinition(
                            "options",
                            "Options",
                            "Secondary runtime test panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("load-scene", "Select Scene", "Loads a scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "TestPlayableScene")),
                                new MenuItemDefinition("options-back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            }),
                        new MenuPanelDefinition(
                            "scene-select",
                            "Select Scene",
                            "Secondary scene selection panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("scene-alpha", "Scene Alpha", "Preview alpha district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-beta", "Scene Beta", "Preview beta district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-gamma", "Scene Gamma", "Preview gamma district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-delta", "Scene Delta", "Preview delta district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-epsilon", "Scene Epsilon", "Preview epsilon district.", true, new MenuActionDefinition(MenuActionKind.None, string.Empty)),
                                new MenuItemDefinition("scene-select-back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            })
                    },
                    includeOverlayImage
                        ? new MenuOverlayImageDefinition("Images/Menu/logo.png", 220, 220, 36, 44)
                        : null));
        }

        /// <summary>
        /// Creates the component persistence registry required to load the authored baked menu scene shape in editor mode.
        /// </summary>
        /// <returns>Persistence registry containing the baked menu descriptors.</returns>
        ComponentPersistenceRegistry CreateDemoMenuPersistenceRegistry() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            registry.Register(new MenuComponentPersistenceDescriptor());
            registry.Register(new MenuPanelComponentPersistenceDescriptor());
            registry.Register(new MenuItemComponentPersistenceDescriptor());
            registry.Register(new MenuSelectedDescriptionComponentPersistenceDescriptor());
            registry.Register(new RoundedRectComponentPersistenceDescriptor());
            registry.Register(new TextComponentPersistenceDescriptor());
            registry.Register(new FPSComponentPersistenceDescriptor());
            return registry;
        }

        /// <summary>
        /// Builds one minimal baked menu scene asset used by the scene-loading menu regressions.
        /// </summary>
        /// <returns>Baked menu scene asset with one panel transition and one scene-loading action.</returns>
        SceneAsset BuildMinimalSceneLoadingMenuSceneAsset() {
            DemoMenuSceneAssetFactory factory = new DemoMenuSceneAssetFactory();
            return factory.BuildSceneAsset(
                "TestMenu",
                typeof(TestMenuDefinitionProvider).AssemblyQualifiedName,
                new MenuDefinition(
                    "Demo",
                    "Editor",
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
                            "Editor resolver test panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("open-options", "Options", "Opens the options panel.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "options"))
                            }),
                        new MenuPanelDefinition(
                            "options",
                            "Options",
                            "Loads one authored test scene through the editor resolver.",
                            4,
                            new[] {
                                new MenuItemDefinition("load-scene", "Load Scene", "Loads the playable scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "TestPlayableScene")),
                                new MenuItemDefinition("back", "Back", "Returns to the main menu.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            })
                    }));
        }

        /// <summary>
        /// Packages the baked demo menu scene into one temporary build root.
        /// </summary>
        /// <param name="projectRootPath">Temporary project root that should receive authored content.</param>
        /// <param name="buildFolderName">Unique build folder name under the temporary root.</param>
        /// <returns>Packaged build output root.</returns>
        string PackageDemoMenuScene(string projectRootPath, string buildFolderName, bool includeOverlayImage = false) {
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

            if (includeOverlayImage) {
                string overlayTexturePath = Path.Combine(assetsRootPath, "Images", "Menu", "logo.png");
                Directory.CreateDirectory(Path.GetDirectoryName(overlayTexturePath));
                File.WriteAllBytes(overlayTexturePath, new byte[] { 9, 10, 11, 12 });
            }

            SceneAsset authoredSceneAsset = BuildDemoMenuSceneAsset(includeOverlayImage);
            string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
            using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                projectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" }),
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" })
                },
                CreateFont());
            packager.Package(new[] { "Scenes/TestMenu.helen" }, buildRootPath);
            return buildRootPath;
        }

        /// <summary>
        /// Loads the packaged demo menu scene and returns its baked runtime host component.
        /// </summary>
        /// <param name="buildRootPath">Packaged build output root.</param>
        /// <returns>Loaded runtime menu host component.</returns>
        MenuComponent LoadPackagedMenu(string buildRootPath) {
            SceneAsset sceneAsset;
            string packagedScenePath = GetPackagedScenePath(buildRootPath, "Scenes/TestMenu.helen");
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
        /// Finds the baked runtime panel entity that owns the supplied stable panel id.
        /// </summary>
        /// <param name="menuHostComponent">Loaded menu host component to inspect.</param>
        /// <param name="panelId">Stable panel id to resolve.</param>
        /// <returns>Panel entity whose metadata matches the requested id.</returns>
        Entity FindPanelEntity(MenuComponent menuHostComponent, string panelId) {
            if (menuHostComponent == null) {
                throw new ArgumentNullException(nameof(menuHostComponent));
            }
            if (string.IsNullOrWhiteSpace(panelId)) {
                throw new ArgumentException("Panel id must be provided.", nameof(panelId));
            }

            List<Entity> panelEntities = new List<Entity>();
            CollectEntitiesWithComponent<MenuPanelComponent>(menuHostComponent.Parent, panelEntities);
            for (int panelIndex = 0; panelIndex < panelEntities.Count; panelIndex++) {
                Entity panelEntity = panelEntities[panelIndex];
                MenuPanelComponent panelComponent = Assert.IsType<MenuPanelComponent>(Assert.Single(panelEntity.Components, component => component is MenuPanelComponent));
                if (string.Equals(panelComponent.PanelId, panelId, StringComparison.Ordinal)) {
                    return panelEntity;
                }
            }

            throw new InvalidOperationException($"Could not find baked menu panel '{panelId}'.");
        }

        /// <summary>
        /// Finds the row-based scroll component baked beneath one runtime menu panel.
        /// </summary>
        /// <param name="menuHostComponent">Loaded menu host component to inspect.</param>
        /// <param name="panelId">Stable panel id whose scroll component should be resolved.</param>
        /// <returns>Resolved panel scroll component.</returns>
        ScrollComponent FindPanelScrollComponent(MenuComponent menuHostComponent, string panelId) {
            Entity panelEntity = FindPanelEntity(menuHostComponent, panelId);
            List<Entity> scrollEntities = new List<Entity>();
            CollectEntitiesWithComponent<ScrollComponent>(panelEntity, scrollEntities);
            Entity scrollEntity = Assert.Single(scrollEntities);
            return Assert.IsType<ScrollComponent>(Assert.Single(scrollEntity.Components, component => component is ScrollComponent));
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
        /// Creates one mouse state positioned inside the clipped scene-list viewport for the supplied panel.
        /// </summary>
        /// <param name="menuHostComponent">Loaded menu host component that owns the panel.</param>
        /// <param name="panelId">Stable panel id whose viewport should receive the pointer.</param>
        /// <param name="wheelDelta">Mouse-wheel delta to expose for the frame.</param>
        /// <param name="leftButtonState">Left mouse button state to emit.</param>
        /// <returns>Mouse state centered inside the panel viewport.</returns>
        MouseState CreateMouseStateInsidePanelViewport(MenuComponent menuHostComponent, string panelId, int wheelDelta, ButtonState leftButtonState) {
            Entity panelEntity = FindPanelEntity(menuHostComponent, panelId);
            List<Entity> clipEntities = new List<Entity>();
            CollectEntitiesWithComponent<ClipRectComponent>(panelEntity, clipEntities);
            Entity viewportEntity = Assert.Single(clipEntities);
            ClipRectComponent clipComponent = Assert.IsType<ClipRectComponent>(Assert.Single(viewportEntity.Components, component => component is ClipRectComponent));
            int pointerX = (int)viewportEntity.Position.X + (clipComponent.Size.X / 2);
            int pointerY = (int)viewportEntity.Position.Y + (clipComponent.Size.Y / 2);
            return new MouseState(pointerX, pointerY, wheelDelta, leftButtonState, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        }

        /// <summary>
        /// Creates one connected gamepad state with the supplied buttons marked active.
        /// </summary>
        /// <param name="buttons">Buttons to mark active in the created state.</param>
        /// <returns>Connected gamepad state configured for the current test step.</returns>
        InputGamepadState CreateConnectedGamepadState(params InputGamepadButton[] buttons) {
            InputGamepadState gamepadState = new InputGamepadState {
                Connected = true
            };
            if (buttons == null) {
                throw new ArgumentNullException(nameof(buttons));
            }

            for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++) {
                gamepadState.SetButtonDown(buttons[buttonIndex], true);
            }

            return gamepadState;
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
        /// Executes one action while the current thread is marked as editor component execution.
        /// </summary>
        /// <param name="action">Action to run inside the editor execution scope.</param>
        void EnterEditorAndRun(Action action) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            ComponentExecutionContext.EnterEditor();
            try {
                action();
            } finally {
                ComponentExecutionContext.ExitEditor();
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
        /// Writes one serialized ambient light component payload.
        /// </summary>
        /// <returns>Serialized ambient light component payload.</returns>
        byte[] WriteAmbientLightComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WriteAmbientLight(writer, new AmbientLightComponent {
                Color = new float4(0.2f, 0.25f, 0.3f, 1f),
                Intensity = 1.5f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.2f
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
                        Id = 1u,
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
            EditorEntity entity = new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
            saveComponent.EntityId = NextEditorEntityId;
            NextEditorEntityId++;
            return entity;
        }
    }
}

