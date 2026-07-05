# Platform Entity And Component Existence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add generic per-platform `Exists` overrides for authored scene entities and components so one common scene can omit handheld-only subtrees and components during packaging without generator-owned scene variants.

**Architecture:** Extend the existing embedded scene override model instead of introducing sidecars. Persist a new entity-level platform existence override array on `SceneEntityAsset`, store matching editor metadata on `EntitySaveComponent`, and prune target-platform entity subtrees before transform override application and component packaging. Reuse the existing component override foundation, but expose it through explicit `Exists` inspector checkboxes for both entities and components.

**Tech Stack:** C#/.NET 9, shared scene asset binary serializers, helengine editor scene save/load services, Windows scene packaging, editor UI (`PropertiesPanel`, `ComponentPropertiesView`), xUnit

---

## File Structure

### New files

- `engine/helengine.core/assets/raw/scene/SceneEntityPlatformExistenceOverrideAsset.cs`
  - Raw serialized entity-level platform existence payload with `PlatformId` and `Exists`.
- `engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs`
  - Editor-only service that reads, writes, and reverts entity existence overrides on `EntitySaveComponent`.

### Modified files

- `engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs`
  - Adds the new `PlatformExistenceOverrides` array beside transform and component overrides.
- `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
  - Adds scene-entity payload version `7` read support for entity existence overrides.
- `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
  - Adds scene-entity payload version `7` write/read support for entity existence overrides.
- `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
  - Stores editor-only entity existence overrides keyed by platform id.
- `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
  - Saves entity existence overrides into `SceneEntityAsset.PlatformExistenceOverrides`.
- `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
  - Restores entity existence overrides from serialized scene assets back into `EntitySaveComponent`.
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
  - Prunes target-platform-absent root entities and child subtrees before transform overrides and component rewriting.
- `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
  - Adds explicit query/set helpers for component existence so the inspector can use `Exists` instead of remove/revert-specific logic.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  - Adds one entity-level platform `Exists` row with checkbox and revert behavior.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  - Adds one component-level platform `Exists` row at the top of each platform-edited component section.

### Test files

- `engine/helengine.editor.tests/BinarySerializationTests.cs`
- `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

---

### Task 1: Add Entity Existence Payload Support To Scene Assets And Binary Serialization

**Files:**
- Create: `engine/helengine.core/assets/raw/scene/SceneEntityPlatformExistenceOverrideAsset.cs`
- Modify: `engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing binary serializer test**

```csharp
[Fact]
public void AssetSerializer_SceneAsset_WhenEntityUsesPlatformExistenceOverride_RoundTripsValues() {
    SceneAsset asset = new SceneAsset {
        Id = "Scenes/PlatformEntityExists.helen",
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = 1u,
                Name = "Root",
                Enabled = true,
                LocalPosition = new float3(1f, 2f, 3f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                PlatformExistenceOverrides = new[] {
                    new SceneEntityPlatformExistenceOverrideAsset {
                        PlatformId = "ds",
                        Exists = false
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            }
        }
    };

    byte[] bytes = AssetSerializer.SerializeToBytes(asset);
    SceneAsset restored = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(bytes));
    SceneEntityAsset root = Assert.Single(restored.RootEntities);
    SceneEntityPlatformExistenceOverrideAsset dsOverride = Assert.Single(root.PlatformExistenceOverrides);

    Assert.Equal("ds", dsOverride.PlatformId);
    Assert.False(dsOverride.Exists);
}
```

