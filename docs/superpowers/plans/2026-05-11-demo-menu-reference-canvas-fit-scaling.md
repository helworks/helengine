# Demo Menu Reference Canvas Fit Scaling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the demo-disc main menu adapt from its authored `1280x720` reference canvas to the live player window so `640x480` uses a real `640x480` camera viewport and a uniformly scaled menu layout.

**Architecture:** Add one generic runtime reference-canvas fit component that reads the authored scene canvas profile, derives a uniform fit scale from the live window size, and applies that scale to one UI subtree. Keep the authored menu scene in `1280x720`, bind the runtime camera to the live window, and stamp the reference canvas explicitly into the scene settings so the runtime contract is data-driven.

**Tech Stack:** C#, xUnit, existing scene serialization, existing UI components (`ViewportComponent`, `AnchorComponent`, `RoundedRectComponent`, `TextComponent`, `ScrollComponent`, `ClipRectComponent`), Windows player verification.

---

### Task 1: Lock The Reference Canvas Contract In Tests

**Files:**
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`
- Create: `engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs`

- [ ] **Step 1: Write the failing scene-authoring test**

Add assertions to `WriteAll_WhenMenuSceneIsGenerated_BakesViewportAndPanelAnchorComponentsForResponsiveLayout` so the generated menu scene must explicitly persist a `1280x720` scene canvas profile and must no longer bake the menu camera viewport as fixed `1280x720`.

```csharp
Assert.Equal(1280, sceneAsset.SceneSettings.CanvasProfile.Width);
Assert.Equal(720, sceneAsset.SceneSettings.CanvasProfile.Height);
Assert.Equal(new float4(0f, 0f, 1f, 1f), cameraComponent.Viewport);
```

- [ ] **Step 2: Run the authoring test to verify it fails**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter WriteAll_WhenMenuSceneIsGenerated_BakesViewportAndPanelAnchorComponentsForResponsiveLayout --no-restore`

Expected: FAIL because the generated scene does not stamp the canvas profile explicitly and still bakes `1280x720` into the camera viewport.

- [ ] **Step 3: Write the failing runtime scaling tests**

Create `ReferenceCanvasFitComponentTests.cs` covering:
- live window `640x480` against reference `1280x720` yields scale `0.5`
- a scaled child `RoundedRectComponent.Size` changes from `560x420` to `280x210`
- a scaled child entity `LocalPosition` changes from `(88,190)` to `(44,95)`
- a scaled `ScrollComponent.Size` and `ClipRectComponent.Size` follow the same rule

Use a minimal entity tree rooted by a new generic fit component and a `ViewportComponent` in screen mode.

- [ ] **Step 4: Run the new runtime scaling tests to verify they fail**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter ReferenceCanvasFitComponentTests --no-restore`

Expected: FAIL because the fit component does not exist yet.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs
git commit -m "Add failing tests for menu reference canvas fit scaling"
```

### Task 2: Implement Generic Reference-Canvas Fit Scaling

**Files:**
- Create: `engine/helengine.core/components/ReferenceCanvasFitComponent.cs`
- Create: `engine/helengine.core/components/ReferenceCanvasFitSnapshot.cs`
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.core/CoreInitializationOptions.cs`
- Modify: `engine/helengine.core/components/CameraComponent.cs`

- [ ] **Step 1: Write the minimal runtime component skeleton**

Add a generic `ReferenceCanvasFitComponent` that:
- stores reference canvas width and height
- subscribes to `RenderManager3D.WindowResized`
- captures authored local positions and supported component sizes once
- computes `scale = min(liveWidth / referenceWidth, liveHeight / referenceHeight)`
- reapplies scaled values on initialization and resize

Add a small snapshot type to hold authored values per entity and per supported component.

- [ ] **Step 2: Make the supported UI components scale**

In the fit component, handle the current generic size-bearing menu/UI components explicitly:
- `RoundedRectComponent.Size`
- `TextComponent.Size`
- `ScrollComponent.Size`
- `ClipRectComponent.Size`
- `ViewportComponent.FixedSize`

Also scale descendant entity `LocalPosition`.

- [ ] **Step 3: Keep the camera viewport bound to the real window**

Use the existing normalized-camera path by leaving the menu camera viewport at `(0,0,1,1)` and ensure runtime never rewrites it back to fixed `1280x720`.

- [ ] **Step 4: Run the runtime scaling tests**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter ReferenceCanvasFitComponentTests --no-restore`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/ReferenceCanvasFitComponent.cs engine/helengine.core/components/ReferenceCanvasFitSnapshot.cs engine/helengine.core/components/CameraComponent.cs
git commit -m "Add generic reference canvas fit scaling component"
```

### Task 3: Wire The Generated Demo Menu Scene To The Generic Contract

**Files:**
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Stamp the authored reference canvas into the generated scene**

Update `DemoMenuSceneAssetFactory.BuildSceneAsset(...)` so the returned `SceneAsset` explicitly includes:

```csharp
SceneSettings = new SceneSettingsAsset {
    CanvasProfile = new SceneCanvasProfile {
        Width = DemoMenuLayout.CanvasWidth,
        Height = DemoMenuLayout.CanvasHeight
    }
}
```

- [ ] **Step 2: Bind the menu camera to the live window**

Update the generated camera payload so the menu camera viewport is authored as:

```csharp
new float4(0f, 0f, 1f, 1f)
```

instead of fixed `1280x720`.

- [ ] **Step 3: Attach the generic fit component to the menu root**

Add a serialized `ReferenceCanvasFitComponent` to `DemoDiscMenuRoot` using:
- reference width `1280`
- reference height `720`

and keep the existing `ViewportComponent` in screen-binding mode as the live bounds source.

- [ ] **Step 4: Run the authoring tests**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter DemoDiscSceneWriterTests --no-restore`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "Wire demo menu scene to reference canvas fit scaling"
```

### Task 4: End-To-End Verification

**Files:**
- Modify: `engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Run the focused editor test suite**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "ReferenceCanvasFitComponentTests|DemoDiscSceneWriterTests" --no-restore`

Expected: PASS

- [ ] **Step 2: Rebuild the known-good copied city workspace**

Run: `dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helworks\helengine\.tmp-city-640x480-build --build windows --output C:\tmp\city-windows-640x480-ui-verify`

Expected: Build completes successfully.

- [ ] **Step 3: Launch the rebuilt player and inspect logs**

Run a short launch and verify:
- startup log reports `640x480`
- render log no longer shows the menu camera as fixed `1280x720`

- [ ] **Step 4: Manual visual verification**

Open `C:\tmp\city-windows-640x480-ui-verify\helengine_windows.exe` and confirm:
- the menu fits inside `640x480`
- the shell is visibly scaled down from the `1280x720` reference
- panel and item sizing are reduced, not just repositioned

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "Verify demo menu reference canvas fit scaling"
```
