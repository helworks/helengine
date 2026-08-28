using helengine.directx11;
using Xunit;
using helengine.editor.tests.testing;

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
        /// Ensures material common-settings bytes do not depend on dictionary insertion order.
        /// </summary>
        [Fact]
        public void MaterialAssetCommonSettingsSerializer_WhenDictionariesAreInsertedInReverseOrder_IsDeterministic() {
            MaterialAssetCommonSettingsDocument first = CreateMaterialCommonSettingsDocument(
                new[] { "roughness", "base-color" },
                new[] { "normal", "albedo" });
            MaterialAssetCommonSettingsDocument second = CreateMaterialCommonSettingsDocument(
                new[] { "base-color", "roughness" },
                new[] { "albedo", "normal" });

            Assert.Equal(SerializeMaterialCommonSettings(first), SerializeMaterialCommonSettings(second));
        }

        /// <summary>
        /// Ensures material embedded former identities are ordered independently of caller insertion order.
        /// </summary>
        [Fact]
        public void MaterialAssetCommonSettingsSerializer_WhenFormerIdentitiesAreInsertedInReverseOrder_IsDeterministic() {
            MaterialAssetCommonSettingsDocument first = CreateMaterialCommonSettingsDocument(
                new[] { "roughness" },
                new[] { "albedo" });
            first.FormerAuthoringAssetIds = new List<string> {
                "ffeeddccbbaa99887766554433221100",
                "11112222333344445555666677778888"
            };
            MaterialAssetCommonSettingsDocument second = CreateMaterialCommonSettingsDocument(
                new[] { "roughness" },
                new[] { "albedo" });
            second.FormerAuthoringAssetIds = first.FormerAuthoringAssetIds.AsEnumerable().Reverse().ToList();

            Assert.Equal(SerializeMaterialCommonSettings(first), SerializeMaterialCommonSettings(second));
        }

        /// <summary>
        /// Ensures the native scene serializer orders unordered asset references by their stable key.
        /// </summary>
        [Fact]
        public void SceneAssetSerializer_WhenReferencesAreInsertedInReverseOrder_IsDeterministic() {
            SceneAsset first = CreateSceneWithReferences(new[] { "Models/Z.hasset", "Models/A.hasset" });
            SceneAsset second = CreateSceneWithReferences(new[] { "Models/A.hasset", "Models/Z.hasset" });

            Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
        }

        /// <summary>
        /// Ensures former embedded identities are serialized in ordinal order.
        /// </summary>
        [Fact]
        public void AssetSerializer_WhenFormerIdentitiesAreInsertedInReverseOrder_IsDeterministic() {
            ModelAsset first = new ModelAsset {
                Id = "Models/FormerOrder",
                AuthoringAssetId = "00112233445566778899aabbccddeeff",
                FormerAuthoringAssetIds = new[] { "ffeeddccbbaa99887766554433221100", "11112222333344445555666677778888" }
            };
            ModelAsset second = new ModelAsset {
                Id = first.Id,
                AuthoringAssetId = first.AuthoringAssetId,
                FormerAuthoringAssetIds = first.FormerAuthoringAssetIds.Reverse().ToArray()
            };

            Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
        }

        /// <summary>
        /// Ensures reference ordering remains deterministic when the primary path key ties.
        /// </summary>
        [Fact]
        public void SceneAssetSerializer_WhenReferencePrimaryKeysTie_IsDeterministic() {
            const string relativePath = "Textures/Tied.png";
            SceneAssetReference firstReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "00112233445566778899aabbccddeeff", relativePath,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            SceneAssetReference secondReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "ffeeddccbbaa99887766554433221100", relativePath,
                "sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");
            SceneAsset first = CreateSceneWithReferenceObjects(new[] { firstReference, secondReference });
            SceneAsset second = CreateSceneWithReferenceObjects(new[] { secondReference, firstReference });

            Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
        }

        /// <summary>
        /// Ensures blueprint references use the same total ordering as scene references.
        /// </summary>
        [Fact]
        public void BlueprintAssetSerializer_WhenReferencePrimaryKeysTie_IsDeterministic() {
            const string relativePath = "Textures/Tied.png";
            SceneAssetReference firstReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "00112233445566778899aabbccddeeff", relativePath,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            SceneAssetReference secondReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "ffeeddccbbaa99887766554433221100", relativePath,
                "sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");
            BlueprintAsset first = CreateBlueprintWithReferenceObjects(new[] { firstReference, secondReference });
            BlueprintAsset second = CreateBlueprintWithReferenceObjects(new[] { secondReference, firstReference });

            Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
        }

        /// <summary>
        /// Ensures animation platform overrides are ordered by platform and nested environment.
        /// </summary>
        [Fact]
        public void AssetSerializer_AnimationPlatformOverrides_WhenInsertedInReverseOrder_IsDeterministic() {
            AnimationClipAsset first = CreateAnimationWithOverrides(new[] {
                new AnimationClipPlatformOverrideAsset { PlatformId = "windows", EnvironmentId = "shipping" },
                new AnimationClipPlatformOverrideAsset { PlatformId = "android", EnvironmentId = "debug" },
                new AnimationClipPlatformOverrideAsset { PlatformId = "windows", EnvironmentId = "debug" }
            });
            AnimationClipAsset second = CreateAnimationWithOverrides(first.PlatformOverrides.Reverse().ToArray());

            Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
        }

        /// <summary>
        /// Ensures audio platform overrides are ordered by platform.
        /// </summary>
        [Fact]
        public void AssetSerializer_AudioPlatformOverrides_WhenInsertedInReverseOrder_IsDeterministic() {
            AudioAsset first = CreateAudioWithOverrides(new[] {
                new AudioAssetPlatformOverrideAsset { PlatformId = "windows", EncodingFamilyId = "pcm" },
                new AudioAssetPlatformOverrideAsset { PlatformId = "android", EncodingFamilyId = "opus" }
            });
            AudioAsset second = CreateAudioWithOverrides(first.PlatformOverrides.Reverse().ToArray());

            Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
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
                writer.WriteInt64(unchecked((long)0x1112131415161718UL));
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
            Assert.Equal(0x1112131415161718UL, unchecked((ulong)reader.ReadInt64()));
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
                writer.WriteInt64(unchecked((long)0x1112131415161718UL));
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
            Assert.Equal(0x1112131415161718UL, unchecked((ulong)reader.ReadInt64()));
            Assert.Equal(0x0102030405060708L, reader.ReadInt64());
            Assert.Equal(1.5f, reader.ReadSingle());
            Assert.Equal("AB", reader.ReadString());
            Assert.Equal(new byte[] { 9, 8, 7 }, reader.ReadByteArray());
        }

        /// <summary>
        /// Ensures null serialized strings normalize to empty strings so native builds never materialize invalid `std::string` values.
        /// </summary>
        [Fact]
        public void EngineBinaryReader_ReadString_WhenPayloadIsNull_ReturnsEmptyString() {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriterLE writer = new BinaryWriterLE(stream)) {
                writer.WriteString(null);
            }

            stream.Position = 0;

            using BinaryReaderLE reader = new BinaryReaderLE(stream);
            Assert.Equal(string.Empty, reader.ReadString());
        }

        /// <summary>
        /// Ensures each decoded empty array has an independent caller-owned lifetime for generated native cleanup.
        /// </summary>
        [Fact]
        public void EngineBinaryReader_ReadArray_WhenPayloadsAreEmpty_ReturnsDistinctArrays() {
            using MemoryStream stream = new MemoryStream(new byte[8]);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);

            int[] first = reader.ReadArray(binaryReader => binaryReader.ReadInt32());
            int[] second = reader.ReadArray(binaryReader => binaryReader.ReadInt32());

            Assert.Empty(first);
            Assert.Empty(second);
            Assert.NotSame(first, second);
        }

        /// <summary>
        /// Ensures each decoded empty byte payload has an independent caller-owned lifetime for generated native cleanup.
        /// </summary>
        [Fact]
        public void EngineBinaryReader_ReadByteArray_WhenPayloadsAreEmpty_ReturnsDistinctArrays() {
            using MemoryStream stream = new MemoryStream(new byte[8]);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);

            byte[] first = reader.ReadByteArray();
            byte[] second = reader.ReadByteArray();

            Assert.Empty(first);
            Assert.Empty(second);
            Assert.NotSame(first, second);
        }

        /// <summary>
        /// Ensures scene assets round-trip through the HELE asset serializer and emit the expected file header.
        /// </summary>
        [Fact]
        public void AssetSerializer_SceneAsset_WritesHeleHeaderAndRoundTrips() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/TestScene.helen",
                Physics3DSceneFeatureFlags = 1234u,
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile {
                        Width = 1920,
                        Height = 1080
                    }
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LayerMask = 0x2222,
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
                                Id = 2u,
                                Name = "Child",
                                LayerMask = 0x4444,
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
            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal("Scenes/TestScene.helen", deserialized.Id);
            Assert.Equal(1234u, deserialized.Physics3DSceneFeatureFlags);
            Assert.Equal(1920, deserialized.SceneSettings.CanvasProfile.Width);
            Assert.Equal(1080, deserialized.SceneSettings.CanvasProfile.Height);
            Assert.Single(deserialized.RootEntities);
            Assert.Equal(1u, deserialized.RootEntities[0].Id);
            Assert.Equal((ushort)0x2222, deserialized.RootEntities[0].LayerMask);
            Assert.Equal(new float3(1f, 2f, 3f), deserialized.RootEntities[0].LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), deserialized.RootEntities[0].LocalScale);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, deserialized.RootEntities[0].Components[0].Payload);
            Assert.Equal(2u, deserialized.RootEntities[0].Children[0].Id);
            Assert.Equal("Child", deserialized.RootEntities[0].Children[0].Name);
            Assert.Equal((ushort)0x4444, deserialized.RootEntities[0].Children[0].LayerMask);
        }

        /// <summary>
        /// Ensures current editor scene payloads round-trip embedded identity and the five-field file reference.
        /// </summary>
        [Fact]
        public void AssetSerializer_SceneAsset_WithCanonicalFileReference_RoundTripsCurrentPayload() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/Identity.helen",
                AuthoringAssetId = "ffeeddccbbaa99887766554433221100",
                AssetReferences = new[] {
                    global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                        "00112233445566778899aabbccddeeff",
                        "Textures/Shared.png",
                        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")
                },
                RootEntities = Array.Empty<SceneEntityAsset>()
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            SceneAsset deserialized = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(data));
            SceneAssetReferenceTestFactoryAssertCanonicalReference(deserialized.AssetReferences.Single());

            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.AuthoringAssetId, deserialized.AuthoringAssetId);
        }

        /// <summary>Ensures current authored scene arrays reject packaged path-only references.</summary>
        [Fact]
        public void AssetSerializer_SceneAsset_WithPathOnlyFileReference_RejectsCurrentPayloadOnRead() {
            SceneAsset scene = new SceneAsset {
                Id = "Scene",
                RootEntities = Array.Empty<SceneEntityAsset>(),
                AssetReferences = new[] {
                    global::helengine.SceneAssetReferenceFactory.CreateFileSystemMaterial("materials/test.hasset")
                }
            };
            using MemoryStream stream = new MemoryStream();
            AssetSerializer.Serialize(stream, scene);
            stream.Position = 0;

            Assert.Throws<ArgumentException>(() => AssetSerializer.Deserialize(stream));
        }

        /// <summary>
        /// Verifies the canonical reference values without deriving expectations from the serializer implementation.
        /// </summary>
        static void SceneAssetReferenceTestFactoryAssertCanonicalReference(SceneAssetReference reference) {
            Assert.Equal("00112233445566778899aabbccddeeff", reference.AssetId);
            Assert.Equal("Textures/Shared.png", reference.RelativePath);
            Assert.Equal("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", reference.ContentHash);
        }

        /// <summary>
        /// Ensures scene assets round-trip the version-five physics feature flags through the editor asset serializer.
        /// </summary>
        [Fact]
        public void SerializeSceneAsset_WhenPhysicsFlagsArePresent_RoundTripsVersionFivePayload() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "scene-id",
                Physics3DSceneFeatureFlags = 1234u,
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile {
                        Width = 1600,
                        Height = 900
                    }
                },
                RootEntities = Array.Empty<SceneEntityAsset>()
            };

            using MemoryStream stream = new MemoryStream();
            global::helengine.files.EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
            stream.Position = 0;

            SceneAsset deserialized = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            Assert.Equal(1234u, deserialized.Physics3DSceneFeatureFlags);
            Assert.Equal(1600, deserialized.SceneSettings.CanvasProfile.Width);
            Assert.Equal(900, deserialized.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Ensures scene assets round-trip the dont-unload scene setting through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_SceneAsset_WhenDontUnloadIsTrue_RoundTripsSceneSettingsFlag() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/Persistent.helen",
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile {
                        Width = 1920,
                        Height = 1080
                    },
                    DontUnload = true
                },
                RootEntities = Array.Empty<SceneEntityAsset>()
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            SceneAsset deserialized = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(data));

            Assert.True(deserialized.SceneSettings.DontUnload);
        }

        /// <summary>
        /// Ensures scene assets round-trip per-platform entity existence overrides through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_SceneAsset_WhenEntityUsesPlatformExistenceOverride_RoundTripsValues() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/PlatformExistence.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 7u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        PlatformExistenceOverrides = new[] {
                            new SceneEntityPlatformExistenceOverrideAsset {
                                PlatformId = "Windows",
                                Exists = true
                            },
                            new SceneEntityPlatformExistenceOverrideAsset {
                                PlatformId = "Nintendo3DS",
                                Exists = false
                            }
                        },
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            SceneAsset deserialized = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(data));

            SceneEntityAsset rootEntity = Assert.Single(deserialized.RootEntities);
            Assert.Collection(
                rootEntity.PlatformExistenceOverrides,
                nintendo3DsOverride => {
                    Assert.Equal("Nintendo3DS", nintendo3DsOverride.PlatformId);
                    Assert.False(nintendo3DsOverride.Exists);
                },
                windowsOverride => {
                    Assert.Equal("Windows", windowsOverride.PlatformId);
                    Assert.True(windowsOverride.Exists);
                });
        }

        /// <summary>
        /// Ensures authored asset payloads reject both older and newer format versions with regeneration guidance.
        /// </summary>
        [Fact]
        public void Deserialize_WhenAuthoredAssetVersionIsNotCurrent_ThrowsRegenerationGuidance() {
            AssertUnsupportedEditorAssetVersion((byte)(EditorAssetBinarySerializer.CurrentVersion - 1));
            AssertUnsupportedEditorAssetVersion((byte)(EditorAssetBinarySerializer.CurrentVersion + 1));
        }

        /// <summary>
        /// Builds an authored asset header with an unsupported version and verifies the exact rejection contract.
        /// </summary>
        static void AssertUnsupportedEditorAssetVersion(byte version) {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                version,
                EditorAssetBinarySerializer.FormatId,
                (ushort)EditorAssetBinarySerializer.RecordKind,
                (ushort)EditorAssetBinaryValueKind.SceneAsset);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Deserialize(stream));
            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(EditorAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies one packaged scene emitted by the current scene packager still deserializes cleanly in managed code.
        /// </summary>
        [Fact]
        public void DeserializePackagedSceneAsset_FromCurrentPackagerOutput_Succeeds() {
            string sceneId = "Scenes/TestPackagedScene.helen";
            string scenePath = Path.Combine(TempRootPath, "assets", "Scenes", "TestPackagedScene.helen");
            string buildRootPath = Path.Combine(TempRootPath, "build");
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
            Directory.CreateDirectory(buildRootPath);
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());

            SceneAsset authoredScene = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "PackagedRoot",
                        LocalPosition = new float3(1f, 2f, 3f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
            using (FileStream authoredStream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                global::helengine.files.EditorAssetBinarySerializer.Serialize(authoredStream, authoredScene);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                TempRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                PackagedFontAssetFactory.Create());
            packager.Package(new[] { sceneId }, buildRootPath);

            string packagedScenePath = Path.Combine(buildRootPath, "cooked", "scenes", "TestPackagedScene.hasset");
            using FileStream stream = File.OpenRead(packagedScenePath);
            SceneAsset scene = global::helengine.PackagedAssetBinarySerializer.DeserializeSceneAsset(stream);

            SceneEntityAsset rootEntity = Assert.Single(scene.RootEntities);
            Assert.Equal(1u, rootEntity.Id);
            Assert.Equal("PackagedRoot", rootEntity.Name);
        }

        /// <summary>
        /// Ensures the packaged runtime asset serializer can still deserialize cooked scene payloads emitted by the editor-side asset serializer.
        /// </summary>
        [Fact]
        public void RuntimeAssetSerializer_WhenGivenPackagedScenePayload_DeserializesSceneAsset() {
            SceneAsset asset = new SceneAsset {
                Id = "Scenes/RuntimePackagedScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 5u,
                        Name = "RuntimeRoot",
                        LocalPosition = new float3(4f, 5f, 6f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };

            byte[] data = global::helengine.files.AssetSerializer.SerializeToBytes(asset);
            SceneAsset deserialized = Assert.IsType<SceneAsset>(global::helengine.AssetSerializer.DeserializeFromBytes(data));

            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(5u, Assert.Single(deserialized.RootEntities).Id);
            Assert.Equal("RuntimeRoot", deserialized.RootEntities[0].Name);
        }

        /// <summary>
        /// Ensures every generic runtime asset reader consumes the embedded v24 authoring identity prefix.
        /// </summary>
        [Fact]
        public void RuntimeAssetSerializer_WhenGivenCurrentEditorPayloads_ReadsEverySupportedAssetKind() {
            Asset[] assets = {
                new TextureAsset { Id = "Texture", Colors = Array.Empty<byte>() },
                new ModelAsset { Id = "Model" },
                new TextAsset { Id = "Text", Text = "payload" },
                new MaterialAsset { Id = "Material" },
                new PlatformMaterialAsset { Id = "PlatformMaterial", RendererFamilyId = string.Empty, TextureRelativePath = string.Empty },
                new AnimationClipAsset { Id = "Animation" },
                new AudioAsset { Id = "Audio" },
                new SceneAsset { Id = "Scene", RootEntities = Array.Empty<SceneEntityAsset>(), AssetReferences = Array.Empty<SceneAssetReference>() }
            };

            for (int index = 0; index < assets.Length; index++) {
                assets[index].AuthoringAssetId = "00112233445566778899aabbccddeeff";
                assets[index].FormerAuthoringAssetIds = ["ffeeddccbbaa99887766554433221100"];
                byte[] data = global::helengine.files.AssetSerializer.SerializeToBytes(assets[index]);

                Asset deserialized = global::helengine.AssetSerializer.DeserializeFromBytes(data);

                Assert.Equal(assets[index].GetType(), deserialized.GetType());
                Assert.Equal(assets[index].AuthoringAssetId, deserialized.AuthoringAssetId);
                Assert.Equal(assets[index].FormerAuthoringAssetIds, deserialized.FormerAuthoringAssetIds);
            }
        }

        /// <summary>Ensures the dedicated runtime shader reader consumes embedded v24 identity fields.</summary>
        [Fact]
        public void ShaderAssetBinarySerializer_WhenGivenCurrentEditorPayload_ReadsEmbeddedIdentity() {
            ShaderAsset asset = new ShaderAsset {
                Id = "Shader",
                AuthoringAssetId = "00112233445566778899aabbccddeeff",
                FormerAuthoringAssetIds = ["ffeeddccbbaa99887766554433221100"],
                Name = "Shader",
                TargetName = "directx11",
                Programs = Array.Empty<ShaderProgramAsset>(),
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };
            byte[] data = global::helengine.files.AssetSerializer.SerializeToBytes(asset);
            using MemoryStream stream = new MemoryStream(data);

            ShaderAsset deserialized = ShaderAssetBinarySerializer.Deserialize(stream);

            Assert.Equal(asset.AuthoringAssetId, deserialized.AuthoringAssetId);
            Assert.Equal(asset.FormerAuthoringAssetIds, deserialized.FormerAuthoringAssetIds);
        }

        /// <summary>
        /// Ensures the dedicated shader reader rejects both adjacent header versions instead of interpreting another payload layout.
        /// </summary>
        [Fact]
        public void ShaderAssetBinarySerializer_WhenHeaderVersionIsNotCurrent_ThrowsRegenerationGuidance() {
            foreach (byte version in new[] {
                (byte)(PackagedAssetBinarySerializer.CurrentVersion - 1),
                (byte)(PackagedAssetBinarySerializer.CurrentVersion + 1)
            }) {
                byte[] data = global::helengine.files.AssetSerializer.SerializeToBytes(CreateShaderAsset());
                data[5] = version;
                using MemoryStream stream = new MemoryStream(data, writable: false);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ShaderAssetBinarySerializer.Deserialize(stream));
                Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
                Assert.Contains(PackagedAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
                Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Ensures the dedicated shader reader rejects a payload whose header record kind is not shader-owned.
        /// </summary>
        [Fact]
        public void ShaderAssetBinarySerializer_WhenHeaderRecordKindIsNotShader_ThrowsFormatError() {
            byte[] data = global::helengine.files.AssetSerializer.SerializeToBytes(CreateShaderAsset());
            data[8] = 0;
            data[9] = 0;
            using MemoryStream stream = new MemoryStream(data, writable: false);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ShaderAssetBinarySerializer.Deserialize(stream));

            Assert.Contains("record kind", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures the packaged runtime asset serializer rejects editor-only blueprint payloads instead of deserializing them at runtime.
        /// </summary>
        [Fact]
        public void RuntimeAssetSerializer_WhenGivenBlueprintPayload_ThrowsUnsupportedAssetValueKind() {
            BlueprintAsset asset = new BlueprintAsset {
                Id = "Blueprints/RuntimeReject.blueprint",
                RootEntity = new SceneEntityAsset {
                    Id = 11u,
                    Name = "BlueprintRoot",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };

            byte[] data = global::helengine.files.AssetSerializer.SerializeToBytes(asset);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => global::helengine.AssetSerializer.DeserializeFromBytes(data));

            Assert.Contains("Unsupported asset value kind", exception.Message);
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
            Assert.Equal(asset.RuntimeAssetId, deserialized.RuntimeAssetId);
            Assert.Equal(asset.Width, deserialized.Width);
            Assert.Equal(asset.Height, deserialized.Height);
            Assert.Equal(asset.ColorFormat, deserialized.ColorFormat);
            Assert.Equal(asset.Colors, deserialized.Colors);
        }

        /// <summary>
        /// Ensures indexed texture assets preserve palette and alpha metadata through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_TextureAsset_WhenIndexed8_preservesPaletteAndAlphaPrecision() {
            TextureAsset asset = new TextureAsset {
                Id = "texture/indexed8",
                RuntimeAssetId = 0x1112131415161718UL,
                Width = 2,
                Height = 2,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                PaletteColors = new byte[] {
                    255, 0, 0, 255,
                    0, 255, 0, 128
                },
                Colors = new byte[] { 0, 1, 1, 0 }
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            TextureAsset deserialized = (TextureAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.RuntimeAssetId, deserialized.RuntimeAssetId);
            Assert.Equal(asset.Width, deserialized.Width);
            Assert.Equal(asset.Height, deserialized.Height);
            Assert.Equal(TextureAssetColorFormat.Indexed8, deserialized.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.A8, deserialized.AlphaPrecision);
            Assert.Equal(asset.PaletteColors, deserialized.PaletteColors);
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
            ShaderMaterialAsset asset = CreateMaterialAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            ShaderMaterialAsset deserialized = (ShaderMaterialAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(ShaderMaterialAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal(ShaderMaterialAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal(ShaderMaterialAssetBinarySerializer.ValueKind, header.ValueKind);
            Assert.Equal(ShaderMaterialAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.ShaderAssetId, deserialized.ShaderAssetId);
            Assert.Equal(asset.VertexProgram, deserialized.VertexProgram);
            Assert.Equal(asset.PixelProgram, deserialized.PixelProgram);
            Assert.Equal(asset.Variant, deserialized.Variant);
            Assert.Equal(asset.DiffuseTextureAssetId, deserialized.DiffuseTextureAssetId);
            Assert.Equal(ReadPublicStringField(asset, "RoughnessTextureAssetId"), ReadPublicStringField(deserialized, "RoughnessTextureAssetId"));
            Assert.Equal(asset.CastsShadows, deserialized.CastsShadows);
            Assert.Equal(asset.ReceivesShadows, deserialized.ReceivesShadows);
            Assert.Equal(asset.RenderState.BlendMode, deserialized.RenderState.BlendMode);
            Assert.Equal(asset.RenderState.CullMode, deserialized.RenderState.CullMode);
            Assert.Equal(asset.RenderState.DepthTestEnabled, deserialized.RenderState.DepthTestEnabled);
            Assert.Equal(asset.RenderState.DepthWriteEnabled, deserialized.RenderState.DepthWriteEnabled);
            Assert.Equal(asset.ConstantBuffers.Length, deserialized.ConstantBuffers.Length);
            Assert.Equal(asset.ConstantBuffers[0].Name, deserialized.ConstantBuffers[0].Name);
            Assert.Equal(asset.ConstantBuffers[0].Data, deserialized.ConstantBuffers[0].Data);
        }

        /// <summary>
        /// Ensures shader material assets preserve metallic and specular standard-material buffers through binary serialization.
        /// </summary>
        [Fact]
        public void Shader_material_binary_serializer_round_trips_metallic_and_specular_constant_buffers() {
            ShaderMaterialAsset asset = CreateMaterialAsset();
            asset.ConstantBuffers = new[] {
                new MaterialConstantBufferAsset {
                    Name = StandardMaterialMetallicDefaults.MetallicBufferName,
                    Data = StandardMaterialMetallicDefaults.CreateConstantBufferData(0.25f)
                },
                new MaterialConstantBufferAsset {
                    Name = StandardMaterialSpecularDefaults.SpecularBufferName,
                    Data = StandardMaterialSpecularDefaults.CreateConstantBufferData(0.75f)
                }
            };

            byte[] data = ShaderMaterialAssetBinarySerializer.SerializeToBytes(asset);

            using MemoryStream stream = new MemoryStream(data, writable: false);
            ShaderMaterialAsset deserialized = ShaderMaterialAssetBinarySerializer.Deserialize(stream);

            MaterialConstantBufferAsset metallicBuffer = Assert.Single(
                deserialized.ConstantBuffers,
                buffer => buffer.Name == StandardMaterialMetallicDefaults.MetallicBufferName);
            MaterialConstantBufferAsset specularBuffer = Assert.Single(
                deserialized.ConstantBuffers,
                buffer => buffer.Name == StandardMaterialSpecularDefaults.SpecularBufferName);

            Assert.Equal(StandardMaterialMetallicDefaults.CreateConstantBufferData(0.25f), metallicBuffer.Data);
            Assert.Equal(StandardMaterialSpecularDefaults.CreateConstantBufferData(0.75f), specularBuffer.Data);
        }

        /// <summary>
        /// Ensures shader material payloads with an older or newer header version are rejected with regeneration guidance.
        /// </summary>
        [Fact]
        public void ShaderMaterialAssetBinarySerializer_Deserialize_WhenHeaderVersionIsNotCurrent_ThrowsRegenerationGuidance() {
            AssertUnsupportedShaderMaterialVersion((byte)(ShaderMaterialAssetBinarySerializer.CurrentVersion - 1));
            AssertUnsupportedShaderMaterialVersion((byte)(ShaderMaterialAssetBinarySerializer.CurrentVersion + 1));
        }

        /// <summary>
        /// Builds a valid shader material header with an unsupported version and verifies the rejection contract.
        /// </summary>
        /// <param name="version">Version encoded in the header.</param>
        static void AssertUnsupportedShaderMaterialVersion(byte version) {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                version,
                ShaderMaterialAssetBinarySerializer.FormatId,
                ShaderMaterialAssetBinarySerializer.RecordKind,
                ShaderMaterialAssetBinarySerializer.ValueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ShaderMaterialAssetBinarySerializer.Deserialize(stream));
            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(ShaderMaterialAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures material assets serialized with an unsupported editor asset version are rejected.
        /// </summary>
        [Fact]
        public void AssetSerializer_MaterialAssetWithUnsupportedVersion_Throws() {
            ShaderMaterialAsset asset = CreateMaterialAsset();
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
        /// Ensures model assets preserve authored submesh metadata through the HELE asset serializer.
        /// </summary>
        [Fact]
        public void AssetSerializer_ModelAssetWithSubmeshes_RoundTrips() {
            ModelAsset asset = CreateModelAssetWithSubmeshes();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            ModelAsset deserialized = (ModelAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.NotNull(deserialized.Submeshes);
            Assert.Equal(2, deserialized.Submeshes.Length);
            Assert.Equal("Body", deserialized.Submeshes[0].MaterialSlotName);
            Assert.Equal(0, deserialized.Submeshes[0].IndexStart);
            Assert.Equal(3, deserialized.Submeshes[0].IndexCount);
            Assert.Equal("Trim", deserialized.Submeshes[1].MaterialSlotName);
            Assert.Equal(3, deserialized.Submeshes[1].IndexStart);
            Assert.Equal(3, deserialized.Submeshes[1].IndexCount);
        }

        /// <summary>
        /// Ensures model assets round-trip through the current HELE asset serializer version.
        /// </summary>
        [Fact]
        public void AssetSerializer_ModelAsset_RoundTripsCurrentVersion() {
            ModelAsset asset = CreateModelAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            ModelAsset deserialized = (ModelAsset)AssetSerializer.DeserializeFromBytes(data);
            EngineBinaryHeader header = ReadHeader(data);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.Positions.Length, deserialized.Positions.Length);
            Assert.Equal(asset.Indices16, deserialized.Indices16);
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
            byte[] data = System.Text.Encoding.UTF8.GetBytes("older-asset");

            Assert.Throws<InvalidOperationException>(() => AssetSerializer.DeserializeFromBytes(data));
        }

        /// <summary>
        /// Ensures asset import settings round-trip through the custom binary serializer and emit the expected header.
        /// </summary>
        [Fact]
        public void SectionedAssetImportSettingsBinarySerializer_WritesExpectedHeaderAndRoundTrips() {
            AssetImportSettings settings = CreateAssetImportSettings();

            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            byte[] data = stream.ToArray();
            EngineBinaryHeader header = ReadHeader(data);
            stream.Position = 0;
            AssetImportSettings deserialized = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)SectionedAssetImportSettingsBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)SectionedAssetImportSettingsBinarySerializer.ValueKind, header.ValueKind);
            Assert.Equal(SectionedAssetImportSettingsBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(settings.Importer.ImporterId, deserialized.Importer.ImporterId);
            Assert.Equal(settings.Importer.SourceChecksum, deserialized.Importer.SourceChecksum);
            Assert.Equal(settings.Importer.AssetId, deserialized.Importer.AssetId);
            Assert.True(deserialized.Processor.Platforms.ContainsKey("windows"));
            Assert.True(deserialized.Processor.Platforms["windows"].Model.FlipWinding);
            Assert.True(deserialized.Processor.Platforms.ContainsKey("android"));
            Assert.False(deserialized.Processor.Platforms["android"].Model.FlipWinding);
        }

        /// <summary>
        /// Ensures texture processor settings round-trip through the asset import settings serializer for each platform.
        /// </summary>
        [Fact]
        public void SectionedAssetImportSettingsBinarySerializer_RoundTripsTextureMaxResolutionPerPlatform() {
            AssetImportSettings settings = CreateAssetImportSettings();
            settings.Processor.Platforms["windows"].Texture = new TextureAssetProcessorSettings {
                MaxResolution = 512,
                ColorFormat = TextureAssetColorFormat.Rgba32
            };
            settings.Processor.Platforms["android"].Texture = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            };

            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            AssetImportSettings deserialized = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(512, deserialized.Processor.Platforms["windows"].Texture.MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Rgba32, deserialized.Processor.Platforms["windows"].Texture.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Opaque, deserialized.Processor.Platforms["windows"].Texture.AlphaPrecision);
            Assert.Equal(256, deserialized.Processor.Platforms["android"].Texture.MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Indexed8, deserialized.Processor.Platforms["android"].Texture.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.A8, deserialized.Processor.Platforms["android"].Texture.AlphaPrecision);
        }

        /// <summary>
        /// Ensures generic asset import settings preserve the selected indexing method for indexed texture formats.
        /// </summary>
        [Fact]
        public void SectionedAssetImportSettingsBinarySerializer_RoundTripsTextureIndexingMethodPerPlatform() {
            AssetImportSettings settings = CreateAssetImportSettings();
            settings.Processor.Platforms["android"].Texture = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            };

            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            AssetImportSettings deserialized = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(
                TextureAssetIndexingMethod.QuantizedIndexed.ToString(),
                deserialized.Processor.Platforms["android"].Texture.IndexingMethodId);
        }

        /// <summary>
        /// Ensures generic asset import settings preserve opaque platform-owned texture color-format identifiers.
        /// </summary>
        [Fact]
        public void SectionedAssetImportSettingsBinarySerializer_WhenGameCubeUsesOpaqueColorFormatId_PreservesThatFormat() {
            AssetImportSettings settings = CreateAssetImportSettings();
            settings.Processor.Platforms["gamecube"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 256,
                    ColorFormatId = "GxRgb5A3",
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                }
            };

            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            AssetImportSettings deserialized = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(256, deserialized.Processor.Platforms["gamecube"].Texture.MaxResolution);
            Assert.Equal("GxRgb5A3", deserialized.Processor.Platforms["gamecube"].Texture.ColorFormatId);
            Assert.Equal(TextureAssetAlphaPrecision.A8, deserialized.Processor.Platforms["gamecube"].Texture.AlphaPrecision);
        }

        /// <summary>
        /// Ensures invalid import-settings payload headers are rejected.
        /// </summary>
        [Fact]
        public void SectionedAssetImportSettingsBinarySerializer_Deserialize_WithInvalidHeader_Throws() {
            using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("older-settings"));

            Assert.Throws<InvalidOperationException>(() => SectionedAssetImportSettingsBinarySerializer.Deserialize(stream));
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
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(TempRootPath));
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SceneAsset loaded = contentManager.Load<SceneAsset>(scenePath);

            Assert.Equal("Scenes/BrowserTest.helen", loaded.Id);
        }

        /// <summary>
        /// Ensures typed content-load failures report the active asset path and read stage for diagnostics.
        /// </summary>
        [Fact]
        public void ContentManager_Load_WithWrongAssetType_IncludesPathAndReadStageInExceptionMessage() {
            TextureAsset asset = new TextureAsset {
                Id = "Textures/WrongType",
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            };
            string texturePath = Path.Combine(TempRootPath, "WrongType.hasset");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(TempRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);

            using (FileStream stream = new FileStream(texturePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => contentManager.Load<SceneAsset>(texturePath, RuntimeContentProcessorIds.SceneAsset));

            Assert.Contains("asset_path='", exception.Message);
            Assert.Contains(texturePath, exception.Message);
            Assert.Contains("read_stage='", exception.Message);
        }

        /// <summary>
        /// Ensures runtime scene loads use one direct binary scene deserializer instead of the generic asset cast path.
        /// </summary>
        [Fact]
        public void RuntimeContentManagerConfiguration_RegistersSceneAssetWithBinaryContentProcessor() {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(TempRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            var registrationsField = typeof(ContentManager).GetField("ProcessorRegistrationsById", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Expected ProcessorRegistrationsById field was not found.");
            var registrations = Assert.IsType<Dictionary<string, ContentProcessorRegistration>>(registrationsField.GetValue(contentManager));
            ContentProcessorRegistration registration = Assert.Single(registrations, pair => pair.Key == RuntimeContentProcessorIds.SceneAsset).Value;

            Assert.IsType<BinaryContentProcessor<SceneAsset>>(registration.Processor);
        }

        /// <summary>
        /// Ensures the editor content manager can load serialized asset import settings through the registered processor.
        /// </summary>
        [Fact]
        public void ContentManager_AssetImportSettings_RoundTripsSerializedFile() {
            AssetImportSettings settings = CreateAssetImportSettings();
            string settingsPath = Path.Combine(TempRootPath, "test.hasset");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(TempRootPath));
            EditorContentManagerConfiguration.ConfigureProjectContentManager(contentManager);

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
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
        public void SectionedAssetImportSettingsBinarySerializer_Deserialize_WithOlderVersion_Throws() {
            AssetImportSettings settings = CreateAssetImportSettings();
            byte[] data;

            using (MemoryStream stream = new MemoryStream()) {
                SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
                data = stream.ToArray();
            }

            data[5] = 2;

            using MemoryStream deserializeStream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SectionedAssetImportSettingsBinarySerializer.Deserialize(deserializeStream));

            Assert.Contains("Unsupported sectioned asset import settings binary version", exception.Message);
        }

        /// <summary>
        /// Ensures negative texture processor limits are rejected during asset import settings serialization.
        /// </summary>
        [Fact]
        public void SectionedAssetImportSettingsBinarySerializer_Serialize_WhenTextureMaxResolutionIsNegative_Throws() {
            AssetImportSettings settings = CreateAssetImportSettings();
            settings.Processor.Platforms["windows"].Texture = new TextureAssetProcessorSettings {
                MaxResolution = -1,
                ColorFormat = TextureAssetColorFormat.Rgba32
            };

            using MemoryStream stream = new MemoryStream();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings));
            Assert.Contains("Texture max resolution cannot be negative", exception.Message);
        }

        /// <summary>
        /// Ensures typed texture asset import settings round-trip through their dedicated serializer.
        /// </summary>
        [Fact]
        public void TextureAssetImportSettingsBinarySerializer_RoundTripsPlatformSettings() {
            TextureAssetImportSettings settings = CreateTextureAssetImportSettings();

            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            byte[] data = stream.ToArray();
            EngineBinaryHeader header = ReadHeader(data);
            stream.Position = 0;

            TextureAssetImportSettings deserialized = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)TextureAssetImportSettingsBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)AssetImportSettingsBinaryValueKind.TextureAssetImportSettings, header.ValueKind);
            Assert.Equal(TextureAssetImportSettingsBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal("pfim", deserialized.Importer.ImporterId);
            Assert.Equal(512, deserialized.Processor.Platforms["windows"].MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Rgba32, deserialized.Processor.Platforms["windows"].ColorFormat);
            Assert.Equal(128, deserialized.Processor.Platforms["android"].MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, deserialized.Processor.Platforms["android"].ColorFormat);
        }

        /// <summary>
        /// Ensures typed model asset import settings round-trip through their dedicated serializer.
        /// </summary>
        [Fact]
        public void ModelAssetImportSettingsBinarySerializer_RoundTripsPlatformSettings() {
            ModelAssetImportSettings settings = CreateModelAssetImportSettings();

            using MemoryStream stream = new MemoryStream();
            ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            byte[] data = stream.ToArray();
            EngineBinaryHeader header = ReadHeader(data);
            stream.Position = 0;

            ModelAssetImportSettings deserialized = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)ModelAssetImportSettingsBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings, header.ValueKind);
            Assert.Equal(ModelAssetImportSettingsBinarySerializer.CurrentVersion, header.Version);
            Assert.True(deserialized.Processor.Platforms["windows"].FlipWinding);
            Assert.True(deserialized.Processor.Platforms["windows"].Tessellate);
            Assert.Equal(0.25d, deserialized.Processor.Platforms["windows"].TessellationMaxEdgeLength);
            Assert.False(deserialized.Processor.Platforms["ps2"].FlipWinding);
            Assert.False(deserialized.Processor.Platforms["ps2"].Tessellate);
            Assert.Equal(1.0d, deserialized.Processor.Platforms["ps2"].TessellationMaxEdgeLength);
        }

        /// <summary>
        /// Ensures version-one model settings are rejected instead of defaulting missing tessellation fields.
        /// </summary>
        [Fact]
        public void ModelAssetImportSettingsBinarySerializer_DeserializeVersionOne_ThrowsRegenerationGuidance() {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                1,
                EditorAssetBinarySerializer.FormatId,
                (ushort)ModelAssetImportSettingsBinarySerializer.RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings);
            EngineBinaryHeaderSerializer.Write(stream, header);
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian, true)) {
                writer.WriteString("assimp");
                writer.WriteString("legacy-model-checksum");
                writer.WriteString("legacy-model-id");
                writer.WriteInt32(1);
                writer.WriteString("windows");
                writer.WriteByte(1);
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ModelAssetImportSettingsBinarySerializer.Deserialize(stream));

            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
            Assert.Contains(ModelAssetImportSettingsBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures obsolete generic model settings are rejected without conversion or rewriting.
        /// </summary>
        [Fact]
        public void LoadOrCreateModelImportSettings_WhenObsoleteGenericSidecarExists_ThrowsWithoutRewrite() {
            string sourcePath = Path.Combine(TempRootPath, "DemoDiscBody.obj");
            string settingsPath = sourcePath + ".hasset";
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });

            AssetImportSettings obsoleteSettings = CreateAssetImportSettings();
            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                SectionedAssetImportSettingsBinarySerializer.Serialize(stream, obsoleteSettings);
            }
            byte[] obsoleteData = File.ReadAllBytes(settingsPath);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(TempRootPath));
            AssetImportManager manager = new AssetImportManager(TempRootPath, contentManager);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => manager.LoadOrCreateModelImportSettings(sourcePath));

            Assert.Contains("obsolete", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(obsoleteData, File.ReadAllBytes(settingsPath));
        }

        /// <summary>
        /// Ensures typed material asset import settings round-trip through their dedicated serializer.
        /// </summary>
        [Fact]
        public void MaterialAssetImportSettingsBinarySerializer_RoundTripsSchemaAndFields() {
            MaterialAssetImportSettings settings = CreateMaterialAssetImportSettings();

            using MemoryStream stream = new MemoryStream();
            MaterialAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            byte[] data = stream.ToArray();
            EngineBinaryHeader header = ReadHeader(data);
            stream.Position = 0;

            MaterialAssetImportSettings deserialized = MaterialAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)MaterialAssetImportSettingsBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)AssetImportSettingsBinaryValueKind.MaterialAssetImportSettings, header.ValueKind);
            Assert.Equal(MaterialAssetImportSettingsBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal("standard-shader", deserialized.Processor.Platforms["windows"].SchemaId);
            Assert.Equal("#ffffffff", deserialized.Processor.Platforms["windows"].FieldValues["base-color"]);
            Assert.Equal("Textures/checker", deserialized.Processor.Platforms["windows"].FieldValues["texture-id"]);
        }

        /// <summary>
        /// Ensures typed texture settings reject every version other than the current layout and explain regeneration.
        /// </summary>
        [Theory]
        [InlineData((byte)(TextureAssetImportSettingsBinarySerializer.CurrentVersion - 1))]
        [InlineData((byte)(TextureAssetImportSettingsBinarySerializer.CurrentVersion + 1))]
        public void TextureAssetImportSettingsBinarySerializer_Deserialize_WhenVersionIsNotCurrent_ReportsRegeneration(byte version) {
            TextureAssetImportSettings settings = CreateTextureAssetImportSettings();
            byte[] data = SerializeTextureSettings(settings);
            data[5] = version;

            using MemoryStream stream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TextureAssetImportSettingsBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(TextureAssetImportSettingsBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures typed model settings reject every version other than the current layout and explain regeneration.
        /// </summary>
        [Theory]
        [InlineData((byte)(ModelAssetImportSettingsBinarySerializer.CurrentVersion - 1))]
        [InlineData((byte)(ModelAssetImportSettingsBinarySerializer.CurrentVersion + 1))]
        public void ModelAssetImportSettingsBinarySerializer_Deserialize_WhenVersionIsNotCurrent_ReportsRegeneration(byte version) {
            ModelAssetImportSettings settings = CreateModelAssetImportSettings();
            byte[] data = SerializeModelSettings(settings);
            data[5] = version;

            using MemoryStream stream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ModelAssetImportSettingsBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(ModelAssetImportSettingsBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures typed material settings reject every version other than the current layout and explain regeneration.
        /// </summary>
        [Theory]
        [InlineData((byte)(MaterialAssetImportSettingsBinarySerializer.CurrentVersion - 1))]
        [InlineData((byte)(MaterialAssetImportSettingsBinarySerializer.CurrentVersion + 1))]
        public void MaterialAssetImportSettingsBinarySerializer_Deserialize_WhenVersionIsNotCurrent_ReportsRegeneration(byte version) {
            MaterialAssetImportSettings settings = CreateMaterialAssetImportSettings();
            byte[] data = SerializeMaterialSettings(settings);
            data[5] = version;

            using MemoryStream stream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MaterialAssetImportSettingsBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(MaterialAssetImportSettingsBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures shared material settings documents reject every version other than the current layout and explain regeneration.
        /// </summary>
        [Theory]
        [InlineData((byte)(MaterialAssetCommonSettingsDocumentBinarySerializer.CurrentVersion - 1))]
        [InlineData((byte)(MaterialAssetCommonSettingsDocumentBinarySerializer.CurrentVersion + 1))]
        public void MaterialAssetCommonSettingsDocumentBinarySerializer_Deserialize_WhenVersionIsNotCurrent_ReportsRegeneration(byte version) {
            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
                AuthoringAssetId = "11111111222243338444555555555555"
            };
            document.Importer.ImporterId = "helengine.material";
            document.Processor.SchemaId = "standard-shader";
            document.Processor.FieldValues["base-color"] = "#ffffffff";
            byte[] data = SerializeMaterialCommonSettings(document);
            data[5] = version;

            using MemoryStream stream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(MaterialAssetCommonSettingsDocumentBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures platform material override documents reject every version other than the current layout and explain regeneration.
        /// </summary>
        [Theory]
        [InlineData((byte)(MaterialAssetPlatformOverrideDocumentBinarySerializer.CurrentVersion - 1))]
        [InlineData((byte)(MaterialAssetPlatformOverrideDocumentBinarySerializer.CurrentVersion + 1))]
        public void MaterialAssetPlatformOverrideDocumentBinarySerializer_Deserialize_WhenVersionIsNotCurrent_ReportsRegeneration(byte version) {
            MaterialAssetPlatformOverrideDocument document = new MaterialAssetPlatformOverrideDocument {
                PlatformId = "windows"
            };
            document.Processor.FieldValues["base-color"] = "#ffffffff";
            byte[] data = SerializeMaterialPlatformOverride(document);
            data[5] = version;

            using MemoryStream stream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MaterialAssetPlatformOverrideDocumentBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(MaterialAssetPlatformOverrideDocumentBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures current shared material settings preserve non-default identity, importer, field, and reference values.
        /// </summary>
        [Fact]
        public void MaterialAssetCommonSettingsDocumentBinarySerializer_RoundTripsNonDefaultSettings() {
            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
                AuthoringAssetId = "11111111222243338444555555555555",
                FormerAuthoringAssetIds = new List<string> { "aaaaaaaa222243338444555555555555" }
            };
            document.Importer.ImporterId = "helengine.material";
            document.Importer.SourceChecksum = "sha256:" + new string('a', 64);
            document.Importer.AssetId = "Materials/Panel.hasset";
            document.Processor.SchemaId = "standard-shader";
            document.Processor.FieldValues["base-color"] = "#12345678";
            document.Processor.AssetReferenceValues["texture-id"] = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "99999999222243338444555555555555",
                "Textures/Panel.png",
                "sha256:" + new string('b', 64));

            byte[] data = SerializeMaterialCommonSettings(document);
            using MemoryStream stream = new MemoryStream(data);
            MaterialAssetCommonSettingsDocument restored = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream);

            Assert.Equal(document.AuthoringAssetId, restored.AuthoringAssetId);
            Assert.Equal(document.FormerAuthoringAssetIds, restored.FormerAuthoringAssetIds);
            Assert.Equal(document.Importer.ImporterId, restored.Importer.ImporterId);
            Assert.Equal(document.Importer.SourceChecksum, restored.Importer.SourceChecksum);
            Assert.Equal(document.Importer.AssetId, restored.Importer.AssetId);
            Assert.Equal(document.Processor.SchemaId, restored.Processor.SchemaId);
            Assert.Equal(document.Processor.FieldValues["base-color"], restored.Processor.FieldValues["base-color"]);
            SceneAssetReference reference = restored.Processor.AssetReferenceValues["texture-id"];
            Assert.Equal("99999999222243338444555555555555", reference.AssetId);
            Assert.Equal("Textures/Panel.png", reference.RelativePath);
            Assert.Equal("sha256:" + new string('b', 64), reference.ContentHash);
        }

        /// <summary>
        /// Ensures current platform material overrides preserve non-default scope, schema, field, and reference values.
        /// </summary>
        [Fact]
        public void MaterialAssetPlatformOverrideDocumentBinarySerializer_RoundTripsNonDefaultSettings() {
            MaterialAssetPlatformOverrideDocument document = new MaterialAssetPlatformOverrideDocument {
                PlatformId = "windows",
                EnvironmentId = "hdr"
            };
            document.Processor.HasSchemaIdOverride = true;
            document.Processor.SchemaId = "standard-shader";
            document.Processor.FieldValues["base-color"] = "#87654321";
            document.Processor.AssetReferenceValues["texture-id"] = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "88888888222243338444555555555555",
                "Textures/HdrPanel.png",
                "sha256:" + new string('c', 64));

            byte[] data = SerializeMaterialPlatformOverride(document);
            using MemoryStream stream = new MemoryStream(data);
            MaterialAssetPlatformOverrideDocument restored = MaterialAssetPlatformOverrideDocumentBinarySerializer.Deserialize(stream);

            Assert.Equal(document.PlatformId, restored.PlatformId);
            Assert.Equal(document.EnvironmentId, restored.EnvironmentId);
            Assert.True(restored.Processor.HasSchemaIdOverride);
            Assert.Equal(document.Processor.SchemaId, restored.Processor.SchemaId);
            Assert.Equal(document.Processor.FieldValues["base-color"], restored.Processor.FieldValues["base-color"]);
            SceneAssetReference reference = restored.Processor.AssetReferenceValues["texture-id"];
            Assert.Equal("88888888222243338444555555555555", reference.AssetId);
            Assert.Equal("Textures/HdrPanel.png", reference.RelativePath);
            Assert.Equal("sha256:" + new string('c', 64), reference.ContentHash);
        }

        /// <summary>
        /// Ensures typed audio settings reject every version other than the current layout and explain regeneration.
        /// </summary>
        [Theory]
        [InlineData((byte)(AudioAssetImportSettingsBinarySerializer.CurrentVersion - 1))]
        [InlineData((byte)(AudioAssetImportSettingsBinarySerializer.CurrentVersion + 1))]
        public void AudioAssetImportSettingsBinarySerializer_Deserialize_WhenVersionIsNotCurrent_ReportsRegeneration(byte version) {
            AudioAssetImportSettings settings = CreateAudioAssetImportSettings();
            byte[] data = SerializeAudioSettings(settings);
            data[5] = version;

            using MemoryStream stream = new MemoryStream(data);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => AudioAssetImportSettingsBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(AudioAssetImportSettingsBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures blank platform ids are rejected by the typed texture settings serializer.
        /// </summary>
        [Fact]
        public void TextureAssetImportSettingsBinarySerializer_Serialize_WhenPlatformIdIsBlank_Throws() {
            TextureAssetImportSettings settings = CreateTextureAssetImportSettings();
            settings.Processor.Platforms[string.Empty] = new TextureAssetProcessorSettings {
                MaxResolution = 64,
                ColorFormat = TextureAssetColorFormat.Rgba32
            };

            using MemoryStream stream = new MemoryStream();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings));
            Assert.Contains("blank processor platform id", exception.Message);
        }

        /// <summary>
        /// Ensures null processor entries are rejected by the typed model settings serializer.
        /// </summary>
        [Fact]
        public void ModelAssetImportSettingsBinarySerializer_Serialize_WhenProcessorMapContainsNullEntry_Throws() {
            ModelAssetImportSettings settings = CreateModelAssetImportSettings();
            settings.Processor.Platforms["windows"] = null;

            using MemoryStream stream = new MemoryStream();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings));
            Assert.Contains("must include processor settings for platform 'windows'", exception.Message);
        }

        /// <summary>
        /// Ensures null material field values are rejected by the typed material settings serializer.
        /// </summary>
        [Fact]
        public void MaterialAssetImportSettingsBinarySerializer_Serialize_WhenFieldValueIsNull_Throws() {
            MaterialAssetImportSettings settings = CreateMaterialAssetImportSettings();
            settings.Processor.Platforms["windows"].FieldValues["texture-id"] = null;

            using MemoryStream stream = new MemoryStream();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => MaterialAssetImportSettingsBinarySerializer.Serialize(stream, settings));
            Assert.Contains("null material field value", exception.Message);
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
            string metadataPath = ShaderPackagePaths.GetMetadataPath(TempRootPath, "olderShader", ShaderCompileTarget.DirectX11);
            File.WriteAllText(metadataPath, "older-metadata");

            Assert.Throws<InvalidOperationException>(() => store.TryLoad("olderShader", out _));
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
                RuntimeAssetId = 0x0102030405060708UL,
                Width = 2,
                Height = 2,
                ColorFormat = TextureAssetColorFormat.Rgba32,
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
        static ShaderMaterialAsset CreateMaterialAsset() {
            ShaderMaterialAsset asset = new ShaderMaterialAsset {
                Id = "material/test",
                ShaderAssetId = "shader/test",
                VertexProgram = "ProgramMain",
                PixelProgram = "ProgramPixel",
                Variant = "Default",
                DiffuseTextureAssetId = "textures/diffuse",
                CastsShadows = false,
                ReceivesShadows = true,
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
                    },
                    new MaterialConstantBufferAsset {
                        Name = "RoughnessBuffer",
                        Data = new byte[] {
                            0x33, 0x33, 0x33, 0x3F,
                            0x33, 0x33, 0x33, 0x3F,
                            0x33, 0x33, 0x33, 0x3F,
                            0x33, 0x33, 0x33, 0x3F
                        }
                    }
                }
            };

            WritePublicStringField(asset, "RoughnessTextureAssetId", "textures/roughness");
            return asset;
        }

        /// <summary>
        /// Reads one public instance string field via reflection so serializer tests can fail cleanly before the field is implemented.
        /// </summary>
        /// <param name="instance">Object instance to inspect.</param>
        /// <param name="fieldName">Public instance field name.</param>
        /// <returns>Current string field value.</returns>
        static string ReadPublicStringField(object instance, string fieldName) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            System.Reflection.FieldInfo field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.NotNull(field);
            return Assert.IsType<string>(field.GetValue(instance));
        }

        /// <summary>
        /// Writes one public instance string field via reflection so serializer tests can set future fields before they exist.
        /// </summary>
        /// <param name="instance">Object instance to mutate.</param>
        /// <param name="fieldName">Public instance field name.</param>
        /// <param name="value">String value to assign.</param>
        static void WritePublicStringField(object instance, string fieldName, string value) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            System.Reflection.FieldInfo field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.NotNull(field);
            field.SetValue(instance, value);
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
        /// Creates a representative model asset with authored submesh metadata for serializer testing.
        /// </summary>
        /// <returns>Model asset with two authored submeshes.</returns>
        static ModelAsset CreateModelAssetWithSubmeshes() {
            return new ModelAsset {
                Id = "model/submeshes",
                Positions = new[] {
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(0f, 1f, 0f),
                    new float3(1f, 1f, 0f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f),
                    new float2(1f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2, 1, 3, 2 },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "Body",
                        IndexStart = 0,
                        IndexCount = 3
                    },
                    new ModelSubmeshAsset {
                        MaterialSlotName = "Trim",
                        IndexStart = 3,
                        IndexCount = 3
                    }
                }
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
        /// Serializes current texture settings so a test can replace only the version byte.
        /// </summary>
        /// <param name="settings">Texture settings to serialize.</param>
        /// <returns>Serialized texture settings bytes.</returns>
        static byte[] SerializeTextureSettings(TextureAssetImportSettings settings) {
            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            return stream.ToArray();
        }

        /// <summary>
        /// Serializes current model settings so a test can replace only the version byte.
        /// </summary>
        /// <param name="settings">Model settings to serialize.</param>
        /// <returns>Serialized model settings bytes.</returns>
        static byte[] SerializeModelSettings(ModelAssetImportSettings settings) {
            using MemoryStream stream = new MemoryStream();
            ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            return stream.ToArray();
        }

        /// <summary>
        /// Serializes current material settings so a test can replace only the version byte.
        /// </summary>
        /// <param name="settings">Material settings to serialize.</param>
        /// <returns>Serialized material settings bytes.</returns>
        static byte[] SerializeMaterialSettings(MaterialAssetImportSettings settings) {
            using MemoryStream stream = new MemoryStream();
            MaterialAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            return stream.ToArray();
        }

        /// <summary>
        /// Serializes current shared material settings so a test can replace only the version byte.
        /// </summary>
        /// <param name="document">Shared material settings document to serialize.</param>
        /// <returns>Serialized shared material settings bytes.</returns>
        static byte[] SerializeMaterialCommonSettings(MaterialAssetCommonSettingsDocument document) {
            using MemoryStream stream = new MemoryStream();
            MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
            return stream.ToArray();
        }

        /// <summary>
        /// Serializes current material platform override settings so a test can replace only the version byte.
        /// </summary>
        /// <param name="document">Platform override document to serialize.</param>
        /// <returns>Serialized platform override bytes.</returns>
        static byte[] SerializeMaterialPlatformOverride(MaterialAssetPlatformOverrideDocument document) {
            using MemoryStream stream = new MemoryStream();
            MaterialAssetPlatformOverrideDocumentBinarySerializer.Serialize(stream, document);
            return stream.ToArray();
        }

        /// <summary>
        /// Serializes current audio settings so a test can replace only the version byte.
        /// </summary>
        /// <param name="settings">Audio settings to serialize.</param>
        /// <returns>Serialized audio settings bytes.</returns>
        static byte[] SerializeAudioSettings(AudioAssetImportSettings settings) {
            using MemoryStream stream = new MemoryStream();
            AudioAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            return stream.ToArray();
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
        /// Creates representative typed texture asset import settings for serializer testing.
        /// </summary>
        /// <returns>Texture asset import settings with sample values.</returns>
        static TextureAssetImportSettings CreateTextureAssetImportSettings() {
            return new TextureAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "pfim",
                    SourceChecksum = "texture-checksum",
                    AssetId = "texture-id"
                },
                Processor = new TextureAssetProcessorPlatformSettings {
                    Platforms = new Dictionary<string, TextureAssetProcessorSettings> {
                        ["windows"] = new TextureAssetProcessorSettings {
                            MaxResolution = 512,
                            ColorFormat = TextureAssetColorFormat.Rgba32
                        },
                        ["android"] = new TextureAssetProcessorSettings {
                            MaxResolution = 128,
                            ColorFormat = TextureAssetColorFormat.Rgba4444
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates representative typed audio asset import settings for serializer testing.
        /// </summary>
        /// <returns>Audio asset import settings with sample values.</returns>
        static AudioAssetImportSettings CreateAudioAssetImportSettings() {
            return new AudioAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "wav",
                    SourceChecksum = "audio-checksum",
                    AssetId = "audio-id"
                },
                Processor = new AudioAssetProcessorPlatformSettings {
                    Platforms = new Dictionary<string, AudioAssetProcessorSettings> {
                        ["windows"] = new AudioAssetProcessorSettings {
                            EncodingFamilyId = "pcm-streamed",
                            PlaybackMode = AudioPlaybackMode.Streamed,
                            TargetChannels = 2,
                            TargetSampleRate = 44100,
                            StreamChunkByteSize = 4096,
                            DefaultLoop = true,
                            DefaultBusId = "music"
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates representative typed model asset import settings for serializer testing.
        /// </summary>
        /// <returns>Model asset import settings with sample values.</returns>
        static ModelAssetImportSettings CreateModelAssetImportSettings() {
            return new ModelAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "assimp",
                    SourceChecksum = "model-checksum",
                    AssetId = "model-id"
                },
                Processor = new ModelAssetProcessorPlatformSettings {
                    Platforms = new Dictionary<string, ModelAssetProcessorSettings> {
                        ["windows"] = new ModelAssetProcessorSettings {
                            FlipWinding = true,
                            Tessellate = true,
                            TessellationMaxEdgeLength = 0.25d
                        },
                        ["ps2"] = new ModelAssetProcessorSettings {
                            FlipWinding = false
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates representative typed material asset import settings for serializer testing.
        /// </summary>
        /// <returns>Material asset import settings with sample values.</returns>
        static MaterialAssetImportSettings CreateMaterialAssetImportSettings() {
            return new MaterialAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "helengine.material",
                    SourceChecksum = string.Empty,
                    AssetId = "Materials/Demo.hasset"
                },
                Processor = new MaterialAssetProcessorPlatformSettings {
                    Platforms = new Dictionary<string, MaterialAssetProcessorSettings> {
                        ["windows"] = new MaterialAssetProcessorSettings {
                            SchemaId = "standard-shader",
                            FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                                ["base-color"] = "#ffffffff",
                                ["texture-id"] = "Textures/checker"
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates one shared material-settings document with caller-controlled insertion order.
        /// </summary>
        /// <param name="fieldOrder">Field ids in insertion order.</param>
        /// <param name="referenceOrder">Reference ids in insertion order.</param>
        /// <returns>Material common-settings document.</returns>
        static MaterialAssetCommonSettingsDocument CreateMaterialCommonSettingsDocument(
            IReadOnlyList<string> fieldOrder,
            IReadOnlyList<string> referenceOrder) {
            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
                AuthoringAssetId = "00112233445566778899aabbccddeeff",
                FormerAuthoringAssetIds = new List<string> { "ffeeddccbbaa99887766554433221100" },
                Importer = new AssetImporterSettings {
                    ImporterId = "helengine.material",
                    SourceChecksum = "source",
                    AssetId = "Materials/Test"
                }
            };
            document.Processor.SchemaId = "standard-shader";
            for (int index = 0; index < fieldOrder.Count; index++) {
                document.Processor.FieldValues[fieldOrder[index]] = fieldOrder[index] + "-value";
            }
            for (int index = 0; index < referenceOrder.Count; index++) {
                string referenceId = referenceOrder[index];
                document.Processor.AssetReferenceValues[referenceId] = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                    string.Equals(referenceId, "normal", StringComparison.Ordinal)
                        ? "11112222333344445555666677778888"
                        : "9999aaaabbbbccccddddeeeeffffffff",
                    "Textures/" + referenceId + ".png",
                    "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            }
            return document;
        }

        /// <summary>
        /// Creates one scene whose reference collection is assembled in the supplied order.
        /// </summary>
        /// <param name="relativePaths">Reference paths in insertion order.</param>
        /// <returns>Scene asset with unordered references.</returns>
        static SceneAsset CreateSceneWithReferences(IReadOnlyList<string> relativePaths) {
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/Deterministic.helen",
                AuthoringAssetId = "00112233445566778899aabbccddeeff",
                FormerAuthoringAssetIds = Array.Empty<string>(),
                RootEntities = Array.Empty<SceneEntityAsset>(),
                SceneSettings = new SceneSettingsAsset()
            };
            List<SceneAssetReference> references = new List<SceneAssetReference>();
            for (int index = 0; index < relativePaths.Count; index++) {
                string relativePath = relativePaths[index];
                references.Add(global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                    relativePath.EndsWith("/A.hasset", StringComparison.Ordinal)
                        ? "11112222333344445555666677778888"
                        : "9999aaaabbbbccccddddeeeeffffffff",
                    relativePath,
                    "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
            }
            scene.AssetReferences = references.ToArray();
            return scene;
        }

        /// <summary>
        /// Creates one scene from already-created reference values.
        /// </summary>
        /// <param name="references">References in caller insertion order.</param>
        /// <returns>Scene asset with unordered references.</returns>
        static SceneAsset CreateSceneWithReferenceObjects(IReadOnlyList<SceneAssetReference> references) {
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/DeterministicTie.helen",
                AuthoringAssetId = "00112233445566778899aabbccddeeff",
                RootEntities = Array.Empty<SceneEntityAsset>(),
                AssetReferences = references.ToArray(),
                SceneSettings = new SceneSettingsAsset()
            };
            return scene;
        }

        /// <summary>
        /// Creates one blueprint from already-created reference values.
        /// </summary>
        /// <param name="references">References in caller insertion order.</param>
        /// <returns>Blueprint asset with unordered references.</returns>
        static BlueprintAsset CreateBlueprintWithReferenceObjects(IReadOnlyList<SceneAssetReference> references) {
            return new BlueprintAsset {
                Id = "Blueprints/DeterministicTie.hblueprint",
                AuthoringAssetId = "00112233445566778899aabbccddeeff",
                RootEntity = new SceneEntityAsset {
                    Id = 1u,
                    Name = "Root",
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                },
                AssetReferences = references.ToArray()
            };
        }

        /// <summary>
        /// Creates one animation clip with caller-ordered platform overrides.
        /// </summary>
        /// <param name="overrides">Platform overrides in caller insertion order.</param>
        /// <returns>Animation clip asset.</returns>
        static AnimationClipAsset CreateAnimationWithOverrides(AnimationClipPlatformOverrideAsset[] overrides) {
            return new AnimationClipAsset {
                Id = "Animations/OverrideOrder",
                Duration = 1f,
                PlatformOverrides = overrides
            };
        }

        /// <summary>
        /// Creates one audio asset with caller-ordered platform overrides.
        /// </summary>
        /// <param name="overrides">Platform overrides in caller insertion order.</param>
        /// <returns>Audio asset.</returns>
        static AudioAsset CreateAudioWithOverrides(AudioAssetPlatformOverrideAsset[] overrides) {
            return new AudioAsset {
                Id = "Audio/OverrideOrder",
                PlatformOverrides = overrides
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