- [ ] **Step 2: Run the focused test and confirm it fails before the payload changes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests.AssetSerializer_SceneAsset_WhenEntityUsesPlatformExistenceOverride_RoundTripsValues" -v minimal
```

Expected: `FAIL` because `SceneEntityAsset` has no `PlatformExistenceOverrides` field and the serializers still use scene-entity payload version `6`.

- [ ] **Step 3: Add the raw asset type and scene-entity field**

Create `engine/helengine.core/assets/raw/scene/SceneEntityPlatformExistenceOverrideAsset.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one platform-specific entity existence override attached to a serialized scene entity.
    /// </summary>
    public class SceneEntityPlatformExistenceOverrideAsset {
        /// <summary>
        /// Gets or sets the platform identifier that owns the existence override.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the entity should exist on the owning platform.
        /// </summary>
        public bool Exists { get; set; } = true;
    }
}
```

Add this property to `engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs` directly above `PlatformTransformOverrides`:

```csharp
/// <summary>
/// Gets or sets the editor-authored per-platform entity existence overrides attached to the entity.
/// </summary>
public SceneEntityPlatformExistenceOverrideAsset[] PlatformExistenceOverrides { get; set; } = Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
```

- [ ] **Step 4: Bump scene-entity payload version from `6` to `7` and serialize the new array**

Apply the same shape in both serializer copies:

```csharp
const byte SceneEntityPayloadVersion = 7;
```

```csharp
writer.WriteArray(asset.PlatformExistenceOverrides, WriteSceneEntityPlatformExistenceOverrideAsset);
writer.WriteArray(asset.PlatformTransformOverrides, WriteSceneEntityPlatformTransformOverrideAsset);
writer.WriteArray(asset.PlatformComponentOverrides, WriteSceneEntityPlatformComponentOverrideAsset);
```

```csharp
SceneEntityPlatformExistenceOverrideAsset[] platformExistenceOverrides = payloadVersion >= 7
    ? reader.ReadArray(ReadSceneEntityPlatformExistenceOverrideAsset) ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>()
    : Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
```

```csharp
return new SceneEntityAsset {
    Id = id,
    Name = name,
    IsStatic = isStatic,
    Enabled = enabled,
    LayerMask = layerMask,
    LocalPosition = localPosition,
    LocalScale = localScale,
    LocalOrientation = localOrientation,
    Components = components,
    PlatformExistenceOverrides = platformExistenceOverrides,
    PlatformTransformOverrides = platformTransformOverrides,
    PlatformComponentOverrides = platformComponentOverrides,
    Children = children
};
```

Add read/write helpers in both serializers:

```csharp
static void WriteSceneEntityPlatformExistenceOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformExistenceOverrideAsset asset) {
    writer.WriteString(asset.PlatformId);
    writer.WriteByte(asset.Exists ? (byte)1 : (byte)0);
}

static SceneEntityPlatformExistenceOverrideAsset ReadSceneEntityPlatformExistenceOverrideAsset(EngineBinaryReader reader) {
    return new SceneEntityPlatformExistenceOverrideAsset {
        PlatformId = reader.ReadString(),
        Exists = reader.ReadByte() != 0
    };
}
```

- [ ] **Step 5: Run the focused binary serializer test again and confirm it passes**

Run the same command from Step 2.

Expected: `PASS`

- [ ] **Step 6: Commit the raw payload extension**

```bash
git add engine/helengine.core/assets/raw/scene/SceneEntityPlatformExistenceOverrideAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "feat: add platform entity existence scene payload"
```

---

### Task 2: Persist Entity Existence Overrides Through Editor Save And Load

**Files:**
- Modify: `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Write the failing save/load roundtrip test**

Add this test near the existing platform transform/component save tests:

```csharp
[Fact]
public void SaveAndLoad_WhenEntityHasDsExistenceOverride_RoundTripsTheOverrideMetadata() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
    string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PlatformEntityExists.helen");
    EditorEntity entity = CreateUserEntity("PlatformEntity", float3.Zero, float3.One, float4.Identity);
    EntitySaveComponent saveComponent = GetSaveComponent(entity);

    saveComponent.GetOrCreateExistencePlatformOverride("ds").Exists = false;
    saveService.Save(scenePath);

    SceneAsset asset;
    using (FileStream stream = File.OpenRead(scenePath)) {
        asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    }

    SceneEntityAsset rootEntity = Assert.Single(asset.RootEntities);
    SceneEntityPlatformExistenceOverrideAsset dsOverride = Assert.Single(rootEntity.PlatformExistenceOverrides);
    Assert.Equal("ds", dsOverride.PlatformId);
    Assert.False(dsOverride.Exists);

    SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
    EditorEntity loadedEntity = Assert.Single(loadService.Load(asset));
    EntitySaveComponent loadedSaveComponent = GetSaveComponent(loadedEntity);

    Assert.True(loadedSaveComponent.TryGetExistencePlatformOverride("ds", out SceneEntityPlatformExistenceOverrideAsset restoredOverride));
    Assert.False(restoredOverride.Exists);
}
```

