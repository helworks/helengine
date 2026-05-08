# Model Asset Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clicking a model asset in the browser shows a live 3D preview that can orbit and zoom, while model bounds are cached at import time and reused for framing.

**Architecture:** Store model bounds on the raw `ModelAsset` during import, preserve them on `RuntimeModel`, and copy them through the renderer build path so every model preview has one authoritative frame reference. Add a dedicated `ModelPreviewSource` for model assets that owns an offscreen camera, a preview mesh, and orbit/zoom state; the preview panel forwards wheel and drag input only when the active source opts into 3D interaction. Keep texture previews and scene-camera previews on their current paths.

**Tech Stack:** C#, engine core/editor runtime, Vulkan and DirectX11 3D backends, xUnit.

---

### Task 1: Cache model bounds on import and preserve them through runtime model creation

**Files:**
- Modify: `engine/helengine.core/assets/raw/ModelAsset.cs`
- Modify: `engine/helengine.core/assets/RuntimeModel.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Create: `engine/helengine.core/utils/ModelAssetBounds.cs`
- Modify: `engine/helengine.core/utils/ModelUtils.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.vulkan/VulkanRenderer3D.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Add a serializer regression that proves model bounds round-trip with the asset payload and a model-import regression that proves imported cached models keep the computed bounds.

```csharp
[Fact]
public void AssetSerializer_ModelAsset_WithBounds_RoundTrips() {
    ModelAsset asset = CreateModelAsset();
    asset.BoundsMin = new float3(-2f, -3f, -4f);
    asset.BoundsMax = new float3(5f, 6f, 7f);

    byte[] data = AssetSerializer.SerializeToBytes(asset);
    ModelAsset deserialized = (ModelAsset)AssetSerializer.DeserializeFromBytes(data);

    Assert.Equal(asset.BoundsMin, deserialized.BoundsMin);
    Assert.Equal(asset.BoundsMax, deserialized.BoundsMax);
}
```

```csharp
[Fact]
public void ImportModel_WhenModelIsImported_CachesBounds() {
    string sourcePath = WriteSourceModel("bounds.obj");
    TestModelImporter modelImporter = new TestModelImporter();
    AssetImportManager manager = CreateManager(modelImporter);

    ModelAsset importedAsset = manager.ImportModel(sourcePath);

    Assert.Equal(new float3(0f, 0f, 0f), importedAsset.BoundsMin);
    Assert.Equal(new float3(1f, 1f, 0f), importedAsset.BoundsMax);
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "BinarySerializationTests|AssetImportManagerModelTests"
```

Expected: the new assertions fail because model bounds are not yet stored or serialized.

- [ ] **Step 3: Implement the bounds pipeline**

Add `BoundsMin` and `BoundsMax` to `ModelAsset`, add matching fields or properties to `RuntimeModel`, and thread them through `EditorAssetBinarySerializer.WriteModelAsset` / `ReadModelAsset`.

```csharp
public float3 BoundsMin;
public float3 BoundsMax;

public float3 BoundsSize => BoundsMax - BoundsMin;
```

Create or update the shared bounds calculation so model utilities and imported model assets both assign the same data after vertex positions are finalized. `AssetImportManager.ImportModel` should compute the bounds after processor settings run and before the asset is written to cache.

Update both 3D backends so `BuildModelFromRaw(ModelAsset data)` copies `data.BoundsMin` and `data.BoundsMax` onto the runtime model resource before returning it.

- [ ] **Step 4: Run the tests to confirm they pass**

Run:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "BinarySerializationTests|AssetImportManagerModelTests"
```

Expected: both new tests pass, and the existing model tests still pass.

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.core/assets/raw/ModelAsset.cs engine/helengine.core/assets/RuntimeModel.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.core/utils/ModelUtils.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.vulkan/VulkanRenderer3D.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/AssetImportManagerModelTests.cs
git commit -m "Cache model bounds for previews"
```

### Task 2: Add an interactive model preview source and forward pointer input from the preview panel

