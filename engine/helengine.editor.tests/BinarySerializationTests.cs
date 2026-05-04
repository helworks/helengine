using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the engine's custom binary serializers for assets and editor metadata.
    /// </summary>
    public class BinarySerializationTests : IDisposable {
        /// <summary>
        /// Temporary root used for file-backed serializer tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes a new serializer test fixture with an isolated temporary root.
        /// </summary>
        public BinarySerializationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-binary-serialization-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Removes the temporary serializer test root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the little-endian binary writer and reader keep payload byte order stable.
        /// </summary>
        [Fact]
        public void EngineBinaryReaderWriter_LittleEndian_RoundTripsValues() {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriterLE writer = new BinaryWriterLE(stream)) {
                writer.WriteUInt16(0x1234);
                writer.WriteInt32(0x12345678);
                writer.WriteInt64(0x0102030405060708L);
                writer.WriteSingle(1.5f);
                writer.WriteString("AB");
                writer.WriteByteArray(new byte[] { 9, 8, 7 });
            }

            byte[] data = stream.ToArray();
            Assert.Equal(new byte[] { 0x34, 0x12 }, data.Take(2).ToArray());
            Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, data.Skip(2).Take(4).ToArray());
            stream.Position = 0;

            using BinaryReaderLE reader = new BinaryReaderLE(stream);
            Assert.Equal((ushort)0x1234, reader.ReadUInt16());
            Assert.Equal(0x12345678, reader.ReadInt32());
            Assert.Equal(0x0102030405060708L, reader.ReadInt64());
            Assert.Equal(1.5f, reader.ReadSingle());
            Assert.Equal("AB", reader.ReadString());
            Assert.Equal(new byte[] { 9, 8, 7 }, reader.ReadByteArray());
        }

        /// <summary>
        /// Ensures the big-endian binary writer and reader keep payload byte order stable.
        /// </summary>
        [Fact]
        public void EngineBinaryReaderWriter_BigEndian_RoundTripsValues() {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriterBE writer = new BinaryWriterBE(stream)) {
                writer.WriteUInt16(0x1234);
                writer.WriteInt32(0x12345678);
                writer.WriteInt64(0x0102030405060708L);
                writer.WriteSingle(1.5f);
                writer.WriteString("AB");
                writer.WriteByteArray(new byte[] { 9, 8, 7 });
            }

            byte[] data = stream.ToArray();
            Assert.Equal(new byte[] { 0x12, 0x34 }, data.Take(2).ToArray());
            Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, data.Skip(2).Take(4).ToArray());
            stream.Position = 0;

            using BinaryReaderBE reader = new BinaryReaderBE(stream);
            Assert.Equal((ushort)0x1234, reader.ReadUInt16());
            Assert.Equal(0x12345678, reader.ReadInt32());
            Assert.Equal(0x0102030405060708L, reader.ReadInt64());
            Assert.Equal(1.5f, reader.ReadSingle());
            Assert.Equal("AB", reader.ReadString());
            Assert.Equal(new byte[] { 9, 8, 7 }, reader.ReadByteArray());
        }

        /// <summary>
        /// Ensures scene assets round-trip through the HELE asset serializer and emit the expected file header.
        /// </summary>
        [Fact]
        public void AssetSerializer_SceneAsset_WritesHeleHeaderAndRoundTrips() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/TestScene.helen",
                Physics3DSceneFeatureFlags = 1234u,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = new float3(1f, 2f, 3f),
                        LocalScale = new float3(2f, 2f, 2f),
                        LocalOrientation = new float4(0f, 0.70710677f, 0f, 0.70710677f),
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.core.MeshComponent",
                                ComponentIndex = 0,
                                Payload = new byte[] { 1, 2, 3, 4 }
                            }
                        },
                        Children = new[] {
                            new SceneEntityAsset {
                                Id = "child-entity",
                                Name = "Child",
                                LocalPosition = new float3(5f, 6f, 7f),
                                LocalScale = float3.One,
                                LocalOrientation = float4.Identity,
                                Components = Array.Empty<SceneComponentAssetRecord>(),
                                Children = Array.Empty<SceneEntityAsset>()
                            }
                        }
                    }
                }
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            SceneAsset deserialized = (SceneAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)EditorAssetBinaryValueKind.SceneAsset, header.ValueKind);
            Assert.Equal("Scenes/TestScene.helen", deserialized.Id);
            Assert.Single(deserialized.RootEntities);
            Assert.Equal("root-entity", deserialized.RootEntities[0].Id);
            Assert.Equal(new float3(1f, 2f, 3f), deserialized.RootEntities[0].LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), deserialized.RootEntities[0].LocalScale);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, deserialized.RootEntities[0].Components[0].Payload);
            Assert.Equal(1234u, deserialized.Physics3DSceneFeatureFlags);
            Assert.Equal("child-entity", deserialized.RootEntities[0].Children[0].Id);
            Assert.Equal("Child", deserialized.RootEntities[0].Children[0].Name);
        }

        /// <summary>
        /// Ensures texture assets round-trip through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_TextureAsset_RoundTripsValues() {
            TextureAsset asset = CreateTextureAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            TextureAsset deserialized = (TextureAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.Width, deserialized.Width);
            Assert.Equal(asset.Height, deserialized.Height);
            Assert.Equal(asset.Colors, deserialized.Colors);
        }

        /// <summary>
        /// Ensures text assets round-trip through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_TextAsset_RoundTripsValues() {
            TextAsset asset = CreateTextAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            TextAsset deserialized = (TextAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.Text, deserialized.Text);
        }

        /// <summary>
        /// Ensures material assets round-trip through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_MaterialAsset_RoundTripsValues() {
            MaterialAsset asset = CreateMaterialAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            MaterialAsset deserialized = (MaterialAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.ShaderAssetId, deserialized.ShaderAssetId);
            Assert.Equal(asset.VertexProgram, deserialized.VertexProgram);
            Assert.Equal(asset.PixelProgram, deserialized.PixelProgram);
            Assert.Equal(asset.Variant, deserialized.Variant);
            Assert.Equal(asset.RenderState.BlendMode, deserialized.RenderState.BlendMode);
            Assert.Equal(asset.RenderState.CullMode, deserialized.RenderState.CullMode);
            Assert.Equal(asset.RenderState.DepthTestEnabled, deserialized.RenderState.DepthTestEnabled);
            Assert.Equal(asset.RenderState.DepthWriteEnabled, deserialized.RenderState.DepthWriteEnabled);
            Assert.Equal(asset.ConstantBuffers.Length, deserialized.ConstantBuffers.Length);
            Assert.Equal(asset.ConstantBuffers[0].Name, deserialized.ConstantBuffers[0].Name);
            Assert.Equal(asset.ConstantBuffers[0].Data, deserialized.ConstantBuffers[0].Data);
        }

        /// <summary>
        /// Ensures material assets serialized with an unsupported editor asset version are rejected.
        /// </summary>
        [Fact]
        public void AssetSerializer_MaterialAssetWithUnsupportedVersion_Throws() {
            MaterialAsset asset = CreateMaterialAsset();
            byte[] data = AssetSerializer.SerializeToBytes(asset);
            data[5] = (byte)(EditorAssetBinarySerializer.CurrentVersion + 1);

            Assert.Throws<InvalidOperationException>(() => AssetSerializer.DeserializeFromBytes(data));
        }

        /// <summary>
        /// Ensures model assets round-trip through the HELE asset serializer and emit the expected file magic.
        /// </summary>
        [Fact]
        public void AssetSerializer_ModelAsset_WritesHeleHeaderAndRoundTrips() {
            ModelAsset asset = CreateModelAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            ModelAsset deserialized = (ModelAsset)AssetSerializer.DeserializeFromBytes(data);
            EngineBinaryHeader header = ReadHeader(data);

            Assert.Equal((byte)'H', data[0]);
            Assert.Equal((byte)'E', data[1]);
            Assert.Equal((byte)'L', data[2]);
            Assert.Equal((byte)'E', data[3]);
            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.Positions, deserialized.Positions);
            Assert.Equal(asset.Normals, deserialized.Normals);
            Assert.Equal(asset.TexCoords, deserialized.TexCoords);
            Assert.Equal(asset.Indices16, deserialized.Indices16);
        }

        /// <summary>
        /// Ensures 32-bit indexed model assets round-trip through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_ModelAssetWith32BitIndices_RoundTrips() {
            ModelAsset asset = CreateModelAssetWith32BitIndices();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            ModelAsset deserialized = (ModelAsset)AssetSerializer.DeserializeFromBytes(data);
            EngineBinaryHeader header = ReadHeader(data);

            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.Positions, deserialized.Positions);
            Assert.Equal(asset.Normals, deserialized.Normals);
            Assert.Equal(asset.TexCoords, deserialized.TexCoords);
            Assert.Null(deserialized.Indices16);
            Assert.Equal(asset.Indices32, deserialized.Indices32);
        }

        /// <summary>
        /// Ensures nested shader assets round-trip through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_ShaderAsset_RoundTripsNestedPayloads() {
            ShaderAsset asset = CreateShaderAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            ShaderAsset deserialized = (ShaderAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.Name, deserialized.Name);
            Assert.Equal(asset.TargetName, deserialized.TargetName);
            Assert.Single(deserialized.Programs);
            Assert.Single(deserialized.Binaries);
            Assert.Equal("ProgramMain", deserialized.Programs[0].Name);
            Assert.Equal("POSITION", deserialized.Programs[0].Inputs[0].Semantic);
            Assert.Equal("USE_FOG=1", deserialized.Programs[0].Variants[0].Defines[0]);
            Assert.Equal(new byte[] { 1, 3, 3, 7 }, deserialized.Binaries[0].Bytecode);
        }

        /// <summary>
        /// Ensures invalid asset payload headers are rejected.
        /// </summary>
        [Fact]
        public void AssetSerializer_Deserialize_WithInvalidHeader_Throws() {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("legacy-asset");

            Assert.Throws<InvalidOperationException>(() => AssetSerializer.DeserializeFromBytes(data));
        }

        /// <summary>
        /// Ensures asset import settings round-trip through the custom binary serializer and emit the expected header.
        /// </summary>
        [Fact]
        public void AssetImportSettingsBinarySerializer_WritesExpectedHeaderAndRoundTrips() {
            AssetImportSettings settings = CreateAssetImportSettings();

            using MemoryStream stream = new MemoryStream();
            AssetImportSettingsBinarySerializer.Serialize(stream, settings);
            byte[] data = stream.ToArray();
            EngineBinaryHeader header = ReadHeader(data);
            stream.Position = 0;
            AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)AssetImportSettingsBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)AssetImportSettingsBinarySerializer.ValueKind, header.ValueKind);
            Assert.Equal(AssetImportSettingsBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(settings.Importer.ImporterId, deserialized.Importer.ImporterId);
            Assert.Equal(settings.Importer.SourceChecksum, deserialized.Importer.SourceChecksum);
            Assert.Equal(settings.Importer.AssetId, deserialized.Importer.AssetId);
            Assert.True(deserialized.Processor.Platforms.ContainsKey("windows"));
            Assert.True(deserialized.Processor.Platforms["windows"].Model.FlipWinding);
            Assert.True(deserialized.Processor.Platforms.ContainsKey("android"));
            Assert.False(deserialized.Processor.Platforms["android"].Model.FlipWinding);
        }

        /// <summary>
        /// Ensures invalid import-settings payload headers are rejected.
        /// </summary>
        [Fact]
        public void AssetImportSettingsBinarySerializer_Deserialize_WithInvalidHeader_Throws() {
            using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("legacy-settings"));

            Assert.Throws<InvalidOperationException>(() => AssetImportSettingsBinarySerializer.Deserialize(stream));
        }

        /// <summary>
        /// Ensures the editor content manager can load serialized scene assets through the registered processor.
        /// </summary>
        [Fact]
        public void ContentManager_SceneAsset_RoundTripsSerializedFile() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/BrowserTest.helen",
                RootEntities = Array.Empty<SceneEntityAsset>()
            };
            string scenePath = Path.Combine(TempRootPath, "BrowserTest.helen");
            ContentManager contentManager = new ContentManager(TempRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SceneAsset loaded = contentManager.Load<SceneAsset>(scenePath);

            Assert.Equal("Scenes/BrowserTest.helen", loaded.Id);
        }

        /// <summary>
        /// Ensures the editor content manager can load serialized asset import settings through the registered processor.
        /// </summary>
        [Fact]
        public void ContentManager_AssetImportSettings_RoundTripsSerializedFile() {
            AssetImportSettings settings = CreateAssetImportSettings();
            string settingsPath = Path.Combine(TempRootPath, "test.hasset");
            ContentManager contentManager = new ContentManager(TempRootPath);
            EditorContentManagerConfiguration.ConfigureProjectContentManager(contentManager);

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetImportSettingsBinarySerializer.Serialize(stream, settings);
            }

            AssetImportSettings loadedSettings = contentManager.Load<AssetImportSettings>(settingsPath);

            Assert.Equal(settings.Importer.ImporterId, loadedSettings.Importer.ImporterId);
            Assert.Equal(settings.Importer.SourceChecksum, loadedSettings.Importer.SourceChecksum);
            Assert.Equal(settings.Importer.AssetId, loadedSettings.Importer.AssetId);
            Assert.True(loadedSettings.Processor.Platforms["windows"].Model.FlipWinding);
        }

        /// <summary>
        /// Ensures unsupported older asset-import-settings versions are rejected by the serializer.
        /// </summary>
        [Fact]
        public void AssetImportSettingsBinarySerializer_Deserialize_WithOlderVersion_Throws() {
            AssetImportSettings settings = CreateAssetImportSettings();
            byte[] data;

            using (MemoryStream stream = new MemoryStream()) {
                AssetImportSettingsBinarySerializer.Serialize(stream, settings);
                data = stream.ToArray();
            }

            data[5] = 1;

            using MemoryStream deserializeStream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => AssetImportSettingsBinarySerializer.Deserialize(deserializeStream));

            Assert.Contains("Unsupported asset import settings binary version", exception.Message);
        }

        /// <summary>
        /// Ensures shader cache metadata round-trips through the custom binary serializer and emits the expected header.
        /// </summary>
        [Fact]
        public void ShaderCacheMetadataBinarySerializer_WritesExpectedHeaderAndRoundTrips() {
            ShaderCacheMetadata metadata = CreateShaderCacheMetadata();

            using MemoryStream stream = new MemoryStream();
            ShaderCacheMetadataBinarySerializer.Serialize(stream, metadata);
            byte[] data = stream.ToArray();
            EngineBinaryHeader header = ReadHeader(data);
            stream.Position = 0;
            ShaderCacheMetadata deserialized = ShaderCacheMetadataBinarySerializer.Deserialize(stream);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)ShaderCacheMetadataBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)ShaderCacheMetadataBinarySerializer.ValueKind, header.ValueKind);
            Assert.Equal(ShaderCacheMetadataBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(metadata.SourceHash, deserialized.SourceHash);
            Assert.Equal(metadata.SourceWriteTimeUtcTicks, deserialized.SourceWriteTimeUtcTicks);
            Assert.Equal(metadata.SourceLengthBytes, deserialized.SourceLengthBytes);
        }

        /// <summary>
        /// Ensures the file-backed shader cache metadata store saves, loads, and deletes HELE metadata correctly.
        /// </summary>
        [Fact]
        public void ShaderCacheMetadataStore_SaveLoadDelete_RoundTripsMetadata() {
            ShaderCacheMetadata metadata = CreateShaderCacheMetadata();
            ShaderCacheMetadataStore store = new ShaderCacheMetadataStore(TempRootPath, ShaderCompileTarget.DirectX11);

            store.Save("testShader", metadata);
            bool loaded = store.TryLoad("testShader", out ShaderCacheMetadata loadedMetadata);
            store.Delete("testShader");
            bool existsAfterDelete = store.TryLoad("testShader", out ShaderCacheMetadata deletedMetadata);

            Assert.True(loaded);
            Assert.Equal(metadata.SourceHash, loadedMetadata.SourceHash);
            Assert.Equal(metadata.SourceWriteTimeUtcTicks, loadedMetadata.SourceWriteTimeUtcTicks);
            Assert.Equal(metadata.SourceLengthBytes, loadedMetadata.SourceLengthBytes);
            Assert.False(existsAfterDelete);
            Assert.Null(deletedMetadata);
        }

        /// <summary>
        /// Ensures invalid shader metadata files are rejected.
        /// </summary>
        [Fact]
        public void ShaderCacheMetadataStore_TryLoad_WithInvalidMetadata_Throws() {
            ShaderCacheMetadataStore store = new ShaderCacheMetadataStore(TempRootPath, ShaderCompileTarget.DirectX11);
            string metadataPath = ShaderPackagePaths.GetMetadataPath(TempRootPath, "legacyShader", ShaderCompileTarget.DirectX11);
            File.WriteAllText(metadataPath, "legacy-metadata");

            Assert.Throws<InvalidOperationException>(() => store.TryLoad("legacyShader", out _));
            Assert.True(File.Exists(metadataPath));
        }

        /// <summary>
        /// Reads the standardized HELE header from a serialized byte buffer.
        /// </summary>
        /// <param name="data">Serialized byte buffer to inspect.</param>
        /// <returns>Decoded header metadata.</returns>
        static EngineBinaryHeader ReadHeader(byte[] data) {
            using MemoryStream stream = new MemoryStream(data, false);
            return EngineBinaryHeaderSerializer.Read(stream);
        }

        /// <summary>
        /// Creates a representative texture asset for serializer testing.
        /// </summary>
        /// <returns>Texture asset with sample image data.</returns>
        static TextureAsset CreateTextureAsset() {
            return new TextureAsset {
                Id = "texture/test",
                Width = 2,
                Height = 2,
                Colors = new byte[] {
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                    0, 0, 255, 255,
                    255, 255, 255, 255
                }
            };
        }

        /// <summary>
        /// Creates a representative text asset for serializer testing.
        /// </summary>
        /// <returns>Text asset with multiline sample content.</returns>
        static TextAsset CreateTextAsset() {
            return new TextAsset {
                Id = "text/test",
                Text = "line one\nline two\nline three"
            };
        }

        /// <summary>
        /// Creates a representative material asset for serializer testing.
        /// </summary>
        /// <returns>Material asset with shader references.</returns>
        static MaterialAsset CreateMaterialAsset() {
            return new MaterialAsset {
                Id = "material/test",
                ShaderAssetId = "shader/test",
                VertexProgram = "ProgramMain",
                PixelProgram = "ProgramPixel",
                Variant = "Default",
                RenderState = new MaterialRenderState {
                    BlendMode = MaterialBlendMode.AlphaBlend,
                    CullMode = MaterialCullMode.None,
                    DepthTestEnabled = true,
                    DepthWriteEnabled = false
                },
                ConstantBuffers = new[] {
                    new MaterialConstantBufferAsset {
                        Name = "MaterialParams",
                        Data = new byte[] { 9, 8, 7, 6 }
                    }
                }
            };
        }

        /// <summary>
        /// Creates a representative model asset for serializer testing.
        /// </summary>
        /// <returns>Model asset with sample mesh data.</returns>
        static ModelAsset CreateModelAsset() {
            return new ModelAsset {
                Id = "model/test",
                Positions = new[] {
                    new float3(1f, 2f, 3f),
                    new float3(4f, 5f, 6f)
                },
                Normals = new[] {
                    new float3(0f, 1f, 0f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 0 }
            };
        }

        /// <summary>
        /// Creates a representative 32-bit indexed model asset for serializer testing.
        /// </summary>
        /// <returns>Model asset with sample 32-bit mesh data.</returns>
        static ModelAsset CreateModelAssetWith32BitIndices() {
            return new ModelAsset {
                Id = "model/test32",
                Positions = new[] {
                    new float3(1f, 2f, 3f),
                    new float3(4f, 5f, 6f),
                    new float3(7f, 8f, 9f)
                },
                Normals = new[] {
                    new float3(0f, 1f, 0f),
                    new float3(0f, 0f, 1f),
                    new float3(1f, 0f, 0f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 1f),
                    new float2(2f, 2f)
                },
                Indices32 = new uint[] { 0u, 1u, 2u }
            };
        }

        /// <summary>
        /// Creates a representative shader asset for serializer testing.
        /// </summary>
        /// <returns>Shader asset with nested program and binary data.</returns>
        static ShaderAsset CreateShaderAsset() {
            return new ShaderAsset {
                Id = "shader/test",
                Name = "shader/test",
                TargetName = "dx11",
                Programs = new[] {
                    new ShaderProgramAsset {
                        Name = "ProgramMain",
                        Stage = ShaderStage.Vertex,
                        EntryPoint = "VSMain",
                        Bindings = new[] {
                            new ShaderBindingAsset {
                                Name = "Globals",
                                Type = ShaderResourceType.ConstantBuffer,
                                Set = 0,
                                Slot = 1,
                                Size = 64,
                                Members = new[] {
                                    new ShaderConstantMemberAsset {
                                        Name = "WorldViewProj",
                                        Type = "float4x4",
                                        Offset = 0,
                                        Size = 64
                                    }
                                }
                            }
                        },
                        Inputs = new[] {
                            new ShaderVertexElementAsset {
                                Semantic = "POSITION",
                                Index = 0,
                                Format = "float3"
                            }
                        },
                        Outputs = new[] {
                            new ShaderVertexElementAsset {
                                Semantic = "SV_POSITION",
                                Index = 0,
                                Format = "float4"
                            }
                        },
                        Variants = new[] {
                            new ShaderVariantAsset {
                                Name = "Default",
                                Defines = new[] { "USE_FOG=1" }
                            }
                        }
                    }
                },
                Binaries = new[] {
                    new ShaderBinaryAsset {
                        ProgramName = "ProgramMain",
                        Stage = ShaderStage.Vertex,
                        TargetName = "dx11",
                        Variant = "Default",
                        Bytecode = new byte[] { 1, 3, 3, 7 }
                    }
                }
            };
        }

        /// <summary>
        /// Creates representative asset import settings for serializer testing.
        /// </summary>
        /// <returns>Asset import settings with sample values.</returns>
        static AssetImportSettings CreateAssetImportSettings() {
            return new AssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "model/obj",
                    SourceChecksum = "abc123",
                    AssetId = "asset-001"
                },
                Processor = new AssetProcessorSettings {
                    Platforms = new Dictionary<string, AssetPlatformProcessorSettings> {
                        ["windows"] = new AssetPlatformProcessorSettings {
                            Model = new ModelAssetProcessorSettings {
                                FlipWinding = true
                            }
                        },
                        ["android"] = new AssetPlatformProcessorSettings {
                            Model = new ModelAssetProcessorSettings {
                                FlipWinding = false
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates representative shader cache metadata for serializer testing.
        /// </summary>
        /// <returns>Shader cache metadata with sample values.</returns>
        static ShaderCacheMetadata CreateShaderCacheMetadata() {
            return new ShaderCacheMetadata {
                SourceHash = "shader-hash",
                SourceWriteTimeUtcTicks = 123456789,
                SourceLengthBytes = 2048
            };
        }
    }
}