- [ ] **Step 2: Run the focused save/load test and confirm it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneSaveServiceTests.SaveAndLoad_WhenEntityHasDsExistenceOverride_RoundTripsTheOverrideMetadata" -v minimal
```

Expected: `FAIL` because `EntitySaveComponent` cannot store entity existence overrides and scene save/load ignores the new serialized array.

- [ ] **Step 3: Extend `EntitySaveComponent` with entity existence override storage**

Add a dictionary and accessors beside the existing transform/component override members:

```csharp
readonly Dictionary<string, SceneEntityPlatformExistenceOverrideAsset> ExistenceOverridesByPlatformId;
```

Initialize it in the constructor:

```csharp
ExistenceOverridesByPlatformId = new Dictionary<string, SceneEntityPlatformExistenceOverrideAsset>(StringComparer.OrdinalIgnoreCase);
```

Add these members:

```csharp
public SceneEntityPlatformExistenceOverrideAsset GetOrCreateExistencePlatformOverride(string platformId) {
    if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    }

    if (!ExistenceOverridesByPlatformId.TryGetValue(platformId, out SceneEntityPlatformExistenceOverrideAsset overrideState)) {
        overrideState = new SceneEntityPlatformExistenceOverrideAsset {
            PlatformId = platformId
        };
        ExistenceOverridesByPlatformId.Add(platformId, overrideState);
    }

    return overrideState;
}

public bool TryGetExistencePlatformOverride(string platformId, out SceneEntityPlatformExistenceOverrideAsset overrideState) {
    if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    }

    return ExistenceOverridesByPlatformId.TryGetValue(platformId, out overrideState);
}

public void RemoveExistencePlatformOverride(string platformId) {
    if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    }

    ExistenceOverridesByPlatformId.Remove(platformId);
}

public IEnumerable<SceneEntityPlatformExistenceOverrideAsset> EnumerateExistencePlatformOverrides() {
    return ExistenceOverridesByPlatformId.Values;
}
```

- [ ] **Step 4: Save and restore the new overrides in scene persistence**

In `SceneSaveService`, add the cloned array when building `SceneEntityAsset`:

```csharp
PlatformExistenceOverrides = ClonePlatformExistenceOverrides(saveComponent),
PlatformTransformOverrides = ClonePlatformTransformOverrides(saveComponent),
PlatformComponentOverrides = ClonePlatformComponentOverrides(saveComponent),
```

Add the clone helper:

```csharp
SceneEntityPlatformExistenceOverrideAsset[] ClonePlatformExistenceOverrides(EntitySaveComponent saveComponent) {
    if (saveComponent == null) {
        return Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
    }

    List<SceneEntityPlatformExistenceOverrideAsset> overrides = new List<SceneEntityPlatformExistenceOverrideAsset>();
    foreach (SceneEntityPlatformExistenceOverrideAsset overrideState in saveComponent.EnumerateExistencePlatformOverrides()) {
        if (overrideState == null || string.IsNullOrWhiteSpace(overrideState.PlatformId) || overrideState.Exists) {
            continue;
        }

        overrides.Add(new SceneEntityPlatformExistenceOverrideAsset {
            PlatformId = overrideState.PlatformId,
            Exists = false
        });
    }

    return overrides.ToArray();
}
```

In `SceneLoadService`, restore the overrides after transform restoration:

```csharp
if (saveComponent != null) {
    RestoreEntityExistencePlatformOverrides(entityAsset, saveComponent);
    RestoreEntityComponentPlatformOverrides(entityAsset, saveComponent);
}
```

```csharp
void RestoreEntityExistencePlatformOverrides(SceneEntityAsset entityAsset, EntitySaveComponent saveComponent) {
    SceneEntityPlatformExistenceOverrideAsset[] overrideAssets = entityAsset.PlatformExistenceOverrides ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
    for (int index = 0; index < overrideAssets.Length; index++) {
        SceneEntityPlatformExistenceOverrideAsset overrideAsset = overrideAssets[index];
        if (overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)) {
            continue;
        }

        saveComponent.GetOrCreateExistencePlatformOverride(overrideAsset.PlatformId).Exists = overrideAsset.Exists;
    }
}
```

- [ ] **Step 5: Run the focused roundtrip test again and confirm it passes**

Run the same command from Step 2.

Expected: `PASS`

- [ ] **Step 6: Commit the editor save/load persistence**

```bash
git add engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "feat: persist platform entity existence overrides"
```

---

### Task 3: Prune Target-Platform-Absent Entity Subtrees During Packaging

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing subtree-pruning packager test**

Add this test beside the existing transform/component override packaging tests:

```csharp
[Fact]
public void Package_WhenSceneEntityDefinesDsExistenceFalse_PrunesTheEntireSubtreeBeforeComponentPackaging() {
    string sceneId = "Scenes/PlatformEntityExists.helen";
    WriteSceneAsset(sceneId, new SceneAsset {
        Id = sceneId,
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = 1u,
                Name = "DesktopOnlyRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                PlatformExistenceOverrides = new[] {
                    new SceneEntityPlatformExistenceOverrideAsset {
                        PlatformId = "ds",
                        Exists = false
                    }
                },
                Children = new[] {
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "DesktopOnlyChild",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            }
        }
    });

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        ProjectRootPath,
        Array.Empty<IAssetImporterRegistration>(),
        "ds");
    packager.Package(new[] { sceneId }, BuildRootPath);

    using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
    SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));

    Assert.Empty(packagedScene.RootEntities);
}
```

- [ ] **Step 2: Run the focused packaging test and confirm it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenSceneEntityDefinesDsExistenceFalse_PrunesTheEntireSubtreeBeforeComponentPackaging" -v minimal
```