**Files:**
- Create: `engine/helengine.editor/managers/preview/IPreviewInteractionSource.cs`
- Create: `engine/helengine.editor/managers/preview/ModelPreviewSource.cs`
- Modify: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Test: `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- Test: `engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs`
- Test: `engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs`
- Test: `engine/helengine.editor.tests/testing/TestInteractivePreviewSource.cs`

- [ ] **Step 1: Write the failing tests**

Add one resolver test that selects a model asset entry and expects a model preview source, plus panel tests that prove wheel and drag input get forwarded to interactive preview sources.

```csharp
[Fact]
public void TryResolve_WhenModelAssetIsSelected_ReturnsModelPreviewSource() {
    PreviewSourceResolver resolver = CreateResolver();
    AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
        "Ship.obj",
        "Models/Ship.obj",
        WriteSourceModel("Ship.obj"),
        ".obj",
        AssetEntryKind.Model);

    bool resolved = resolver.TryResolve(entry, null, out IPreviewSource source);

    Assert.True(resolved);
    Assert.IsType<ModelPreviewSource>(source);
}
```

```csharp
PreviewSourceResolver CreateResolver() {
    AssetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
    return new PreviewSourceResolver(AssetImportManager, Core.Instance.RenderManager2D, Core.Instance.RenderManager3D);
}
```

```csharp
string WriteSourceModel(string fileName) {
    if (string.IsNullOrWhiteSpace(fileName)) {
        throw new ArgumentException("File name must be provided.", nameof(fileName));
    }

    string sourcePath = Path.Combine(AssetsRootPath, fileName);
    File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
    return sourcePath;
}
```

```csharp
[Fact]
public void UpdatePreviewSource_WhenInteractivePreviewIsAssigned_ForwardsWheelAndDrag() {
    PreviewPanel panel = new PreviewPanel(CreateFont()) { Size = new int2(416, 312) };
    TestInteractivePreviewSource source = new TestInteractivePreviewSource(new TestRuntimeTexture { Width = 64, Height = 64 });
    panel.SetPreviewSource(source);

    panel.UpdatePreviewSource();

    Assert.Equal(1, source.UpdateCount);
    Assert.Equal(1, source.WheelCount);
    Assert.Equal(1, source.DragCount);
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "PreviewSourceResolverTests|PreviewPanelTests|ModelPreviewSourceTests"
```

Expected: the new model preview source does not exist yet, and the panel still only knows texture-specific input handling.

- [ ] **Step 3: Implement the preview source and interaction contract**

Create an interaction interface with the minimal input the preview panel should forward:

```csharp
public interface IPreviewInteractionSource {
    void HandleMouseWheel(int wheelDelta);
    void HandleMouseDrag(int2 delta);
}
```

```csharp
internal sealed class TestInteractivePreviewSource : IPreviewSource, IPreviewInteractionSource {
    public TestInteractivePreviewSource(RuntimeTexture texture) {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
    }

    public RuntimeTexture Texture { get; }
    public int UpdateCount { get; private set; }
    public int WheelCount { get; private set; }
    public int DragCount { get; private set; }

    public void HandleMouseWheel(int wheelDelta) {
        WheelCount++;
    }

    public void HandleMouseDrag(int2 delta) {
        DragCount++;
    }

    public void Resize(int2 contentSize) {
    }

    public void Update() {
        UpdateCount++;
    }

    public void Dispose() {
    }
}
```

Implement `ModelPreviewSource` so it:
- Resolves a model asset from the selected browser entry.
- Builds the runtime model through `RenderManager3D`.
- Creates a hidden preview entity with a mesh component and a preview camera.
- Uses cached bounds to center the model and derive the initial camera distance.
- Rotates the camera around the bounds center on left-drag.
- Adjusts the camera distance on wheel input.
- Resizes its render target when the preview panel changes size.

Use the existing editor standard material path for the preview mesh so the preview is neutral and readable without inventing a new shader path.

Update `PreviewSourceResolver.TryResolve` so model asset entries resolve to `ModelPreviewSource` before the texture branch, and update `PreviewPanel.UpdatePreviewSource` so it forwards wheel and drag input only when the active source implements `IPreviewInteractionSource`.

- [ ] **Step 4: Run the tests to confirm they pass**

Run:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "PreviewSourceResolverTests|PreviewPanelTests|ModelPreviewSourceTests"
```

Expected: resolver, panel forwarding, and model preview interaction tests pass.

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.editor/managers/preview/IPreviewInteractionSource.cs engine/helengine.editor/managers/preview/ModelPreviewSource.cs engine/helengine.editor/managers/preview/PreviewSourceResolver.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs engine/helengine.editor.tests/testing/TestInteractivePreviewSource.cs
git commit -m "Add interactive model preview source"
```

### Task 3: Wire model preview selection through editor session coverage and verify the full model-preview path

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionModelAssetSelectionTests.cs`
- Modify: `engine/helengine.editor.tests/testing/TestPreviewSource.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Test: `engine/helengine.editor.tests/PreviewPanelTests.cs`
- Test: `engine/helengine.editor.tests/PreviewSourceResolverTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionModelAssetSelectionTests.cs`

- [ ] **Step 1: Write the failing editor-session regression**

Add a session test that selects a file-system model entry and asserts the preview panel binds a `ModelPreviewSource` instead of leaving the panel empty or falling back to texture handling.

```csharp
[Fact]
public void HandleAssetSelected_WhenModelAssetIsSelected_ReplacesPreviewWithModelPreviewSource() {
    EditorSession session = CreateSession();
    AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
        "Ship.obj",
        "Models/Ship.obj",
        WriteSourceModel("Ship.obj"),
        ".obj",
        AssetEntryKind.Model);

    InvokePrivate(session, "HandleAssetSelected", entry);

    PreviewPanel previewPanel = GetPrivateField<PreviewPanel>(session, "previewPanel");
    Assert.IsType<ModelPreviewSource>(previewPanel.ActivePreviewSource);
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "EditorSessionPreviewSelectionTests|EditorSessionModelAssetSelectionTests"
```

Expected: the editor session still routes model entries through the old preview selection path.

- [ ] **Step 3: Update the session-facing tests and any test doubles**

Keep the existing texture and camera assertions intact, then add the model-selection assertion above. If the new interaction interface requires a test stub, add it under `engine/helengine.editor.tests/testing/` with explicit counters for wheel and drag forwarding.

- [ ] **Step 4: Run the tests and a focused build sweep**

Run:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "BinarySerializationTests|AssetImportManagerModelTests|PreviewSourceResolverTests|PreviewPanelTests|ModelPreviewSourceTests|EditorSessionPreviewSelectionTests|EditorSessionModelAssetSelectionTests"
dotnet build engine/helengine.core/helengine.core.csproj --no-restore
dotnet build engine/helengine.editor/helengine.editor.csproj --no-restore
dotnet build engine/helengine.vulkan/helengine.vulkan.csproj --no-restore -m:1
dotnet build engine/helengine.directx11/helengine.directx11.csproj --no-restore -m:1
```

Expected: the model-preview test set passes, and the renderer builds confirm the runtime model bounds changes were copied through both backends.

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs engine/helengine.editor.tests/EditorSessionModelAssetSelectionTests.cs engine/helengine.editor.tests/testing/TestPreviewSource.cs engine/helengine.editor.tests/testing/TestInteractivePreviewSource.cs
git commit -m "Cover model preview selection in editor session tests"
```
