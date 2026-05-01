# Preview Source Resolver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the texture-only preview panel with a resolver-driven preview-source pipeline that supports texture previews now and live camera previews when a camera entity is selected.

**Architecture:** `PreviewPanel` becomes a host that renders the active preview source and forwards resize/update events. A dedicated `PreviewSourceResolver` decides which source should be active from the current asset/entity selection snapshot, with camera preview taking precedence over texture preview. `EditorSession` owns the current selection snapshot and asks the resolver to recompute the preview whenever asset or scene selection changes.

**Tech Stack:** C#, xUnit, the existing editor UI layer, `RenderManager2D`, `RenderManager3D`, and the current offscreen camera/render-target pipeline.

---

### Task 1: Introduce Preview Source Plumbing And Convert `PreviewPanel` Into A Host

**Files:**
- Create: `engine/helengine.editor/managers/preview/IPreviewSource.cs`
- Create: `engine/helengine.editor/managers/preview/TexturePreviewSource.cs`
- Create: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- Create: `engine/helengine.editor/components/ui/PreviewPanelUpdater.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Test: `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- Test: `engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create one resolver test that proves a texture asset selection resolves to a preview source:

```csharp
[Fact]
public void TryResolve_WhenTextureAssetIsSelected_ReturnsTexturePreviewSource() {
    TextureAsset texture = new TextureAsset {
        Width = 64,
        Height = 32,
        Colors = new byte[64 * 32 * 4]
    };

    bool resolved = resolver.TryResolve(textureEntry, null, out IPreviewSource source);

    Assert.True(resolved);
    Assert.IsType<TexturePreviewSource>(source);
}
```

Create one panel test that proves binding a new preview source replaces the previous one instead of layering sources:

```csharp
[Fact]
public void SetPreviewSource_WhenNewSourceIsAssigned_DisposesThePreviousSource() {
    PreviewPanel panel = new PreviewPanel(CreateFont());
    TestPreviewSource first = new TestPreviewSource(new TestRuntimeTexture { Width = 32, Height = 32 });
    TestPreviewSource second = new TestPreviewSource(new TestRuntimeTexture { Width = 64, Height = 64 });

    panel.SetPreviewSource(first);
    panel.SetPreviewSource(second);

    Assert.True(first.IsDisposed);
    Assert.Same(second, panel.ActivePreviewSource);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PreviewSourceResolverTests|FullyQualifiedName~PreviewPanelTests" -m:1 -p:UseSharedCompilation=false -p:DisableFastUpToDateCheck=true
```

Expected: compile or assertion failures because the new resolver/source API does not exist yet.

- [ ] **Step 3: Implement the preview-source interface, texture source, resolver, and panel host**

Implement the API so the panel owns one active source at a time:

```csharp
public interface IPreviewSource : IDisposable {
    RuntimeTexture Texture { get; }
    void Resize(int2 contentSize);
    void Update();
}
```

`TexturePreviewSource` should wrap the existing `RenderManager2D.BuildTextureFromRaw(TextureAsset)` flow and expose the created runtime texture.

`PreviewPanel` should:

- keep one active `IPreviewSource`,
- dispose the previous source when a new one is bound,
- keep the existing title bar and layout behavior,
- forward `Resize` calls from `OnSizeChanged`,
- forward frame updates through `PreviewPanelUpdater`,
- keep the sprite texture in sync with the active source.

- [ ] **Step 4: Run the focused tests again**

Run the same `dotnet test` command and confirm both tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/preview/IPreviewSource.cs engine/helengine.editor/managers/preview/TexturePreviewSource.cs engine/helengine.editor/managers/preview/PreviewSourceResolver.cs engine/helengine.editor/components/ui/PreviewPanelUpdater.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs
git commit -m "feat: add preview source host plumbing"
```

### Task 2: Add Live Camera Preview Source And Renderer Test Support

**Files:**
- Create: `engine/helengine.editor/managers/preview/CameraPreviewSource.cs`
- Create: `engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs`
- Create: `engine/helengine.editor.tests/testing/TestRenderTarget.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`

- [ ] **Step 1: Write the failing tests**

Create one test that proves a selected camera with authored suppression state is mirrored into the preview camera:

```csharp
[Fact]
public void Update_WhenSuppressionStateExists_UsesAuthoredCameraValues() {
    EditorEntity cameraEntity = new EditorEntity();
    CameraComponent liveCamera = new CameraComponent {
        LayerMask = 0,
        Viewport = new float4(0f, 0f, 1f, 1f),
        ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1f, false, 0)
    };
    cameraEntity.AddComponent(liveCamera);
    cameraEntity.AddComponent(new EditorSceneCameraSuppressionComponent(
        7,
        0x1234,
        new float4(0f, 0f, 640f, 360f),
        new CameraClearSettings(true, new float4(1f, 2f, 3f, 1f), true, 1f, false, 0)));

    CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, renderManager);
    source.Update();

    Assert.Equal(0x1234, source.PreviewCamera.LayerMask);
    Assert.Equal(new float4(0f, 0f, 640f, 360f), source.PreviewCamera.Viewport);
}
```

Create one test that proves resizing the source changes the render target dimensions:

```csharp
[Fact]
public void Resize_WhenPanelSizeChanges_RebuildsTheRenderTarget() {
    CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, renderManager);

    source.Resize(new int2(320, 180));

    Assert.Equal(320, source.RenderTarget.Width);
    Assert.Equal(180, source.RenderTarget.Height);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraPreviewSourceTests" -m:1 -p:UseSharedCompilation=false -p:DisableFastUpToDateCheck=true
```

Expected: compile or assertion failures because the camera preview source and test render target do not exist yet.

- [ ] **Step 3: Implement the camera preview source and renderer test double**

`CameraPreviewSource` should:

- own a hidden offscreen camera entity,
- own a render target created through `RenderManager3D.CreateRenderTarget`,
- copy the selected camera's world transform into the preview camera,
- copy the authored suppression state when `EditorSceneCameraSuppressionComponent` is present,
- rebuild the render target when `Resize` receives a new content size,
- dispose both the hidden entity and the render target when released.

`TestRenderManager3D` should override `CreateRenderTarget(int width, int height)` and return a lightweight test render target.

`TestRenderTarget` should inherit from `RenderTarget` and only carry width, height, and disposable state needed by tests.

- [ ] **Step 4: Run the focused tests again**

Run the same `dotnet test` command and confirm the camera preview tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/preview/CameraPreviewSource.cs engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs engine/helengine.editor.tests/testing/TestRenderTarget.cs engine/helengine.editor.tests/testing/TestRenderManager3D.cs
git commit -m "feat: add camera preview source"
```

### Task 3: Wire `EditorSession` To The Resolver And Add Selection Regression Coverage

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionModelAssetSelectionTests.cs` if the existing fixture is the best place for the asset-preview baseline

- [ ] **Step 1: Write the failing tests**

Create one session test that proves a selected camera replaces an existing texture preview:

```csharp
[Fact]
public void HandleSelectionChanged_WhenCameraIsSelected_ReplacesTexturePreview() {
    EditorSession session = CreateSession();
    InvokePrivate(session, "HandleAssetSelected", textureEntry);
    InvokePrivate(session, "HandleSelectionChanged", new EditorSelectionChangedEventArgs(cameraEntity, true));

    PreviewPanel panel = GetPrivateField<PreviewPanel>(session, "previewPanel");
    Assert.IsType<CameraPreviewSource>(GetPrivateField<object>(panel, "activePreviewSource"));
}
```

Create one session test that proves clearing the selection snapshot clears the preview when nothing previewable remains:

```csharp
[Fact]
public void HandleSelectionChanged_WhenNothingPreviewableRemains_ClearsPreview() {
    EditorSession session = CreateSession();
    InvokePrivate(session, "HandleAssetSelected", textureEntry);
    InvokePrivate(session, "HandleSelectionChanged", new EditorSelectionChangedEventArgs(null, false));

    PreviewPanel panel = GetPrivateField<PreviewPanel>(session, "previewPanel");
    Assert.Null(GetPrivateField<object>(panel, "activePreviewSource"));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionPreviewSelectionTests|FullyQualifiedName~EditorSessionModelAssetSelectionTests" -m:1 -p:UseSharedCompilation=false -p:DisableFastUpToDateCheck=true
```

Expected: compile or assertion failures because `EditorSession` does not yet cache the selection snapshot or ask the resolver for a preview source.

- [ ] **Step 3: Implement the session snapshot and resolver wiring**

Update `EditorSession` so it keeps the current asset selection and entity selection in fields, then resolves a preview source whenever either changes.

The session should:

- cache the last selected asset entry,
- cache the last selected entity,
- call `PreviewSourceResolver.TryResolve(...)` after asset selection changes and scene selection changes,
- bind the resulting source to `PreviewPanel`,
- clear the preview when the resolver returns no source,
- preserve the existing properties-panel behavior.

- [ ] **Step 4: Run the session tests again**

Run the same `dotnet test` command and confirm the camera-over-texture replacement and preview-clearing tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs engine/helengine.editor.tests/EditorSessionModelAssetSelectionTests.cs
git commit -m "feat: wire preview resolver into editor session"
```

### Task 4: Verify The Full Editor Test Slice

**Files:**
- No new files expected

- [ ] **Step 1: Run the focused editor test slice**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PreviewSourceResolverTests|FullyQualifiedName~PreviewPanelTests|FullyQualifiedName~CameraPreviewSourceTests|FullyQualifiedName~EditorSessionPreviewSelectionTests|FullyQualifiedName~EditorSessionModelAssetSelectionTests" -m:1 -p:UseSharedCompilation=false -p:DisableFastUpToDateCheck=true
```

Expected: all preview-related tests pass, and the existing editor selection tests continue to pass.

- [ ] **Step 2: Spot-check the updated preview flow in the editor**

Open the editor, select a texture asset, then select a camera entity.

Expected behavior:

- texture selection shows the texture preview,
- camera selection replaces it with a live camera preview,
- clearing selection removes the preview when no previewable source remains.

- [ ] **Step 3: Commit the feature branch changes**

```bash
git add engine/helengine.editor engine/helengine.editor.tests docs/superpowers/plans/2026-05-01-preview-source-resolver.md
git commit -m "feat: add resolver-driven preview sources"
```