Expected: `FAIL` because the packager currently ignores `PlatformExistenceOverrides` and still writes the root entity.

- [ ] **Step 3: Add target-platform entity existence pruning before transform/component override application**

Refactor the rewrite flow from one-entity recursion into array-level pruning:

```csharp
void RewriteSceneAsset(SceneAsset sceneAsset, string buildRootPath) {
    SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
    sceneAsset.RootEntities = RewriteEntityAssets(rootEntityAssets, buildRootPath);
}

SceneEntityAsset[] RewriteEntityAssets(SceneEntityAsset[] entityAssets, string buildRootPath) {
    List<SceneEntityAsset> rewritten = new List<SceneEntityAsset>();
    for (int index = 0; index < entityAssets.Length; index++) {
        SceneEntityAsset entityAsset = entityAssets[index];
        if (entityAsset == null || !ShouldEntityExistOnTargetPlatform(entityAsset)) {
            continue;
        }

        RewriteEntityAsset(entityAsset, buildRootPath);
        rewritten.Add(entityAsset);
    }

    return rewritten.ToArray();
}
```

At the top of `RewriteEntityAsset`, rewrite children through the new array helper and strip the editor-only existence metadata:

```csharp
entityAsset.Children = RewriteEntityAssets(entityAsset.Children ?? Array.Empty<SceneEntityAsset>(), buildRootPath);
entityAsset.PlatformExistenceOverrides = Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
entityAsset.LayerMask = NormalizePackagedEntityLayerMask(entityAsset.LayerMask);
ApplyTargetPlatformTransformOverride(entityAsset);
ApplyTargetPlatformComponentOverrides(entityAsset);
```

Add the predicate helper:

```csharp
bool ShouldEntityExistOnTargetPlatform(SceneEntityAsset entityAsset) {
    if (entityAsset == null) {
        throw new ArgumentNullException(nameof(entityAsset));
    }

    if (string.IsNullOrWhiteSpace(TargetPlatformId)
        || string.Equals(TargetPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return true;
    }

    SceneEntityPlatformExistenceOverrideAsset[] overrides = entityAsset.PlatformExistenceOverrides ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
    for (int index = 0; index < overrides.Length; index++) {
        SceneEntityPlatformExistenceOverrideAsset existenceOverride = overrides[index];
        if (existenceOverride == null || string.IsNullOrWhiteSpace(existenceOverride.PlatformId)) {
            continue;
        }

        if (string.Equals(existenceOverride.PlatformId, TargetPlatformId, StringComparison.OrdinalIgnoreCase)) {
            return existenceOverride.Exists;
        }
    }

    return true;
}
```

