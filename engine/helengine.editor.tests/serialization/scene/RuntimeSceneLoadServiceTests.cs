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
        /// Ensures packaged runtime scene loading materializes menu-host components through the default runtime registry.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsMenuHostComponent_MaterializesTheComponent() {
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                Core.Instance.ContentManager,
                TempRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            WriteFontAsset("fonts/title.hefont", CreateFont());
            WriteFontAsset("fonts/body.hefont", CreateFont());
            WriteSceneAsset("Scenes/TestPlayableScene.helen");
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = MenuHostComponent.SerializedComponentTypeId,
                                ComponentIndex = 0,
                                Payload = WriteMenuHostComponentPayload()
                            }
                        }
                    }
                }
            };

            IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
            Entity loadedRoot = Assert.Single(loadedRoots);
            MenuHostComponent menuHostComponent = Assert.IsType<MenuHostComponent>(Assert.Single(loadedRoot.Components, component => component is MenuHostComponent));

            Assert.Equal(typeof(TestMenuDefinitionProvider).AssemblyQualifiedName, menuHostComponent.ProviderTypeName);
            Assert.True(menuHostComponent.IsInitialized);
            Assert.Equal("main", menuHostComponent.ActivePanelId);
            Assert.Equal("select-scene", menuHostComponent.SelectedItemId);
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
        /// Writes one serialized menu-host component payload.
        /// </summary>
        /// <returns>Serialized menu-host component payload.</returns>
        byte[] WriteMenuHostComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuHostComponent.CurrentVersion);
            writer.WriteString(typeof(TestMenuDefinitionProvider).AssemblyQualifiedName);
            return stream.ToArray();
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
                ShadowStrength = 0.7f
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
    }
}

