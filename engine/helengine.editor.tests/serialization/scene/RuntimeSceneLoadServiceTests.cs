using helengine.editor.tests.testing;
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
    }
}

