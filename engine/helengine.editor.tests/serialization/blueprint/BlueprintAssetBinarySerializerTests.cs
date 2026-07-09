using Xunit;

namespace helengine.editor.tests.serialization.blueprint {
    /// <summary>
    /// Verifies HELE serialization behavior for blueprint assets.
    /// </summary>
    public class BlueprintAssetBinarySerializerTests {
        /// <summary>
        /// Ensures blueprint assets round-trip through the HELE serializer with one root entity, asset references, and platform overrides intact.
        /// </summary>
        [Fact]
        public void AssetSerializer_BlueprintAsset_RoundTripsRootEntityReferencesAndPlatformOverrides() {
            BlueprintAsset asset = new BlueprintAsset {
                Id = "Blueprints/Test.blueprint",
                RootEntity = new SceneEntityAsset {
                    Id = 11u,
                    Name = "BlueprintRoot",
                    LayerMask = 0x2222,
                    LocalPosition = new float3(1f, 2f, 3f),
                    LocalScale = new float3(2f, 2f, 2f),
                    LocalOrientation = new float4(0f, 0.70710677f, 0f, 0.70710677f),
                    Components = new[] {
                        new SceneComponentAssetRecord {
                            ComponentKey = "mesh-0",
                            ComponentTypeId = "helengine.MeshComponent",
                            ComponentIndex = 0,
                            Payload = new byte[] { 1, 2, 3, 4 }
                        }
                    },
                    PlatformExistenceOverrides = new[] {
                        new SceneEntityPlatformExistenceOverrideAsset {
                            PlatformId = "windows",
                            Exists = true
                        },
                        new SceneEntityPlatformExistenceOverrideAsset {
                            PlatformId = "nintendo_ds",
                            Exists = false
                        }
                    },
                    PlatformTransformOverrides = new[] {
                        new SceneEntityPlatformTransformOverrideAsset {
                            PlatformId = "windows",
                            HasLocalPositionOverride = true,
                            LocalPosition = new float3(4f, 5f, 6f),
                            HasLocalScaleOverride = true,
                            LocalScale = new float3(3f, 3f, 3f),
                            HasLocalOrientationOverride = true,
                            LocalOrientation = new float4(0f, 0f, 0.70710677f, 0.70710677f)
                        }
                    },
                    PlatformComponentOverrides = new[] {
                        new SceneEntityPlatformComponentOverrideAsset {
                            PlatformId = "windows",
                            RemovedComponentKeys = new[] { "mesh-removed" },
                            AddedComponents = new[] {
                                new SceneEntityPlatformAddedComponentAsset {
                                    Component = new SceneComponentAssetRecord {
                                        ComponentKey = "mesh-added",
                                        ComponentTypeId = "helengine.MeshComponent",
                                        ComponentIndex = 1,
                                        Payload = new byte[] { 9, 8, 7 }
                                    }
                                }
                            }
                        }
                    },
                    Children = new[] {
                        new SceneEntityAsset {
                            Id = 12u,
                            Name = "BlueprintChild",
                            LayerMask = 0x4444,
                            LocalPosition = new float3(7f, 8f, 9f),
                            LocalScale = float3.One,
                            LocalOrientation = float4.Identity,
                            Components = Array.Empty<SceneComponentAssetRecord>(),
                            Children = Array.Empty<SceneEntityAsset>()
                        }
                    }
                },
                AssetReferences = new[] {
                    global::helengine.SceneAssetReferenceFactory.CreateFileSystemTexture("Textures/test.png"),
                    global::helengine.SceneAssetReferenceFactory.CreateFileSystemMaterial("Materials/test.hasset")
                }
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            BlueprintAsset deserialized = Assert.IsType<BlueprintAsset>(AssetSerializer.DeserializeFromBytes(data));

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)EditorAssetBinaryValueKind.BlueprintAsset, header.ValueKind);
            Assert.Equal(EditorAssetBinarySerializer.CurrentVersion, header.Version);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(11u, deserialized.RootEntity.Id);
            Assert.Equal("BlueprintRoot", deserialized.RootEntity.Name);
            Assert.Equal((ushort)0x2222, deserialized.RootEntity.LayerMask);
            Assert.Equal(new float3(1f, 2f, 3f), deserialized.RootEntity.LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), deserialized.RootEntity.LocalScale);
            Assert.Equal(new float4(0f, 0.70710677f, 0f, 0.70710677f), deserialized.RootEntity.LocalOrientation);
            Assert.Equal("mesh-0", Assert.Single(deserialized.RootEntity.Components).ComponentKey);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, deserialized.RootEntity.Components[0].Payload);
            Assert.Collection(
                deserialized.RootEntity.PlatformExistenceOverrides,
                windowsOverride => {
                    Assert.Equal("windows", windowsOverride.PlatformId);
                    Assert.True(windowsOverride.Exists);
                },
                dsOverride => {
                    Assert.Equal("nintendo_ds", dsOverride.PlatformId);
                    Assert.False(dsOverride.Exists);
                });
            SceneEntityPlatformTransformOverrideAsset transformOverride = Assert.Single(deserialized.RootEntity.PlatformTransformOverrides);
            Assert.Equal("windows", transformOverride.PlatformId);
            Assert.True(transformOverride.HasLocalPositionOverride);
            Assert.Equal(new float3(4f, 5f, 6f), transformOverride.LocalPosition);
            SceneEntityPlatformComponentOverrideAsset componentOverride = Assert.Single(deserialized.RootEntity.PlatformComponentOverrides);
            Assert.Equal("windows", componentOverride.PlatformId);
            Assert.Equal("mesh-removed", Assert.Single(componentOverride.RemovedComponentKeys));
            Assert.Equal("mesh-added", Assert.Single(componentOverride.AddedComponents).Component.ComponentKey);
            Assert.Equal(12u, Assert.Single(deserialized.RootEntity.Children).Id);
            Assert.Equal("BlueprintChild", deserialized.RootEntity.Children[0].Name);
            Assert.Collection(
                deserialized.AssetReferences,
                textureReference => Assert.Equal("Textures/test.png", textureReference.RelativePath),
                materialReference => Assert.Equal("Materials/test.hasset", materialReference.RelativePath));
        }

        /// <summary>
        /// Ensures blueprint serialization rejects assets without one root entity.
        /// </summary>
        [Fact]
        public void AssetSerializer_BlueprintAsset_WhenRootEntityIsNull_ThrowsInvalidOperationException() {
            BlueprintAsset asset = new BlueprintAsset {
                Id = "Blueprints/Invalid.blueprint",
                RootEntity = null
            };

            Action action = () => AssetSerializer.SerializeToBytes(asset);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(action);

            Assert.Equal("Blueprint assets must define exactly one root entity.", exception.Message);
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
    }
}
