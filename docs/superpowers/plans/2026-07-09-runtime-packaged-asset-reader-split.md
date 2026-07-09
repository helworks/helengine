# Runtime Packaged Asset Reader Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove editor-asset deserialization from `helengine.core` so packaged runtimes load only cooked/runtime asset payloads.

**Architecture:** Keep editor-authored asset serialization/deserialization in `helengine.files`, add a packaged-runtime asset reader seam in `helengine.core`, and repoint runtime content registration plus generic runtime asset loading at that packaged seam. Preserve the existing cooked scene/material/font payloads so platform build outputs do not need a format migration in this pass.

**Tech Stack:** C#, xUnit, helengine content pipeline, source-based guard tests, packaged runtime asset loading.

---

### Task 1: Add seam guard tests

**Files:**
- Modify: `engine/helengine.editor.tests/RuntimeContentManagerConfigurationSourceTests.cs`
- Create: `engine/helengine.editor.tests/RuntimeAssetSerializerSourceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void RuntimeContentManagerConfiguration_source_uses_packaged_scene_serializer() {
    string source = File.ReadAllText(sourcePath);

    Assert.Contains("PackagedAssetBinarySerializer.DeserializeSceneAsset", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EditorAssetBinarySerializer.DeserializeSceneAsset", source, StringComparison.Ordinal);
}

[Fact]
public void AssetSerializer_source_uses_packaged_asset_binary_serializer() {
    string source = File.ReadAllText(sourcePath);

    Assert.Contains("PackagedAssetBinarySerializer", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EditorAssetBinarySerializer", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeContentManagerConfigurationSourceTests|FullyQualifiedName~RuntimeAssetSerializerSourceTests"
```

Expected: FAIL because the runtime source still references `EditorAssetBinarySerializer`.

- [ ] **Step 3: Commit**

```bash
git add engine/helengine.editor.tests/RuntimeContentManagerConfigurationSourceTests.cs engine/helengine.editor.tests/RuntimeAssetSerializerSourceTests.cs
git commit -m "test: guard packaged runtime serializer seam"
```

### Task 2: Introduce packaged runtime serializer in `helengine.core`

**Files:**
- Create: `engine/helengine.core/assets/PackagedAssetBinarySerializer.cs`
- Create: `engine/helengine.core/assets/PackagedAssetBinaryValueKind.cs`
- Modify: `engine/helengine.core/assets/AssetSerializer.cs`
- Modify: `engine/helengine.core/content/RuntimeContentManagerConfiguration.cs`
- Delete: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Delete: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`

- [ ] **Step 1: Write the minimal packaged-runtime reader**

```csharp
public static class PackagedAssetBinarySerializer {
    public const ushort FormatId = 1;
    public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.Asset;
    public const byte CurrentVersion = 19;

    public static Asset Deserialize(Stream stream) { ... }
    public static Asset Deserialize(Stream stream, EngineBinaryHeader header) { ... }
    public static SceneAsset DeserializeSceneAsset(Stream stream) { ... }
    public static SceneAsset DeserializeSceneAsset(Stream stream, EngineBinaryHeader header) { ... }
}
```

Reader scope:

```text
TextureAsset
ModelAsset
TextAsset
MaterialAsset
PlatformMaterialAsset
AnimationClipAsset
SceneAsset
BlueprintAsset
```

Note: keep the same binary layout for the cooked payloads this pass; only the owning runtime type/seam changes.

- [ ] **Step 2: Repoint the runtime asset entry points**

```csharp
if (header.FormatId == PackagedAssetBinarySerializer.FormatId) {
    return PackagedAssetBinarySerializer.Deserialize(stream, header);
}
```

```csharp
new BinaryContentProcessor<SceneAsset>(PackagedAssetBinarySerializer.DeserializeSceneAsset)
```

- [ ] **Step 3: Remove the editor serializer from `helengine.core`**

```text
Delete the runtime copy of EditorAssetBinarySerializer and its value-kind enum from helengine.core.
Leave the editor/files serializer in helengine.files as the authoring-side implementation.
```

- [ ] **Step 4: Run the seam tests to verify they pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeContentManagerConfigurationSourceTests|FullyQualifiedName~RuntimeAssetSerializerSourceTests"
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/assets/PackagedAssetBinarySerializer.cs engine/helengine.core/assets/PackagedAssetBinaryValueKind.cs engine/helengine.core/assets/AssetSerializer.cs engine/helengine.core/content/RuntimeContentManagerConfiguration.cs engine/helengine.editor.tests/RuntimeContentManagerConfigurationSourceTests.cs engine/helengine.editor.tests/RuntimeAssetSerializerSourceTests.cs
git commit -m "refactor: split packaged runtime asset reader"
```

### Task 3: Prove packaged runtime behavior still works

**Files:**
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Add a behavioral regression test**

```csharp
[Fact]
public void AssetSerializer_WhenGivenPackagedScenePayload_DeserializesWithoutEditorSerializerDependency() {
    SceneAsset asset = new SceneAsset {
        Id = "Scenes/TestPackagedScene.helen",
        RootEntities = Array.Empty<SceneEntityAsset>(),
        AssetReferences = Array.Empty<SceneAssetReference>()
    };

    byte[] data = FilesAssetSerializer.SerializeToBytes(asset);
    SceneAsset deserialized = Assert.IsType<SceneAsset>(global::helengine.AssetSerializer.DeserializeFromBytes(data));

    Assert.Equal(asset.Id, deserialized.Id);
}
```

- [ ] **Step 2: Run the focused behavioral tests**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests.AssetSerializer_WhenGivenPackagedScenePayload_DeserializesWithoutEditorSerializerDependency|FullyQualifiedName~BinarySerializationTests.RuntimeContentManagerConfiguration_RegistersSceneAssetWithBinaryContentProcessor"
```

Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "test: cover packaged runtime asset deserialization"
```

### Task 4: Verify DS size/build impact

**Files:**
- No source changes required unless a build/test reveals a follow-up issue.

- [ ] **Step 1: Build the narrow DS slice used for size tracking**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'C:\dev\helworks\helengine\artifacts\build-platform.ps1' -Project 'C:\dev\helprojs\city\project.heproj' -Platform ds -Output 'C:\dev\helprojs\city\ds-size-check-runtime-split' -Configuration Release *> 'C:\dev\helprojs\city\ds-size-check-runtime-split-build.log'; exit $LASTEXITCODE"
```

Expected: PASS and a regenerated native binary size report under `C:\dev\helprojs\city\ds-size-check-runtime-split`.

- [ ] **Step 2: Compare the native binary report**

Run:

```powershell
rtk powershell -NoProfile -Command "Get-Content 'C:\dev\helprojs\city\ds-size-check-runtime-split\helengine_ds-native-binary-size-report.txt' -TotalCount 120"
```

Expected: the packaged runtime report no longer attributes space to `EditorAssetBinarySerializer.cpp`.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/plans/2026-07-09-runtime-packaged-asset-reader-split.md
git commit -m "docs: record packaged runtime asset reader split plan"
```
