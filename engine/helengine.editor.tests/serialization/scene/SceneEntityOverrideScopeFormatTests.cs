using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the scope path and level order survive the editor asset format and that the previous entity payload version is rejected.
    /// </summary>
    public sealed class SceneEntityOverrideScopeFormatTests {
        [Fact]
        public void SceneAsset_RoundTripsGroupChainCommonAndLevelOrder() {
            SceneEntityPlatformExistenceOverrideAsset commonOverride = new SceneEntityPlatformExistenceOverrideAsset {
                Scope = SceneOverrideScopePath.Common(),
                Exists = false
            };
            SceneEntityPlatformExistenceOverrideAsset groupOverride = new SceneEntityPlatformExistenceOverrideAsset {
                Scope = new[] {
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "consoles" },
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "handheld" }
                },
                Exists = true
            };
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/Scope.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Rig",
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        HasOverrideLevelOrder = true,
                        OverrideLevelOrder = new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform },
                        PlatformExistenceOverrides = new[] { groupOverride, commonOverride },
                        PlatformTransformOverrides = new[] {
                            new SceneEntityPlatformTransformOverrideAsset {
                                Scope = SceneOverrideScopePath.PlatformBuildConfig("ps1", "debug"),
                                HasLocalScaleOverride = true,
                                LocalScale = new float3(2f, 2f, 2f)
                            }
                        }
                    }
                }
            };

            SceneAsset loaded = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(new MemoryStream(AssetSerializer.SerializeToBytes(scene))));
            SceneEntityAsset entity = Assert.Single(loaded.RootEntities);

            Assert.True(entity.HasOverrideLevelOrder);
            Assert.Equal(new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform }, entity.OverrideLevelOrder);
            Assert.Equal(2, entity.PlatformExistenceOverrides.Length);
            Assert.Equal("common", SceneOverrideScopePath.Format(entity.PlatformExistenceOverrides[0].Scope));
            Assert.False(entity.PlatformExistenceOverrides[0].Exists);
            Assert.Equal("group:consoles/group:handheld", SceneOverrideScopePath.Format(entity.PlatformExistenceOverrides[1].Scope));
            Assert.Equal("platform:ps1/buildconfig:debug", SceneOverrideScopePath.Format(Assert.Single(entity.PlatformTransformOverrides).Scope));
        }

        [Fact]
        public void SceneAsset_WithoutLevelOrder_ReadsBackAsAbsent() {
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/NoOrder.helen",
                RootEntities = new[] { new SceneEntityAsset { Id = 1u, Name = "A", LocalScale = float3.One, LocalOrientation = float4.Identity } }
            };

            SceneAsset loaded = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(new MemoryStream(AssetSerializer.SerializeToBytes(scene))));

            Assert.False(loaded.RootEntities[0].HasOverrideLevelOrder);
            Assert.Empty(loaded.RootEntities[0].OverrideLevelOrder);
        }

        [Fact]
        public void SceneAsset_WithDuplicatePath_IsRejectedBeforeWriting() {
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/Dup.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u, Name = "A", LocalScale = float3.One, LocalOrientation = float4.Identity,
                        PlatformExistenceOverrides = new[] {
                            new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Platform("PS1"), Exists = true },
                            new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Platform("ps1"), Exists = false }
                        }
                    }
                }
            };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(scene));
            Assert.Contains("platform:ps1", error.Message);
        }

        [Fact]
        public void Versions_MovedTogether() {
            Assert.Equal(9, helengine.files.SceneEntityPayloadFormat.SceneEntityPayloadVersion);
            Assert.Equal(25, helengine.files.EditorAssetBinarySerializer.CurrentVersion);
            Assert.Equal(25, PackagedAssetBinarySerializer.CurrentVersion);
        }
    }
}