- [ ] **Step 4: Add a second packaging assertion that common `Enabled` survives when existence stays `true`**

Add this test in the same file:

```csharp
[Fact]
public void Package_WhenSceneEntityIsDisabledButStillExistsOnWindows_PreservesEnabledFalse() {
    string sceneId = "Scenes/DisabledButPresent.helen";
    WriteSceneAsset(sceneId, new SceneAsset {
        Id = sceneId,
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = 1u,
                Name = "DisabledRoot",
                Enabled = false,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = Array.Empty<SceneEntityAsset>()
            }
        }
    });

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        ProjectRootPath,
        Array.Empty<IAssetImporterRegistration>(),
        "windows");
    packager.Package(new[] { sceneId }, BuildRootPath);

    using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
    SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
    SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);

    Assert.False(packagedRoot.Enabled);
}
```

- [ ] **Step 5: Run the focused packaging slice and confirm it passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenSceneEntityDefinesDsExistenceFalse_PrunesTheEntireSubtreeBeforeComponentPackaging|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenSceneEntityIsDisabledButStillExistsOnWindows_PreservesEnabledFalse" -v minimal
```

Expected: `PASS`

- [ ] **Step 6: Commit target-platform subtree pruning**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: prune platform-absent entity subtrees during packaging"
```

---

### Task 4: Add Entity-Level Platform `Exists` Controls To The Properties Panel

**Files:**
- Create: `engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Write the failing entity inspector mutation test**

Add this test beside the existing platform transform tests:

```csharp
[Fact]
public void ShowEntityProperties_WhenDsExistsIsUnchecked_CreatesOnlyMetadataAndLeavesTheLiveEntityVisible() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity {
        Name = "BottomScreenCamera"
    };

    panel.ShowEntityProperties(entity, new[] { "ds" });
    SelectInspectorPlatform(panel, "ds");

    CheckBoxComponent existsCheckBox = GetPrivateField<CheckBoxComponent>(panel, "ExistsCheckBox");
    MethodInfo checkedChangedMethod = typeof(PropertiesPanel).GetMethod("HandleExistsCheckedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
    checkedChangedMethod.Invoke(panel, new object[] { existsCheckBox, false });

    EntitySaveComponent saveComponent = GetSaveComponent(entity);
    Assert.True(saveComponent.TryGetExistencePlatformOverride("ds", out SceneEntityPlatformExistenceOverrideAsset overrideState));
    Assert.False(overrideState.Exists);
    Assert.True(entity.Enabled);
}
```

- [ ] **Step 2: Run the focused entity inspector test and confirm it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenDsExistsIsUnchecked_CreatesOnlyMetadataAndLeavesTheLiveEntityVisible" -v minimal
```

Expected: `FAIL` because the properties panel has no entity-level platform `Exists` row or handler.

- [ ] **Step 3: Add one dedicated entity existence editing service**

Create `engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Reads, writes, and reverts one entity's platform-specific existence metadata.
    /// </summary>
    public sealed class EntityPlatformExistenceEditingService {
        /// <summary>
        /// Stable platform id used by the shared common entity state.
        /// </summary>
        public const string CommonPlatformId = ComponentPlatformEditingService.CommonPlatformId;

        /// <summary>
        /// Returns whether the entity effectively exists on the supplied platform.
        /// </summary>
        public bool ExistsOnPlatform(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return !saveComponent.TryGetExistencePlatformOverride(platformId, out SceneEntityPlatformExistenceOverrideAsset overrideState)
                || overrideState.Exists;
        }

        /// <summary>
        /// Stores one entity existence override for the supplied platform.
        /// </summary>
        public void SetExists(EntitySaveComponent saveComponent, string platformId, bool exists) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Common entity existence cannot be authored through a platform override.");
            }

            if (exists) {
                saveComponent.RemoveExistencePlatformOverride(platformId);
                return;
            }

            saveComponent.GetOrCreateExistencePlatformOverride(platformId).Exists = false;
        }
    }
}
```

