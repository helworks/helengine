using Xunit;

namespace helengine.editor.tests.serialization;

/// <summary>
/// Verifies total ordering of scene and blueprint platform override payloads.
/// </summary>
public sealed class DeterministicScenePlatformSerializationTests {
    [Fact]
    public void AssetSerializer_ScenePlatformOverrides_WhenInsertedInReverseOrder_IsDeterministic() {
        SceneAsset first = CreateScene(new[] {
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "windows", EnvironmentId = "shipping", Exists = true },
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "android", EnvironmentId = "debug", Exists = false },
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "android", EnvironmentId = "shipping", Exists = true }
        });
        SceneAsset second = CreateScene(first.RootEntities[0].PlatformExistenceOverrides.Reverse().ToArray());

        Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
    }

    [Fact]
    public void AssetSerializer_BlueprintPlatformOverrides_WhenInsertedInReverseOrder_IsDeterministic() {
        SceneEntityPlatformComponentOverrideAsset[] firstOverrides = new[] {
            new SceneEntityPlatformComponentOverrideAsset {
                PlatformId = "windows", EnvironmentId = "shipping",
                RemovedComponentKeys = new[] { "z-key", "a-key" },
                AddedComponents = new[] {
                    new SceneEntityPlatformAddedComponentAsset { Component = CreateComponent("z-key", 2, new byte[] { 2 }) },
                    new SceneEntityPlatformAddedComponentAsset { Component = CreateComponent("a-key", 1, new byte[] { 1 }) }
                }
            },
            new SceneEntityPlatformComponentOverrideAsset { PlatformId = "android", EnvironmentId = "debug" }
        };
        BlueprintAsset first = CreateBlueprint(firstOverrides);
        BlueprintAsset second = CreateBlueprint(first.RootEntity.PlatformComponentOverrides.Reverse().Select(CloneComponentOverride).ToArray());

        Assert.Equal(AssetSerializer.SerializeToBytes(first), AssetSerializer.SerializeToBytes(second));
    }

    [Fact]
    public void AssetSerializer_ScenePlatformExistenceOverrides_WhenScopeIsDuplicated_RejectsBeforeWriting() {
        SceneAsset scene = CreateScene(new[] {
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "windows", EnvironmentId = "shipping", Exists = true },
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "windows", EnvironmentId = "shipping", Exists = false }
        });

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(scene));
    }

    [Fact]
    public void AssetSerializer_ScenePlatformExistenceOverrides_WhenTrimmedCaseInsensitiveScopeIsDuplicated_RejectsBeforeWriting() {
        SceneAsset scene = CreateScene(new[] {
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = " Windows ", EnvironmentId = " SHIPPING ", Exists = true },
            new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "windows", EnvironmentId = "shipping", Exists = false }
        });

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(scene));
    }

    [Fact]
    public void AssetSerializer_BlueprintPlatformExistenceOverrides_WhenScopeIsDuplicated_LeavesOutputUntouched() {
        BlueprintAsset blueprint = new BlueprintAsset {
            Id = "Blueprints/Duplicate.hblueprint",
            RootEntity = new SceneEntityAsset {
                Id = 1,
                PlatformExistenceOverrides = new[] {
                    new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "p", EnvironmentId = "e", Exists = true },
                    new SceneEntityPlatformExistenceOverrideAsset { PlatformId = "p", EnvironmentId = "e", Exists = false }
                },
                Children = Array.Empty<SceneEntityAsset>()
            },
            AssetReferences = Array.Empty<SceneAssetReference>()
        };
        using MemoryStream stream = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.Serialize(stream, blueprint));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void AssetSerializer_BlueprintPlatformTransformOverrides_WhenSignedZeroScopeIsDuplicated_RejectsBeforeWriting() {
        BlueprintAsset blueprint = CreateBlueprintWithTransforms(new[] {
            new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = "windows", EnvironmentId = "shipping", LocalPosition = new float3(+0f, 1f, 2f)
            },
            new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = "windows", EnvironmentId = "shipping", LocalPosition = new float3(-0f, 1f, 2f)
            }
        });

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(blueprint));
    }

    [Fact]
    public void AssetSerializer_BlueprintPlatformTransformOverrides_WhenTrimmedCaseInsensitiveScopeIsDuplicated_RejectsBeforeWriting() {
        BlueprintAsset blueprint = CreateBlueprintWithTransforms(new[] {
            new SceneEntityPlatformTransformOverrideAsset { PlatformId = " Windows ", EnvironmentId = " SHIPPING ", LocalPosition = new float3(1f, 2f, 3f) },
            new SceneEntityPlatformTransformOverrideAsset { PlatformId = "windows", EnvironmentId = "shipping", LocalPosition = new float3(4f, 5f, 6f) }
        });

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(blueprint));
    }

    [Fact]
    public void AssetSerializer_ScenePlatformComponentOverrides_WhenScopeIsDuplicated_RejectsBeforeWriting() {
        SceneAsset scene = CreateSceneWithComponents(new[] {
            new SceneEntityPlatformComponentOverrideAsset {
                PlatformId = "platform\u001fenvironment", EnvironmentId = "scope", RemovedComponentKeys = new[] { "a\u001fb" }
            },
            new SceneEntityPlatformComponentOverrideAsset {
                PlatformId = "platform\u001fenvironment", EnvironmentId = "scope", RemovedComponentKeys = new[] { "other" }
            }
        });

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(scene));
    }

    [Fact]
    public void AssetSerializer_BlueprintPlatformComponentOverrides_WhenTrimmedCaseInsensitiveScopeIsDuplicated_RejectsBeforeWriting() {
        BlueprintAsset blueprint = CreateBlueprint(new[] {
            new SceneEntityPlatformComponentOverrideAsset { PlatformId = " p ", EnvironmentId = " E ", RemovedComponentKeys = new[] { "one" } },
            new SceneEntityPlatformComponentOverrideAsset { PlatformId = "P", EnvironmentId = "e", RemovedComponentKeys = new[] { "two" } }
        });

        Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(blueprint));
    }

    static SceneAsset CreateScene(SceneEntityPlatformExistenceOverrideAsset[] overrides) {
        return new SceneAsset {
            Id = "Scenes/Deterministic.hscene",
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = 1,
                    PlatformExistenceOverrides = overrides,
                    PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                    PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            },
            AssetReferences = Array.Empty<SceneAssetReference>()
        };
    }

    static BlueprintAsset CreateBlueprint(SceneEntityPlatformComponentOverrideAsset[] overrides) {
        return new BlueprintAsset {
            Id = "Blueprints/Deterministic.hblueprint",
            RootEntity = new SceneEntityAsset {
                Id = 1,
                PlatformExistenceOverrides = Array.Empty<SceneEntityPlatformExistenceOverrideAsset>(),
                PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                PlatformComponentOverrides = overrides,
                Children = Array.Empty<SceneEntityAsset>()
            },
            AssetReferences = Array.Empty<SceneAssetReference>()
        };
    }

    static BlueprintAsset CreateBlueprintWithTransforms(SceneEntityPlatformTransformOverrideAsset[] overrides) {
        return new BlueprintAsset {
            Id = "Blueprints/Deterministic.hblueprint",
            RootEntity = new SceneEntityAsset {
                Id = 1,
                PlatformExistenceOverrides = Array.Empty<SceneEntityPlatformExistenceOverrideAsset>(),
                PlatformTransformOverrides = overrides,
                PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>(),
                Children = Array.Empty<SceneEntityAsset>()
            },
            AssetReferences = Array.Empty<SceneAssetReference>()
        };
    }

    static SceneAsset CreateSceneWithComponents(SceneEntityPlatformComponentOverrideAsset[] overrides) {
        return new SceneAsset {
            Id = "Scenes/Deterministic.hscene",
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = 1,
                    PlatformExistenceOverrides = Array.Empty<SceneEntityPlatformExistenceOverrideAsset>(),
                    PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                    PlatformComponentOverrides = overrides,
                    Children = Array.Empty<SceneEntityAsset>()
                }
            },
            AssetReferences = Array.Empty<SceneAssetReference>()
        };
    }

    static SceneEntityPlatformComponentOverrideAsset CloneComponentOverride(SceneEntityPlatformComponentOverrideAsset source) {
        return new SceneEntityPlatformComponentOverrideAsset {
            PlatformId = source.PlatformId,
            EnvironmentId = source.EnvironmentId,
            RemovedComponentKeys = source.RemovedComponentKeys.ToArray(),
            AddedComponents = source.AddedComponents.Select(added => new SceneEntityPlatformAddedComponentAsset {
                Component = CreateComponent(
                    added.Component.ComponentKey,
                    added.Component.ComponentIndex,
                    added.Component.Payload.ToArray())
            }).ToArray()
        };
    }

    static SceneComponentAssetRecord CreateComponent(string key, int index, byte[] payload) {
        return new SceneComponentAssetRecord {
            ComponentKey = key,
            ComponentTypeId = "test.component",
            ComponentIndex = index,
            Payload = payload
        };
    }
}
