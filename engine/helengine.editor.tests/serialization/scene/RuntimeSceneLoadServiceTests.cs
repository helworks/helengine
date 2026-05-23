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
        /// Ensures runtime scene-load diagnostics expose safe empty strings before any materialization occurs.
        /// </summary>
        [Fact]
        public void Constructor_whenCreated_initializesDiagnosticStringsToEmptyValues() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            Assert.Equal(string.Empty, resolver.LastTextLoadStage);
            Assert.Equal(string.Empty, resolver.LastTextFontRelativePath);
            Assert.Equal(string.Empty, loadService.LastTraceStage);
            Assert.Equal(string.Empty, loadService.LastTraceComponentTypeId);
            Assert.Equal(string.Empty, loadService.LastTextLoadStage);
            Assert.Equal(string.Empty, loadService.LastTextFontRelativePath);
            Assert.NotNull(loadService.LastFontDeserializeStage);
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
                                ComponentTypeId = "helengine.FPSComponent",
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
                                ComponentTypeId = "helengine.FPSComponent",
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
        /// Ensures packaged runtime scene loading materializes debug overlay components on the player side.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsDebugComponent_MaterializesTheComponent() {
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
                                ComponentTypeId = "helengine.DebugComponent",
                                ComponentIndex = 0,
                                Payload = WriteDebugComponentPayload()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            DebugComponent debugComponent = Assert.IsType<DebugComponent>(Assert.Single(loadedRoot.Components, component => component is DebugComponent));

            Assert.Equal(0.5d, debugComponent.RefreshIntervalSeconds);
            Assert.Equal(new int2(8, 6), debugComponent.Padding);
            Assert.Equal((byte)250, debugComponent.RenderOrder2D);
            Assert.NotNull(debugComponent.Font);
            Assert.Equal(16f, debugComponent.Font.LineHeight);
        }

        /// <summary>
        /// Ensures packaged runtime scene loading accepts debug overlays whose payload omits the packaged font reference.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsDebugComponentWithoutFontReference_LoadsComponentWithNullFont() {
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
                                ComponentTypeId = "helengine.DebugComponent",
                                ComponentIndex = 0,
                                Payload = WriteDebugComponentPayloadWithoutFontReference()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            DebugComponent debugComponent = Assert.IsType<DebugComponent>(Assert.Single(loadedRoot.Components, component => component is DebugComponent));

            Assert.Null(debugComponent.Font);
            Assert.Equal(0.5d, debugComponent.RefreshIntervalSeconds);
            Assert.Equal(new int2(8, 6), debugComponent.Padding);
            Assert.Equal((byte)250, debugComponent.RenderOrder2D);
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
                                ComponentTypeId = "helengine.FPSComponent",
                                ComponentIndex = 0,
                                Payload = WriteOlderVersionFpsComponentPayload()
                            }
                        }
                    }
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(sceneAsset));
            Assert.Contains("Unsupported automatic scripted component payload version", exception.Message);
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
                                ComponentTypeId = "helengine.MeshComponent",
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
                                ComponentTypeId = "helengine.MeshComponent",
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
                                ComponentTypeId = "helengine.CameraComponent",
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
                                ComponentTypeId = "helengine.TextComponent",
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
        /// Ensures packaged runtime font resolution rebuilds one live atlas texture when the packaged font references one external cooked atlas payload.
        /// </summary>
        [Fact]
        public void ResolveFont_WhenPackagedFontUsesExternalCookedAtlas_LoadsTheExternalAtlasTexture() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            FontAsset packagedFontAsset = new FontAsset(
                new FontInfo("ExternalAtlasFont", 16, 4f),
                null,
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1) {
                CookedAtlasTextureRelativePath = "cooked/fonts/external-atlas.ps2tex"
            };
            TextureAsset externalAtlasTextureAsset = new TextureAsset {
                Width = 2,
                Height = 2,
                Colors = new byte[] {
                    255, 255, 255, 255,
                    255, 255, 255, 255,
                    255, 255, 255, 255,
                    255, 255, 255, 255
                }
            };

            WriteFontAsset("fonts/default.hefont", packagedFontAsset);
            WriteTextureAsset("cooked/fonts/external-atlas.ps2tex", externalAtlasTextureAsset);

            FontAsset resolvedFontAsset = resolver.ResolveFont(CreateFileFontReference("fonts/default.hefont"));

            Assert.NotNull(resolvedFontAsset.Texture);
            Assert.NotNull(resolvedFontAsset.SourceTextureAsset);
            Assert.Equal(2, resolvedFontAsset.AtlasWidth);
            Assert.Equal(2, resolvedFontAsset.AtlasHeight);
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
                                ComponentTypeId = "helengine.TextComponent",
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
                                ComponentTypeId = "helengine.TextComponent",
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
            Assert.Empty(loadResult.OwnedAssets.OwnedTextures);
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
            byte[] olderVersionPayload = WriteSpotLightComponentPayload();
            olderVersionPayload[0] = 2;

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
            Assert.Contains("Unsupported automatic scripted component payload version", exception.Message);
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
            FPSComponent fpsComponent = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(FPSComponent.Font), CreateFileFontReference("fonts/default.hefont"));
            return WriteAutomaticRuntimeComponentPayload(fpsComponent, saveState);
        }

        /// <summary>
        /// Serializes one engine component through the unified automatic reflected persistence path used by player packaging.
        /// </summary>
        /// <param name="component">Engine component to serialize.</param>
        /// <param name="saveState">Optional asset-reference state associated with the component.</param>
        /// <returns>Serialized automatic component payload.</returns>
        byte[] WriteAutomaticComponentPayload(Component component, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Serializes one engine component through the packaged automatic reflected runtime payload path used by the player.
        /// </summary>
        /// <param name="component">Engine component to serialize.</param>
        /// <param name="saveState">Optional asset-reference state associated with the component.</param>
        /// <returns>Serialized automatic runtime component payload.</returns>
        byte[] WriteAutomaticRuntimeComponentPayload(Component component, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
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

            return stream.ToArray();
        }

        /// <summary>
        /// Flattens the serialized component hierarchy for one scene entity array.
        /// </summary>
        /// <param name="entities">Serialized scene entities whose components should be enumerated.</param>
        /// <returns>Flattened component records in depth-first order.</returns>
        IReadOnlyList<SceneComponentAssetRecord> FlattenComponents(SceneEntityAsset[] entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            List<SceneComponentAssetRecord> components = new List<SceneComponentAssetRecord>();
            AppendComponents(entities, components);
            return components;
        }

        /// <summary>
        /// Appends all serialized components from the supplied scene subtree into the destination list.
        /// </summary>
        /// <param name="entities">Serialized scene entities whose components should be appended.</param>
        /// <param name="components">Destination list receiving the flattened component records.</param>
        void AppendComponents(SceneEntityAsset[] entities, List<SceneComponentAssetRecord> components) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            } else if (components == null) {
                throw new ArgumentNullException(nameof(components));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                }

                SceneComponentAssetRecord[] entityComponents = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
                for (int componentIndex = 0; componentIndex < entityComponents.Length; componentIndex++) {
                    if (entityComponents[componentIndex] != null) {
                        components.Add(entityComponents[componentIndex]);
                    }
                }

                AppendComponents(entity.Children ?? Array.Empty<SceneEntityAsset>(), components);
            }
        }

        /// <summary>
        /// Writes one serialized FPS component payload that omits the packaged font reference.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayloadWithoutFontReference() {
            FPSComponent fpsComponent = new FPSComponent {
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            return WriteAutomaticRuntimeComponentPayload(fpsComponent, null);
        }

        /// <summary>
        /// Writes one serialized debug component payload.
        /// </summary>
        /// <returns>Serialized debug component payload.</returns>
        byte[] WriteDebugComponentPayload() {
            DebugComponent debugComponent = new DebugComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(DebugComponent.Font), CreateFileFontReference("fonts/default.hefont"));
            return WriteAutomaticRuntimeComponentPayload(debugComponent, saveState);
        }

        /// <summary>
        /// Writes one serialized debug component payload that omits the packaged font reference.
        /// </summary>
        /// <returns>Serialized debug component payload.</returns>
        byte[] WriteDebugComponentPayloadWithoutFontReference() {
            DebugComponent debugComponent = new DebugComponent {
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            return WriteAutomaticRuntimeComponentPayload(debugComponent, null);
        }

        /// <summary>
        /// Writes one older serialized FPS component payload that omits the packaged font reference.
        /// </summary>
        /// <returns>Serialized older-version FPS component payload.</returns>
        byte[] WriteOlderVersionFpsComponentPayload() {
            FPSComponent fpsComponent = new FPSComponent {
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            byte[] payload = WriteAutomaticRuntimeComponentPayload(fpsComponent, null);
            payload[0] = 2;
            return payload;
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
        /// Writes one serialized texture asset at the supplied packaged content path.
        /// </summary>
        /// <param name="relativePath">Packaged content-relative path.</param>
        /// <param name="textureAsset">Texture asset payload to serialize.</param>
        void WriteTextureAsset(string relativePath, TextureAsset textureAsset) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, textureAsset);
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
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
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
            return WriteAutomaticRuntimeComponentPayload(new DirectionalLightComponent {
                Color = new float4(0.3f, 0.4f, 0.5f, 1f),
                Intensity = 3.0f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.7f,
                ShadowDistance = 64f
            }, null);
        }

        /// <summary>
        /// Writes one serialized ambient light component payload.
        /// </summary>
        /// <returns>Serialized ambient light component payload.</returns>
        byte[] WriteAmbientLightComponentPayload() {
            return WriteAutomaticRuntimeComponentPayload(new AmbientLightComponent {
                Color = new float4(0.2f, 0.25f, 0.3f, 1f),
                Intensity = 1.5f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.2f
            }, null);
        }

        /// <summary>
        /// Writes one serialized point light component payload.
        /// </summary>
        /// <returns>Serialized point light component payload.</returns>
        byte[] WritePointLightComponentPayload() {
            return WriteAutomaticRuntimeComponentPayload(new PointLightComponent {
                Color = new float4(1f, 0.8f, 0.6f, 1f),
                Intensity = 4.0f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Auto,
                ShadowStrength = 0.85f,
                Range = 18f
            }, null);
        }

        /// <summary>
        /// Writes one serialized spot light component payload.
        /// </summary>
        /// <returns>Serialized spot light component payload.</returns>
        byte[] WriteSpotLightComponentPayload() {
            return WriteAutomaticRuntimeComponentPayload(new SpotLightComponent {
                Color = new float4(0.8f, 0.9f, 1f, 1f),
                Intensity = 2.5f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.45f,
                Range = 24f,
                InnerConeAngleDegrees = 20f,
                OuterConeAngleDegrees = 36f
            }, null);
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