- [ ] **Step 4: Add the entity-level `Exists` row to `PropertiesPanel`**

Add UI fields beside the transform rows:

```csharp
const int ExistsRowHeight = 24;

readonly EditorEntity ExistsRow;
readonly RoundedRectComponent ExistsOverrideOutline;
readonly EditorEntity ExistsRevertButtonHost;
readonly ButtonComponent ExistsRevertButton;
readonly CheckBoxComponent ExistsCheckBox;
readonly EntityPlatformExistenceEditingService ExistencePlatformEditingService;
```

Initialize the service:

```csharp
ExistencePlatformEditingService = new EntityPlatformExistenceEditingService();
```

When showing entity properties on a non-common tab, bind the row:

```csharp
bool showExistsRow = !string.Equals(SelectedPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase);
ExistsRow.Enabled = showExistsRow;
if (showExistsRow && saveComponent != null) {
    bool exists = ExistencePlatformEditingService.ExistsOnPlatform(saveComponent, SelectedPlatformId);
    ExistsCheckBox.IsChecked = exists;
    bool hasExplicitOverride = saveComponent.TryGetExistencePlatformOverride(SelectedPlatformId, out _);
    ExistsOverrideOutline.BorderThickness = hasExplicitOverride ? TransformOverrideOutlineThickness : 0f;
    ExistsRevertButtonHost.Enabled = hasExplicitOverride;
}
```

Add the checkbox handler:

```csharp
void HandleExistsCheckedChanged(CheckBoxComponent checkBox, bool isChecked) {
    if (SelectedEntity is not EditorEntity editorEntity) {
        return;
    }

    EntitySaveComponent saveComponent = FindEntitySaveComponent(editorEntity);
    if (saveComponent == null) {
        throw new InvalidOperationException("Selected editor entity requires an attached EntitySaveComponent.");
    }

    ExistencePlatformEditingService.SetExists(saveComponent, SelectedPlatformId, isChecked);
    ShowEntityProperties(SelectedEntity);
}
```

- [ ] **Step 5: Run the focused entity inspector test again and confirm it passes**

Run the same command from Step 2.

Expected: `PASS`

- [ ] **Step 6: Commit the entity inspector existence row**

```bash
git add engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
git commit -m "feat: add platform entity exists inspector control"
```

---

### Task 5: Expose Component-Level Platform `Exists` Checkboxes Through The Existing Override Model

**Files:**
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Write the failing component inspector tests**

Add these tests beside the existing platform-only/remove-revert tests:

```csharp
[Fact]
public void HandleRemoveComponentConfirmed_WhenWindowsTabRemovesMesh_ShowsExistsRowUncheckedForThePlaceholderSection() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = CreateEntityWithVisibleComponents();

    panel.ShowEntityProperties(entity, new[] { "windows" });
    SelectInspectorPlatform(panel, "windows");

    ComponentPropertiesView windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    List<ComponentSectionView> windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
    ComponentSectionView meshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);

    InvokePrivate(windowsView, "HandleSectionRemoveClicked", meshSection);
    InvokePrivate(panel, "HandleRemoveComponentConfirmed");
    SelectInspectorPlatform(panel, "windows");

    windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
    ComponentSectionView removedMeshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);
    ComponentPropertyRow existsRow = Assert.Single(removedMeshSection.Rows, row => string.Equals(row.Label.Text, "Exists", StringComparison.Ordinal));

    Assert.False(existsRow.CheckBoxField.IsChecked);
}
```

```csharp
[Fact]
public void HandleBooleanCheckedChanged_WhenPlatformExistsRowIsChecked_RestoresTheRemovedCommonComponent() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = CreateEntityWithVisibleComponents();

    panel.ShowEntityProperties(entity, new[] { "windows" });
    SelectInspectorPlatform(panel, "windows");

    ComponentPropertiesView windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    List<ComponentSectionView> windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
    ComponentSectionView meshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);

    InvokePrivate(windowsView, "HandleSectionRemoveClicked", meshSection);
    InvokePrivate(panel, "HandleRemoveComponentConfirmed");
    SelectInspectorPlatform(panel, "windows");

    windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
    ComponentSectionView removedMeshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);
    ComponentPropertyRow existsRow = Assert.Single(removedMeshSection.Rows, row => string.Equals(row.Label.Text, "Exists", StringComparison.Ordinal));

    MethodInfo checkedChangedMethod = typeof(ComponentPropertiesView).GetMethod("HandleBooleanCheckedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
    checkedChangedMethod.Invoke(windowsView, new object[] { existsRow.CheckBoxField, true });

    windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
    Assert.Contains(windowsSections, value => value.TargetComponent is MeshComponent && value.Rows.Count > 1);
}
```

- [ ] **Step 2: Run the focused component inspector tests and confirm they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests.HandleRemoveComponentConfirmed_WhenWindowsTabRemovesMesh_ShowsExistsRowUncheckedForThePlaceholderSection|FullyQualifiedName~PropertiesPanelComponentShellTests.HandleBooleanCheckedChanged_WhenPlatformExistsRowIsChecked_RestoresTheRemovedCommonComponent" -v minimal
```

Expected: `FAIL` because removed/platform-only sections do not yet expose an `Exists` row.

- [ ] **Step 3: Add explicit component existence helpers to `ComponentPlatformEditingService`**

Add these methods:

```csharp
public bool ExistsOnPlatform(Component component, EntitySaveComponent saveComponent, string platformId) {
    if (component == null) {
        throw new ArgumentNullException(nameof(component));
    } else if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    } else if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    }

    if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return true;
    }

    if (TryGetAddedComponentState(component, saveComponent, platformId, out _)) {
        return true;
    }

    return !IsComponentRemoved(component, saveComponent, platformId);
}

public void SetComponentExists(Component component, EntitySaveComponent saveComponent, string platformId, bool exists) {
    if (exists) {
        RevertComponentExistenceOverride(component, saveComponent, platformId);
        return;
    }

    RemoveComponent(component, saveComponent, platformId);
}
```

- [ ] **Step 4: Add one platform `Exists` row to every non-common component section**

In `ComponentPropertiesView`, insert this at the top of `AddPropertyRows(...)` for non-common platforms:

```csharp
if (!string.Equals(platformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
    AddComponentExistenceRow(section, commonComponent ?? editableComponent, saveComponent, platformId);
}
```

Add the binder:

```csharp
void AddComponentExistenceRow(
    ComponentSectionView section,
    Component component,
    EntitySaveComponent saveComponent,
    string platformId) {
    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Boolean);
    row.Label.Text = "Exists";
    row.TargetComponent = component;
    row.CommonComponent = component;
    row.SaveComponent = saveComponent;
    row.EditingPlatformId = platformId;
    row.IsPlatformExistenceRow = true;
    row.CheckBoxField.IsChecked = PlatformEditingService.ExistsOnPlatform(component, saveComponent, platformId);
    section.Rows.Add(row);
}
```

Extend the boolean handler:

```csharp
if (row.IsPlatformExistenceRow) {
    PlatformEditingService.SetComponentExists(row.TargetComponent, row.SaveComponent, row.EditingPlatformId, isChecked);
    RefreshCurrentView();
    return;
}
```

Add the row flag to `ComponentPropertyRow.cs`:

```csharp
/// <summary>
/// Gets or sets a value indicating whether the row edits platform-specific component existence instead of a reflected property.
/// </summary>
public bool IsPlatformExistenceRow { get; set; }
```

- [ ] **Step 5: Run the focused component inspector tests again and confirm they pass**

Run the same command from Step 2.

Expected: `PASS`

- [ ] **Step 6: Commit the component `Exists` inspector flow**

```bash
git add engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/components/ui/ComponentPropertyRow.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "feat: expose platform component exists checkboxes"
```
